using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
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

        private void StartWatching()
        {
            if (automaticDestinationsWatcher != null) return;

            string automaticDestPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Recent\AutomaticDestinations");

            // 监控 AutomaticDestinations 文件夹
            automaticDestinationsWatcher = new FileSystemWatcher
            {
                Path = automaticDestPath,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = "*.automaticDestinations-ms"
            };

            automaticDestinationsWatcher.Changed += OnAutomaticDestinationsChanged;
            //automaticDestinationsWatcher.Created += OnAutomaticDestinationsChanged;
            automaticDestinationsWatcher.EnableRaisingEvents = true;
        }

        private void StopWatching()
        {
            if (automaticDestinationsWatcher != null)
            {
                automaticDestinationsWatcher.EnableRaisingEvents = false;
                automaticDestinationsWatcher.Dispose();
                automaticDestinationsWatcher = null;
            }
        }

        private void OnAutomaticDestinationsChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 防止重复触发
                Thread.Sleep(100);

                string fileName = Path.GetFileNameWithoutExtension(e.Name);
                if (string.IsNullOrEmpty(fileName)) return;

                // 移除文件扩展名部分
                fileName = fileName.Replace(".automaticDestinations", "");

                switch (fileName.ToLower())
                {
                    case FOLDERS_APPID:
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Frequently used folders changed");
                        // 这里可以添加解析文件内容的代码来获取具体的文件夹路径
                        break;

                    case RECENT_FILES_APPID:
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Recent files changed");
                        // 这里可以添加解析文件内容的代码来获取具体的文件路径
                        break;

                    default:
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Other AutomaticDestinations file changed: {fileName}");
                        break;
                }

                Console.WriteLine($"Change type: {e.ChangeType}");
                Console.WriteLine($"Full path: {e.FullPath}");
                Console.WriteLine("----------------------------------------");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error processing AutomaticDestinations change: {ex.Message}");
            }
        }
    }
}
