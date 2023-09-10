using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using NLog;
using FramePFX.Themes;
using Newtonsoft.Json;
using QuickAccess;

namespace clean_recent_mini
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Notify Icon
        private readonly System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();

        // Quick Access Handler
        private QuickAccessHandler quickAccessHandler = new QuickAccessHandler();

        // Data Management
        public AppConfig appConfig;
        public CleanConfig cleanConfig;
        public CleanHistory cleanHistory;

        // Time Interval Trigger
        System.Threading.Timer cleanIntervalTimer = null;

        // Monitor Trigger
        FileSystemWatcher watcher = null;
        System.Threading.Timer watcherDebounceTimer = null;
        bool debounceWatcherValid = true;

        public MainWindow()
        {
            this.Build_NotifyIcon();

            InitializeComponent();

            this.Load_AppData();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Logger.Debug("Window Closing");
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            Logger.Debug("Window rendered content");

            this.Update_StatusMenu();
        }

        private string Get_Locale_From_Resource(string name)
        {
            ResourceDictionary resourceDictionary;
            resourceDictionary = System.Windows.Application.Current.Resources.MergedDictionaries[3];
            return resourceDictionary.Contains(name) ? resourceDictionary[name].ToString() : name;
        }

        /******** Notify Icon ********/
        private void On_WindowMenuItem_Clicked(object sender, EventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                this.Show();
            }

            this.Build_NotifyIcon_ContextMenu();
        }

        private void On_ExitMenuItem_Clicked(object sender, EventArgs e)
        {
            Logger.Debug("Exit Program");

            this.notifyIcon.Visible = false;

            Thread.Sleep(1000);

            this.notifyIcon.Dispose();

            System.Windows.Application.Current.Shutdown();

            return;
        }

        private void Build_NotifyIcon_ContextMenu()
        {
            ContextMenuStrip menuStrip = new ContextMenuStrip();
            this.notifyIcon.ContextMenuStrip = menuStrip;

            ToolStripMenuItem windowMenuItem = new ToolStripMenuItem();
            windowMenuItem.Text = this.Visibility == Visibility.Visible ? Get_Locale_From_Resource("Show") : Get_Locale_From_Resource("Hide");
            windowMenuItem.Click += new EventHandler(On_WindowMenuItem_Clicked);

            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem();
            exitMenuItem.Text = Get_Locale_From_Resource("Exit");
            exitMenuItem.Click += new EventHandler(On_ExitMenuItem_Clicked);

            menuStrip.Items.Add(windowMenuItem);
            menuStrip.Items.Add(exitMenuItem);

            this.notifyIcon.Text = Get_Locale_From_Resource("AppName");
        }

        private void Build_NotifyIcon()
        {
            System.Drawing.Icon _icon = new System.Drawing.Icon(System.Windows.Application.GetResourceStream(new Uri("/Assets/Icons/icon.ico", UriKind.Relative)).Stream);

            this.notifyIcon.Visible = true;
            this.notifyIcon.Icon = _icon;
            this.notifyIcon.Text = Get_Locale_From_Resource("AppName");
            this.notifyIcon.DoubleClick += On_WindowMenuItem_Clicked;

            Build_NotifyIcon_ContextMenu();
        }

        /******** Data Management ********/
        private string Get_Project_Dir(string qualifer = "dev", string organization = "", string application = "CleanRecent")
        {
            var systemPath = System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var projectPath = System.IO.Path.Combine(systemPath, application);
            if (!System.IO.Directory.Exists(projectPath))
            {
                System.IO.Directory.CreateDirectory(projectPath);
            }
            return projectPath;
        }

        private string Get_Project_Data_Dir(string dataPath = "data")
        {
            var projectPath = this.Get_Project_Dir();
            var projectDataPath = System.IO.Path.Combine(projectPath, dataPath);
            if (!System.IO.Directory.Exists(projectDataPath))
            {
                System.IO.Directory.CreateDirectory(projectDataPath);
            }
            return projectDataPath;
        }

        private string Get_Project_Config_Dir(string configPath = "config")
        {
            var projectPath = this.Get_Project_Dir();
            var projectConfigPath = System.IO.Path.Combine(projectPath, configPath);
            if (!System.IO.Directory.Exists(projectConfigPath))
            {
                System.IO.Directory.CreateDirectory(projectConfigPath);
            }
            return projectConfigPath;
        }

        private AppConfig Build_Default_AppConfig()
        {
            var config = new AppConfig();
            config.start_time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            config.stop_time = 0;
            config.run_time = 0;
            config.dark_mode = false;
            config.auto_start = false;
            config.close_to_tray = false;
            config.close_trigger_count = 0;
            config.ask_close_option = true;
            config.reask_close_count = 5;
            config.is_certified_core = false;
            config.is_supported_system = false;
            config.language = "en-US";
            config.version = "0.0.1";

            return config;
        }

        private CleanConfig Build_Default_CleanConfig()
        {
            var config = new CleanConfig();
            config.is_monitor_running = false;
            config.is_cron_running = false;
            config.action_state = 0;
            config.clean_method = 0;
            config.clean_policy = 0;
            config.clean_category = 0;
            config.cron_expression = "";
            config.filter_list = new List<CleanFilterItem>();
            config.next_runtime = new Dictionary<string, long>();
            config.last_runtime = new Dictionary<string, long>();
            config.command_names = new List<string>();

            return config;
        }

        private CleanHistory Build_Default_CleanHistory()
        {
            var history = new CleanHistory();
            history.start_time = 0;
            history.stop_time = 0;
            history.run_time = 0;
            history.clean_snapshots = new List<CleanedSnapshotItem>();
            history.clean_snapshots_max = 9;
            history.cleaned_data = new List<CleanedHistoryItem>();

            return history;
        }

        public void Build_DefaultData()
        {
            this.appConfig = this.Build_Default_AppConfig();
            this.cleanConfig = this.Build_Default_CleanConfig();
            this.cleanHistory = this.Build_Default_CleanHistory();
        }

        private void Load_AppConfig()
        {
            var project_config_dir = this.Get_Project_Config_Dir();
            var config_file = System.IO.Path.Combine(project_config_dir, "app_config_mini.json");
            if (!File.Exists(config_file))
            {
                // this.existMiniConfig = false;
                this.appConfig = this.Build_Default_AppConfig();
                File.Create(config_file).Close();
                this.Save_AppConfig();

                Logger.Debug("Init app config with default configuration");
            }
            else
            {
                // this.existMiniConfig = true;
                var config_content = File.ReadAllText(config_file);
                this.appConfig = JsonConvert.DeserializeObject<AppConfig>(config_content);

                Logger.Debug("Load app config from " + config_file);
            }
        }

        private bool Save_AppConfig()
        {
            var config_content = JsonConvert.SerializeObject(this.appConfig);
            var project_config_dir = this.Get_Project_Config_Dir();
            var config_file = System.IO.Path.Combine(project_config_dir, "app_config_mini.json");

            using (FileStream fs = File.Open(config_file, FileMode.Truncate, FileAccess.Write))
            {

                Byte[] config = new UTF8Encoding(true).GetBytes(config_content);
                fs.Write(config, 0, config.Length);
            }

            Logger.Debug("Save app config to " + config_file);
            return true;
        }

        private void Load_CleanConfig()
        {
            var project_config_dir = this.Get_Project_Config_Dir();
            var config_file = System.IO.Path.Combine(project_config_dir, "clean_config_mini.json");
            if (!File.Exists(config_file))
            {
                this.cleanConfig = this.Build_Default_CleanConfig();
                this.Save_CleanConfig();

                Logger.Debug("Init clean config with default configuration");
            }
            else
            {
                var config_content = File.ReadAllText(config_file);
                this.cleanConfig = JsonConvert.DeserializeObject<CleanConfig>(config_content);

                Logger.Debug("Load clean config from " + config_file);
            }
        }

        private bool Save_CleanConfig()
        {
            var config_content = JsonConvert.SerializeObject(this.cleanConfig);
            var project_config_dir = this.Get_Project_Config_Dir();
            var config_file = System.IO.Path.Combine(project_config_dir, "clean_config_mini.json");

            using (FileStream fs = File.Open(config_file, FileMode.OpenOrCreate, FileAccess.Write))
            {
                Byte[] config = new UTF8Encoding(true).GetBytes(config_content);
                fs.Write(config, 0, config.Length);
            }

            Logger.Debug("Save clean config to " + config_file);
            return true;
        }

        private void Load_CleanHistory()
        {
            var project_data_dir = this.Get_Project_Data_Dir();
            var history_file = System.IO.Path.Combine(project_data_dir, "clean_history_mini.json");
            if (!File.Exists(history_file))
            {
                this.cleanHistory = this.Build_Default_CleanHistory();
                this.Save_CleanHistory();

                Logger.Debug("Init clean history with default configuration");
            }
            else
            {
                var config_content = File.ReadAllText(history_file);
                this.cleanHistory = JsonConvert.DeserializeObject<CleanHistory>(config_content);

                Logger.Debug("Load clean history from " + history_file);
            }
        }

        private bool Save_CleanHistory()
        {
            var config_content = JsonConvert.SerializeObject(this.cleanHistory);
            var project_data_dir = this.Get_Project_Data_Dir();
            var history_file = System.IO.Path.Combine(project_data_dir, "clean_history_mini.json");

            using (FileStream fs = File.Open(history_file, FileMode.OpenOrCreate, FileAccess.Write))
            {
                Byte[] data = new UTF8Encoding(true).GetBytes(config_content);
                fs.Write(data, 0, data.Length);
            }

            Logger.Debug("Save clean history to " + history_file);
            return true;
        }

        private void Load_AppData()
        {
            this.Build_DefaultData();
            this.Load_AppConfig();
            this.Load_CleanConfig();
            this.Load_CleanHistory();
        }


        /******** Change Menu ********/
        private void On_MenuStatus_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Switch to status menu");

            this.Update_StatusMenu();

            this.ContainerController.SelectedIndex = 0;
        }

        private void On_MenuFilter_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Switch to filter menu");

            this.ContainerController.SelectedIndex = 1;
        }

        private void On_MenuConfig_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Switch to config menu");

            this.ContainerController.SelectedIndex = 2;
        }

        /************* About Status Menu ******************/
        private void Update_QuickAccess_Status()
        {
            Dictionary<string, string> quick_access = this.quickAccessHandler.GetQuickAccessDict();
            Dictionary<string, string> frequent_folders = this.quickAccessHandler.GetFrequentFolders();
            Dictionary<string, string> recent_files = this.quickAccessHandler.GetRecentFiles();

            this.ValueRecentFiles.Text = recent_files.Count.ToString();
            this.ValueQuickAccess.Text = quick_access.Count.ToString();
            this.ValueFrequentFolders.Text = frequent_folders.Count.ToString();
        }

        private void Update_StatusMenu()
        {
            this.Update_QuickAccess_Status();
        }

        /************* About Config Menu ******************/

        /********** About App Config ***************/
        private void On_Theme_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.appConfig.dark_mode = (idx != 0);

            // About theme change https://github.com/AngryCarrot789/WPFDarkTheme
            if (this.appConfig.dark_mode)
            {
                ThemesController.SetTheme(ThemeType.SoftDark);
            }
            else
            {
                ThemesController.SetTheme(ThemeType.LightTheme);
            }

            if (this.cleanConfig.action_state == 2)
            {
                if (this.appConfig.dark_mode == false)
                {
                    this.ManualCleanButtonLight.Visibility = Visibility.Visible;
                    this.ManualCleanButtonDark.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.ManualCleanButtonLight.Visibility = Visibility.Collapsed;
                    this.ManualCleanButtonDark.Visibility = Visibility.Visible;
                }
            }
            else
            {
                this.ManualCleanButtonLight.Visibility = Visibility.Collapsed;
                this.ManualCleanButtonDark.Visibility = Visibility.Collapsed;
            }

            Logger.Debug("Select change theme to: " + this.appConfig.dark_mode);
        }

        private void On_Language_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            List<string> default_languages = new List<string> { "en-US", "zh-CN", "zh-TW", "fr-FR", "ru-RU" };

            this.appConfig.language = default_languages[idx];

            System.Windows.Application.Current.Resources.MergedDictionaries[3] = new ResourceDictionary() { Source = new Uri($"Locale/{this.appConfig.language}.xaml", UriKind.Relative) };
        }

        private void On_Autostart_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.appConfig.auto_start = (idx != 0);

            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            string str = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            if (this.appConfig.auto_start)
            {
                key.SetValue("CleanRecentMini", str);

                Logger.Debug("Add clean-recent-mini to registry key");
            }
            else
            {
                if (key.GetValueNames().Contains("CleanRecentMini"))
                {
                    key.DeleteValue("CleanRecentMini");

                    Logger.Debug("Delete clean-recent-mini from registry key");
                }
            }
        }

        private void On_Closeoption_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.appConfig.close_to_tray = (idx != 0);

            this.appConfig.ask_close_option = true;
        }

        /********** About Clean Config ***************/
        private void On_ActionState_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.cleanConfig.action_state = (byte)idx;

            if (idx == 2)
            {
                if (this.appConfig.dark_mode == false)
                {
                    this.ManualCleanButtonLight.Visibility = Visibility.Visible;
                    this.ManualCleanButtonDark.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.ManualCleanButtonLight.Visibility = Visibility.Collapsed;
                    this.ManualCleanButtonDark.Visibility = Visibility.Visible;
                }
            }
            else
            {
                this.ManualCleanButtonLight.Visibility = Visibility.Collapsed;
                this.ManualCleanButtonDark.Visibility = Visibility.Collapsed;
            }

            // First stop every trigger when selection change
            if (this.watcher != null)
            {
                this.watcher = null;
                this.debounceWatcherValid = false;

                Logger.Debug("Stop file system watcher");
            }

            if (this.cleanIntervalTimer != null)
            {
                this.cleanIntervalTimer.Change(Timeout.Infinite, Timeout.Infinite);
                this.cleanIntervalTimer = null;

                Logger.Debug("Stop interval timer");
            }

            // Restart trigger
            if (this.cleanConfig.clean_method == 0)
            {
                // Start timer;
                if (this.cleanIntervalTimer != null)
                {
                    this.cleanIntervalTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    this.cleanIntervalTimer = null;

                    // this.interval_button.Content = "Start Interval";

                    Logger.Debug("Stop interval timer");
                }
                else
                {
                    Int32 interval = 30;
                    if (this.CleanIntervalSelector != null)
                    {
                        if (this.CleanIntervalSelector.SelectedIndex == 0)
                        {
                            interval = 1;
                        }
                        else if (this.CleanIntervalSelector.SelectedIndex == 1)
                        {
                            interval = 30;
                        }
                        else if (this.CleanIntervalSelector.SelectedIndex == 2)
                        {
                            interval = 60;
                        }
                    }

                    this.cleanIntervalTimer = new System.Threading.Timer(new TimerCallback(On_IntervalTimer_Triggered), null, Timeout.Infinite, Timeout.Infinite);
                    this.cleanIntervalTimer.Change(TimeSpan.FromMinutes(interval), TimeSpan.FromMinutes(interval)); // 第一项决定 timer 时间，第二项决定 timeout 后下一次 timer 时间,相同时则为固定时间间隔触发

                    // this.interval_button.Content = "Stop Interval";
                    DateTime next_runtime = DateTime.Now.AddMinutes(interval);

                    Logger.Debug(string.Format("Start interval timer, every {0} minutes, next runtime: {1}", interval, next_runtime.ToString()));

                    if (this.LabelCleanInterval != null)
                    {
                        this.LabelCleanInterval.ToolTip = next_runtime.ToString();
                    }

                }
            }
            else if (this.cleanConfig.clean_method == 1)
            {
                // Start watcher;
                this.debounceWatcherValid = true;

                this.watcher = new FileSystemWatcher();
                // https://learn.microsoft.com/en-us/dotnet/api/system.environment.getfolderpath?view=net-7.0
                watcher.Path = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                watcher.NotifyFilter = NotifyFilters.LastWrite; // 仅当向快速访问中添加新项时出现
                watcher.Filter = "*.*";
                watcher.Changed += On_Recent_QuickAccess_Changed;
                watcher.EnableRaisingEvents = true;

                Logger.Debug("Watcher starts watching path: " + watcher.Path);

                if (this.LabelCleanInterval != null)
                {
                    this.LabelCleanInterval.ToolTip = "Last runtime";
                }
            }

            App.Current.Dispatcher.Invoke((Action)(() =>
            {
                if (this.CommandNames != null)
                {
                    this.CleanMethodSelector.IsEnabled = !Convert.ToBoolean(idx);
                    this.CleanIntervalSelector.IsEnabled = !Convert.ToBoolean(idx);
                    this.CleanPolicySelector.IsEnabled = !Convert.ToBoolean(idx);
                    this.CleanCategorySelector.IsEnabled = !Convert.ToBoolean(idx);
                    this.CommandNames.IsEnabled = !Convert.ToBoolean(idx);
                }
            }));
        }

        private void On_CleanMethod_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.cleanConfig.clean_method = (byte)idx;
        }

        private void On_CleanPolicy_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.cleanConfig.clean_policy = (byte)idx;
        }

        private void On_CleanCategory_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.cleanConfig.clean_category = (byte)idx;
        }

        private void On_CleanInterval_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            switch (idx)
            {
                case 0:
                    {
                        this.cleanConfig.cron_expression = "0 0/10 * * * ?";
                    }
                    break;

                case 1:
                    {
                        this.cleanConfig.cron_expression = "0 0/30 * * * ?";
                    }
                    break;


                case 2:
                    {
                        this.cleanConfig.cron_expression = "0 0/60 * * * ?";
                    }
                    break;

                default:
                    {
                        this.cleanConfig.cron_expression = "0 0/30 * * * ?";
                    }
                    break;
            }

            Logger.Debug("Update clean interval to: " + this.cleanConfig.cron_expression);
        }

        /********** About Clean Quick Access ***************/
        private void On_WatcherDebouncerTimer_Trigger(object state)
        {
            this.debounceWatcherValid = true;

            Logger.Debug("Water timer triggered");
        }

        private void On_Recent_QuickAccess_Changed(object source, FileSystemEventArgs eventArgs)
        {
            if (source == null) return;

            Logger.Debug("Detect change in recent");
            if (!this.debounceWatcherValid) return;
            this.debounceWatcherValid = false;

            if (this.watcherDebounceTimer == null)
            {
                this.watcherDebounceTimer = new System.Threading.Timer(new TimerCallback(On_WatcherDebouncerTimer_Trigger), null, Timeout.Infinite, Timeout.Infinite);
            }

            this.watcherDebounceTimer.Change(10000, Timeout.Infinite);

            string path = eventArgs.FullPath.ToString();
            Logger.Debug("Event path: " + path);

            if (this.cleanConfig.action_state == 2)
            {
                Logger.Debug("Clean require manual confirm");
                // https://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this
                App.Current.Dispatcher.Invoke((Action)(() =>
                {
                    this.On_ManualClean_Button_Clicked(null, null); // manual confirm clean
                }));

            }
            else
            {
                Logger.Debug("Silent clean recent");
                this.Handle_Clean_QuickAccess();
            }
        }

        private void On_IntervalTimer_Triggered(object state)
        {
            if (this.cleanConfig.action_state == 2)
            {
                Logger.Debug("Clean require manual confirm");
                // https://stackoverflow.com/questions/2329978/the-calling-thread-must-be-sta-because-many-ui-components-require-this
                App.Current.Dispatcher.Invoke((Action)(() =>
                {
                    this.On_ManualClean_Button_Clicked(null, null); // manual confirm clean
                }));
            }
            else
            {
                Logger.Debug("Silent clean recent");
                this.Handle_Clean_QuickAccess();
            }
            Logger.Debug("Interval timer timout");
        }

        private void On_ManualClean_Button_Clicked(object sender, RoutedEventArgs e)
        {
            Logger.Debug("manual clean");

            // category
            List<string> clean_source = new List<string>();
            if (this.cleanConfig.clean_category == 1)
            {
                clean_source = this.quickAccessHandler.GetFrequentFolders().Keys.ToList();
            }
            else if (this.cleanConfig.clean_category == 2)
            {
                clean_source = this.quickAccessHandler.GetRecentFiles().Keys.ToList();
            }
            else
            {
                clean_source = this.quickAccessHandler.GetQuickAccessDict().Keys.ToList();
            }

            // policy
            List<string> clean_items = new List<string>();
            if (this.cleanConfig.clean_policy == 2)
            {
                clean_items = clean_source;
            }
            else
            {
                for (Int32 i = 0; i < clean_source.Count; i++)
                {
                    for (Int32 j = 0; j < this.cleanConfig.filter_list.Count; j++)
                    {
                        if (clean_source[i].Contains(this.cleanConfig.filter_list[j].keyword))
                        {
                            if (this.cleanConfig.filter_list[i].group == this.cleanConfig.clean_policy &&
                                this.cleanConfig.filter_list[i].state)
                            {
                                clean_items.Add(clean_source[i]);
                            }
                        }
                    }
                }
            }

            if (clean_items.Count > 0)
            {
                if (App.Current.MainWindow.WindowState != 0)
                {
                    App.Current.MainWindow.Show();
                    App.Current.MainWindow.Activate();

                    Logger.Debug("Try reshow mainwindow");
                }

                // Show Clean Confirm Dialog
            }
            else
            {
                Logger.Debug("No detect items to clean");
            }
        }

        private void Handle_Clean_QuickAccess()
        {
            // category
            List<string> cleanSource = new List<string>();
            if (this.cleanConfig.clean_category == 1)
            {
                cleanSource = this.quickAccessHandler.GetFrequentFolders().Keys.ToList();
            }
            else if (this.cleanConfig.clean_category == 2)
            {
                cleanSource = this.quickAccessHandler.GetRecentFiles().Keys.ToList();
            }
            else
            {
                cleanSource = this.quickAccessHandler.GetQuickAccessDict().Keys.ToList();
            }

            // policy
            List<string> cleanList = new List<string>();
            if (this.cleanConfig.clean_policy == 2)
            {
                cleanList = cleanSource;
            }
            else
            {
                for (Int32 i = 0; i < cleanSource.Count; i++)
                {
                    for (Int32 j = 0; j < this.cleanConfig.filter_list.Count; j++)
                    {
                        if (cleanSource[i].Contains(this.cleanConfig.filter_list[j].keyword))
                        {
                            if (this.cleanConfig.filter_list[i].group == this.cleanConfig.clean_policy &&
                                this.cleanConfig.filter_list[i].state)
                            {
                                cleanList.Add(cleanSource[i]);

                                Logger.Debug(string.Format("Cur keyword: {0}, cur source: {1}", this.cleanConfig.filter_list[j].keyword, cleanSource[i]));
                            }
                        }
                    }
                }
            }

            // command
            Int32 before_clean_count = this.quickAccessHandler.GetQuickAccessDict().Count;
            Logger.Debug(string.Format("Detect {0} items to remove from quick access", cleanList.Count));
            if (this.cleanConfig.command_names.Count > 0)
            {
                Logger.Debug("Add system command names");
                foreach (string item in this.cleanConfig.command_names)
                {
                    this.quickAccessHandler.AddquickAccessCommandName(item);
                }
            }
            // this.quickAccessHandler.RemoveFromQuickAccess(clean_items);

            Int32 failed_clean = 0;
            List<string> cur_quick_access = this.quickAccessHandler.GetQuickAccessDict().Keys.ToList();
            foreach (string item in cleanList)
            {
                if (cur_quick_access.Contains(item))
                {
                    failed_clean++;
                    Logger.Debug(string.Format("Failed to remove path: {0} from quick access", item));
                }
            }

            if (failed_clean > 0)
            {
                // Show some error message
            }
            else
            {
                Logger.Debug(string.Format("Clean recent success! Remove {0} items from quick access", cleanList.Count));
            }
        }
    }
}
