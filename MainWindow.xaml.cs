using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using Wincent;
using Application = System.Windows.Application;
using NotifyIcon = System.Windows.Forms.NotifyIcon;


namespace CleanRecentMini
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon trayIcon;
        private Config config;
        private FileSystemWatcher automaticDestinationsWatcher;
        private const string FOLDERS_APPID = "f01b4d95cf55d32a";
        private const string RECENT_FILES_APPID = "5f7b5f1e01b83767";

        private ToolStripMenuItem languageMenu;
        private Dictionary<string, ToolStripMenuItem> languageItems = new Dictionary<string, ToolStripMenuItem>();

        private List<string> _currentRecentFiles = new List<string>();
        private List<string> _currentFrequentFolders = new List<string>();

        private readonly object _processingLock = new object();
        private bool _isProcessing = false;

        public MainWindow()
        {
            LoadConfig();
            InitializeLanguage();
            InitializeComponent();
            InitializeTrayIcon();
            if (config.IncognitoMode)
            {
                StartWatching();
            }
        }

        private void InitializeLanguage()
        {
            var culture = new CultureInfo(config.Language);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Properties.Resources.Culture = culture;
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            var autoStartItem = new ToolStripMenuItem(
                Properties.Resources.AutoStart,
                null, OnAutoStartClick)
            {
                Checked = config.AutoStart,
                CheckOnClick = true
            };
            var incognitoModeItem = new ToolStripMenuItem(
                Properties.Resources.IncognitoMode,
                null, OnIncognitoModeClick)
            {
                Checked = config.IncognitoMode,
                CheckOnClick = true
            };

            languageMenu = new ToolStripMenuItem(Properties.Resources.Language);
            foreach (var lang in Config.SupportedLanguages)
            {
                var langItem = new ToolStripMenuItem(lang.DisplayName)
                {
                    Tag = lang.Code,
                    Checked = lang.Code == config.Language,
                    CheckOnClick = false
                };
                langItem.Click += OnLanguageItemClick;
                languageMenu.DropDownItems.Add(langItem);
                languageItems[lang.Code] = langItem;
            }

            var aboutItem = new ToolStripMenuItem(
                Properties.Resources.About,
                null, OnAboutClick);

            var exitItem = new ToolStripMenuItem(
                Properties.Resources.Exit,
                null, OnExitClick);

            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                autoStartItem,
                incognitoModeItem,
                new ToolStripSeparator(),
                languageMenu,
                aboutItem,
                new ToolStripSeparator(),
                exitItem
            });

            trayIcon.ContextMenuStrip = contextMenu;
        }

        private void OnLanguageItemClick(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null && menuItem.Tag is string langCode)
            {
                foreach (var item in languageItems.Values)
                {
                    item.Checked = false;
                }
                menuItem.Checked = true;

                config.Language = langCode;
                Config.Save(config);

                InitializeLanguage();

                RefreshMenuTexts();
            }
        }

        private void RefreshMenuTexts()
        {
            var contextMenu = trayIcon.ContextMenuStrip;
            if (contextMenu != null)
            {
                if (contextMenu.Items[0] is ToolStripMenuItem autoStartItem)
                    autoStartItem.Text = Properties.Resources.AutoStart;

                if (contextMenu.Items[1] is ToolStripMenuItem incognitoModeItem)
                    incognitoModeItem.Text = Properties.Resources.IncognitoMode;

                if (contextMenu.Items[3] is ToolStripMenuItem languageMenuItem)
                    languageMenuItem.Text = Properties.Resources.Language;

                if (contextMenu.Items[4] is ToolStripMenuItem aboutItem)
                    aboutItem.Text = Properties.Resources.About;

                if (contextMenu.Items[6] is ToolStripMenuItem exitItem)
                    exitItem.Text = Properties.Resources.Exit;
            }
        }

        private void LoadConfig()
        {
            config = Config.Load();
            UpdateAutoStart(config.AutoStart);
        }

        private void OnAutoStartClick(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            config.AutoStart = menuItem.Checked;
            UpdateAutoStart(config.AutoStart);
            Config.Save(config);
        }

        private void OnIncognitoModeClick(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            config.IncognitoMode = menuItem.Checked;
            Config.Save(config);

            if (config.IncognitoMode)
            {
                StartWatching();
            }
            else
            {
                StopWatching();
            }
        }

        private void OnAboutClick(object sender, EventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.ShowDialog();
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void UpdateAutoStart(bool enable)
        {
            string appName = "CleanRecentMini";
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (enable)
                {
                    key.SetValue(appName, appPath);
                }
                else
                {
                    key.DeleteValue(appName, false);
                }
            }
        }

        private async void StartWatching()
        {
            if (automaticDestinationsWatcher != null) return;

            try
            {
                await RefreshCurrentQuickAccessItems();

                string automaticDestPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Recent\AutomaticDestinations");

                if (!Directory.Exists(automaticDestPath))
                {
                    Console.WriteLine("无法监控快速访问：目录不存在", ToolTipIcon.Warning);
                    return;
                }

                automaticDestinationsWatcher = new FileSystemWatcher
                {
                    Path = automaticDestPath,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    Filter = "*.automaticDestinations-ms"
                };

                automaticDestinationsWatcher.Changed += OnAutomaticDestinationsChanged;
                automaticDestinationsWatcher.Created += OnAutomaticDestinationsChanged;
                automaticDestinationsWatcher.EnableRaisingEvents = true;

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始监控快速访问变化");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 启动监控时出错: {ex.Message}");
            }
        }

        private void StopWatching()
        {
            if (automaticDestinationsWatcher != null)
            {
                automaticDestinationsWatcher.EnableRaisingEvents = false;
                automaticDestinationsWatcher.Dispose();
                automaticDestinationsWatcher = null;

                _currentRecentFiles.Clear();
                _currentFrequentFolders.Clear();

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 停止监控快速访问变化");
            }
        }

        private async Task RefreshCurrentQuickAccessItems()
        {
            try
            {
                _currentRecentFiles = await QuickAccessQuery.GetRecentFilesAsync();
                _currentFrequentFolders = await QuickAccessQuery.GetFrequentFoldersAsync();

                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 已刷新快速访问项目");
                Console.WriteLine($"当前最近文件数量: {_currentRecentFiles.Count}");
                Console.WriteLine($"当前常用文件夹数量: {_currentFrequentFolders.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 刷新快速访问项目时出错: {ex.Message}");
            }
        }

        private async void OnAutomaticDestinationsChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 使用锁防止重复处理
                lock (_processingLock)
                {
                    if (_isProcessing) return;
                    _isProcessing = true;
                }

                // 防止重复触发，等待文件写入完成
                await Task.Delay(500);

                string fileName = Path.GetFileNameWithoutExtension(e.Name);
                if (string.IsNullOrEmpty(fileName)) return;

                // 移除文件扩展名部分
                fileName = fileName.Replace(".automaticDestinations", "");

                switch (fileName.ToLower())
                {
                    case FOLDERS_APPID:
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 常用文件夹已更改");
                        if (config.IncognitoMode)
                        {
                            await ProcessFrequentFoldersChange();
                        }
                        break;

                    case RECENT_FILES_APPID:
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 最近文件已更改");
                        if (config.IncognitoMode)
                        {
                            await ProcessRecentFilesChange();
                        }
                        break;

                    default:
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 其他快速访问文件已更改: {fileName}");
                        break;
                }

                Console.WriteLine($"变更类型: {e.ChangeType}");
                Console.WriteLine($"完整路径: {e.FullPath}");
                Console.WriteLine("----------------------------------------");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 处理快速访问变更时出错: {ex.Message}");
            }
            finally
            {
                // 释放处理锁
                lock (_processingLock)
                {
                    _isProcessing = false;
                }
            }
        }

        private async Task ProcessRecentFilesChange()
        {
            try
            {
                // 保存旧列表
                var oldRecentFiles = new List<string>(_currentRecentFiles);

                // 获取新列表
                var newRecentFiles = await QuickAccessQuery.GetRecentFilesAsync();

                // 找出新增的文件
                var addedFiles = newRecentFiles.Except(oldRecentFiles).ToList();

                if (addedFiles.Count > 0)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 检测到 {addedFiles.Count} 个新增最近文件");

                    // 获取所有文件的访问时间
                    var fileAccessTimes = new Dictionary<string, DateTime>();
                    foreach (var file in newRecentFiles)
                    {
                        try
                        {
                            if (File.Exists(file))
                            {
                                fileAccessTimes[file] = File.GetLastAccessTime(file);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"获取文件访问时间失败: {file}, 错误: {ex.Message}");
                        }
                    }

                    // 找出访问时间最新的文件
                    DateTime mostRecentTime = DateTime.MinValue;
                    string mostRecentFile = null;

                    foreach (var file in fileAccessTimes.Keys)
                    {
                        if (fileAccessTimes[file] > mostRecentTime)
                        {
                            mostRecentTime = fileAccessTimes[file];
                            mostRecentFile = file;
                        }
                    }

                    foreach (var file in addedFiles)
                    {
                        Console.WriteLine($"检查新增文件: {file}");

                        // 检查是否是最近访问的文件
                        bool isRecentlyAccessed = false;

                        if (fileAccessTimes.ContainsKey(file))
                        {
                            // 获取文件的访问时间
                            DateTime accessTime = fileAccessTimes[file];

                            // 如果是最近访问的文件或接近最近访问时间（5秒内）
                            if (file == mostRecentFile || (mostRecentTime - accessTime).TotalSeconds <= 5)
                            {
                                isRecentlyAccessed = true;
                                Console.WriteLine($"文件是最近访问的: {file}, 访问时间: {accessTime}");
                            }
                            else
                            {
                                Console.WriteLine($"文件不是最近访问的: {file}, 访问时间: {accessTime}, 最新访问时间: {mostRecentTime}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"无法获取文件访问时间: {file}");
                        }

                        if (isRecentlyAccessed)
                        {
                            Console.WriteLine($"移除新增文件: {file}");

                            // 从快速访问中移除新增文件
                            await QuickAccessManager.RemoveItemAsync(file, QuickAccessItemType.File);
                        }
                        else
                        {
                            Console.WriteLine($"跳过非最近访问的文件: {file}");
                        }
                    }

                    // ShowNotification($"已自动移除 {addedFiles.Count} 个最近文件");
                }

                // 更新当前列表
                _currentRecentFiles = await QuickAccessQuery.GetRecentFilesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 处理最近文件变更时出错: {ex.Message}");
            }
        }

        private async Task ProcessFrequentFoldersChange()
        {
            try
            {
                // 保存旧列表
                var oldFrequentFolders = new List<string>(_currentFrequentFolders);

                // 获取新列表
                var newFrequentFolders = await QuickAccessQuery.GetFrequentFoldersAsync();

                // 找出新增的文件夹
                var addedFolders = newFrequentFolders.Except(oldFrequentFolders).ToList();

                if (addedFolders.Count > 0)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 检测到 {addedFolders.Count} 个新增常用文件夹");

                    // 获取所有文件夹的访问时间
                    var folderAccessTimes = new Dictionary<string, DateTime>();
                    foreach (var folder in newFrequentFolders)
                    {
                        try
                        {
                            if (Directory.Exists(folder))
                            {
                                folderAccessTimes[folder] = Directory.GetLastAccessTime(folder);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"获取文件夹访问时间失败: {folder}, 错误: {ex.Message}");
                        }
                    }

                    // 找出访问时间最新的文件夹
                    DateTime mostRecentTime = DateTime.MinValue;
                    string mostRecentFolder = null;

                    foreach (var folder in folderAccessTimes.Keys)
                    {
                        if (folderAccessTimes[folder] > mostRecentTime)
                        {
                            mostRecentTime = folderAccessTimes[folder];
                            mostRecentFolder = folder;
                        }
                    }

                    foreach (var folder in addedFolders)
                    {
                        Console.WriteLine($"检查新增文件夹: {folder}");

                        // 检查是否是最近访问的文件夹
                        bool isRecentlyAccessed = false;

                        if (folderAccessTimes.ContainsKey(folder))
                        {
                            // 获取文件夹的访问时间
                            DateTime accessTime = folderAccessTimes[folder];

                            // 如果是最近访问的文件夹或接近最近访问时间（5秒内）
                            if (folder == mostRecentFolder || (mostRecentTime - accessTime).TotalSeconds <= 5)
                            {
                                isRecentlyAccessed = true;
                                Console.WriteLine($"文件夹是最近访问的: {folder}, 访问时间: {accessTime}");
                            }
                            else
                            {
                                Console.WriteLine($"文件夹不是最近访问的: {folder}, 访问时间: {accessTime}, 最新访问时间: {mostRecentTime}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"无法获取文件夹访问时间: {folder}");
                        }

                        if (isRecentlyAccessed)
                        {
                            Console.WriteLine($"移除新增文件夹: {folder}");

                            // 从快速访问中移除新增文件夹
                            await QuickAccessManager.RemoveItemAsync(folder, QuickAccessItemType.Directory);
                        }
                        else
                        {
                            Console.WriteLine($"跳过非最近访问的文件夹: {folder}");
                        }
                    }

                    // 如果有文件夹被移除，显示通知
                    int removedCount = addedFolders.Count(folder =>
                        folderAccessTimes.ContainsKey(folder) &&
                        (folder == mostRecentFolder || (mostRecentTime - folderAccessTimes[folder]).TotalSeconds <= 5));

                    if (removedCount > 0)
                    {
                        Console.WriteLine($"已自动移除 {removedCount} 个常用文件夹");
                    }
                }

                // 更新当前列表
                _currentFrequentFolders = await QuickAccessQuery.GetFrequentFoldersAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 处理常用文件夹变更时出错: {ex.Message}");
            }
        }
    }
}
