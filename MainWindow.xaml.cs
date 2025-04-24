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
using Serilog;
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
        private IQuickAccessManager _quickAccessManager;

        public MainWindow()
        {
            InitializeLogger();
            _quickAccessManager = new QuickAccessManager();
            
            Task.Run(async () =>
            {
                try 
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        config = Config.Load();
                        if (!config.QueryFeasible || !config.HandleFeasible)
                        {
                            var (queryFeasible, handleFeasible) = await _quickAccessManager.CheckFeasibleAsync();
                            config.QueryFeasible = queryFeasible;
                            config.HandleFeasible = handleFeasible;
                            Log.Information("Feasibility check completed: Query={query}, Handle={handle}", queryFeasible, handleFeasible);
                            Config.Save(config);
                        }
                        UpdateAutoStart(config.AutoStart);

                        bool hasFunctionLimitation = !config.QueryFeasible || !config.HandleFeasible;
                        if (hasFunctionLimitation)
                        {
                            var limitedFeatures = new List<string>();
                            if (!config.QueryFeasible)
                                limitedFeatures.Add(Properties.Resources.Query);
                            if (!config.HandleFeasible)
                                limitedFeatures.Add(Properties.Resources.Handle);

                            var message = string.Format(
                                Properties.Resources.CoreFunctionLimitedError,
                                string.Join("/", limitedFeatures));

                            Log.Warning("Function limitations detected: {Features}", string.Join("/", limitedFeatures));

                            System.Windows.MessageBox.Show(
                                message,
                                Properties.Resources.Warning,
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }

                        InitializeLanguage();
                        InitializeComponent();
                        InitializeTrayIcon(hasFunctionLimitation);
                        
                        if (config.IncognitoMode && !hasFunctionLimitation)
                        {
                            StartWatching();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during initialization");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        System.Windows.MessageBox.Show(
                            "Application initialization failed",
                            Properties.Resources.Warning,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Application.Current.Shutdown();
                    });
                }
            });
        }

        private void InitializeLogger()
        {
            var logPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "logs", "CleanRecentMini.log");

            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug();

#if DEBUG
            logConfig = logConfig.WriteTo.Console();
#endif
            logConfig = logConfig.WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                retainedFileCountLimit: 10,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

            Log.Logger = logConfig.CreateLogger();
            Log.Information("CleanRecentMini Started");
        }

        private void InitializeLanguage()
        {
            var culture = new CultureInfo(config.Language);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Properties.Resources.Culture = culture;
        }

        private void InitializeTrayIcon(bool hasFunctionLimitation)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(config.Language);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(config.Language);
            Properties.Resources.Culture = new CultureInfo(config.Language);

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
                Checked = config.IncognitoMode && !hasFunctionLimitation,
                CheckOnClick = true,
                Enabled = !hasFunctionLimitation
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
            Properties.Resources.Culture = new CultureInfo(config.Language);
            
            var contextMenu = trayIcon.ContextMenuStrip;
            if (contextMenu != null)
            {
                if (contextMenu.Items[0] is ToolStripMenuItem autoStartItem)
                {
                    autoStartItem.Text = Properties.Resources.AutoStart;
                }

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
            Log.Information("CleanRecentMini Exited");
            Log.CloseAndFlush();
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
                    Log.Warning("Cannot monitor Quick Access: Directory does not exist - {Path}", automaticDestPath);
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

                Log.Information("Started monitoring Quick Access changes");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error starting monitoring");
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

                Log.Information("Stopped monitoring Quick Access changes");
            }
        }

        private async Task RefreshCurrentQuickAccessItems()
        {
            try
            {
                _currentRecentFiles = await _quickAccessManager.GetItemsAsync(QuickAccess.RecentFiles);
                _currentFrequentFolders = await _quickAccessManager.GetItemsAsync(QuickAccess.FrequentFolders);

                Log.Information("Quick Access items refreshed, Recent files count: {RecentFileCount}, Frequent folders count: {FrequentFolderCount}",
                    _currentRecentFiles.Count, _currentFrequentFolders.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing Quick Access items");
            }
        }

        private async void OnAutomaticDestinationsChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                lock (_processingLock)
                {
                    if (_isProcessing) return;
                    _isProcessing = true;
                }

                await Task.Delay(500);

                string fileName = Path.GetFileNameWithoutExtension(e.Name);
                if (string.IsNullOrEmpty(fileName)) return;

                fileName = fileName.Replace(".automaticDestinations", "");

                switch (fileName.ToLower())
                {
                    case FOLDERS_APPID:
                        Log.Information("Frequent folders changed");
                        if (config.IncognitoMode)
                        {
                            await ProcessFrequentFoldersChange();
                        }
                        break;

                    case RECENT_FILES_APPID:
                        Log.Information("Recent files changed");
                        if (config.IncognitoMode)
                        {
                            await ProcessRecentFilesChange();
                        }
                        break;

                    default:
                        Log.Debug("Other Quick Access file changed: {FileName}", fileName);
                        break;
                }

                Log.Debug("Change type: {ChangeType}, Full path: {FullPath}", e.ChangeType, e.FullPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing Quick Access changes");
            }
            finally
            {
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
                var oldRecentFiles = new List<string>(_currentRecentFiles);

                var newRecentFiles = await _quickAccessManager.GetItemsAsync(QuickAccess.RecentFiles);

                var addedFiles = newRecentFiles.Except(oldRecentFiles).ToList();

                if (addedFiles.Count > 0)
                {
                    Log.Information("Detected {Count} new recent files", addedFiles.Count);

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
                            Log.Warning(ex, "Failed to get file access time: {File}", file);
                        }
                    }

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
                        Log.Debug("Checking new file: {File}", file);

                        bool isRecentlyAccessed = false;

                        if (fileAccessTimes.ContainsKey(file))
                        {
                            DateTime accessTime = fileAccessTimes[file];

                            if (file == mostRecentFile || (mostRecentTime - accessTime).TotalSeconds <= 5)
                            {
                                isRecentlyAccessed = true;
                                Log.Debug("File is recently accessed: {File}, Access time: {AccessTime}", file, accessTime);
                            }
                            else
                            {
                                Log.Debug("File is not recently accessed: {File}, Access time: {AccessTime}, Most recent time: {MostRecentTime}",
                                    file, accessTime, mostRecentTime);
                            }
                        }
                        else
                        {
                            Log.Warning("Cannot get file access time: {File}", file);
                        }

                        if (isRecentlyAccessed)
                        {
                            Log.Information("Removing new file: {File}", file);

                            await _quickAccessManager.RemoveItemAsync(file, QuickAccess.RecentFiles);
                        }
                        else
                        {
                            Log.Debug("Skipping non-recent file: {File}", file);
                        }
                    }
                }

                _currentRecentFiles = await _quickAccessManager.GetItemsAsync(QuickAccess.RecentFiles);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing recent files changes");
            }
        }

        private async Task ProcessFrequentFoldersChange()
        {
            try
            {
                var oldFrequentFolders = new List<string>(_currentFrequentFolders);

                var newFrequentFolders = await _quickAccessManager.GetItemsAsync(QuickAccess.FrequentFolders);

                var addedFolders = newFrequentFolders.Except(oldFrequentFolders).ToList();

                if (addedFolders.Count > 0)
                {
                    Log.Information("Detected {Count} new frequent folders", addedFolders.Count);

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
                            Log.Warning(ex, "Failed to get folder access time: {Folder}", folder);
                        }
                    }

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
                        Log.Debug("Checking new folder: {Folder}", folder);

                        bool isRecentlyAccessed = false;

                        if (folderAccessTimes.ContainsKey(folder))
                        {
                            DateTime accessTime = folderAccessTimes[folder];

                            if (folder == mostRecentFolder || (mostRecentTime - accessTime).TotalSeconds <= 5)
                            {
                                isRecentlyAccessed = true;
                                Log.Debug("Folder is recently accessed: {Folder}, Access time: {AccessTime}", folder, accessTime);
                            }
                            else
                            {
                                Log.Debug("Folder is not recently accessed: {Folder}, Access time: {AccessTime}, Most recent time: {MostRecentTime}",
                                    folder, accessTime, mostRecentTime);
                            }
                        }
                        else
                        {
                            Log.Warning("Cannot get folder access time: {Folder}", folder);
                        }

                        if (isRecentlyAccessed)
                        {
                            Log.Information("Removing new folder: {Folder}", folder);

                            await _quickAccessManager.RemoveItemAsync(folder, QuickAccess.FrequentFolders);
                        }
                        else
                        {
                            Log.Debug("Skipping non-recent folder: {Folder}", folder);
                        }
                    }

                    int removedCount = addedFolders.Count(folder =>
                        folderAccessTimes.ContainsKey(folder) &&
                        (folder == mostRecentFolder || (mostRecentTime - folderAccessTimes[folder]).TotalSeconds <= 5));

                    if (removedCount > 0)
                    {
                        Log.Information("Automatically removed {Count} frequent folders", removedCount);
                    }
                }

                _currentFrequentFolders = await _quickAccessManager.GetItemsAsync(QuickAccess.FrequentFolders);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing frequent folders changes");
            }
        }
    }
}
