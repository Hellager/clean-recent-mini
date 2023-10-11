using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Diagnostics;
using NLog;
using FramePFX.Themes;
using Newtonsoft.Json;
using QuickAccess;


namespace CleanRecentMini
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Instance variable <c>notifyIcon</c> <br /> 
        /// App notifyIcon.
        /// </summary>
        private readonly System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();

        /// <summary>
        /// Instance variable <c>quickAccessHandler</c> <br /> 
        /// Handler for windows quick access.
        /// </summary>
        private QuickAccessHandler quickAccessHandler = new QuickAccessHandler();

        /// <summary>
        /// Instance variable <c>appConfig</c> <br /> 
        /// </summary>
        public AppConfig appConfig;

        /// <summary>
        /// Instance variable <c>cleanConfig</c> <br /> 
        /// </summary>
        public CleanConfig cleanConfig;

        /// <summary>
        /// Instance variable <c>cleanHistory</c> <br /> 
        /// </summary>
        public CleanHistory cleanHistory;

        /// <summary>
        /// Instance variable <c>cleanIntervalTimer</c> <br /> 
        /// Timer for clean in period.
        /// </summary>
        System.Threading.Timer cleanIntervalTimer = null;

        // Monitor Trigger
        /// <summary>
        /// Instance variable <c>watcher</c> <br /> 
        /// Monitor for windows quick access folder.
        /// </summary>
        FileSystemWatcher watcher = null;

        /// <summary>
        /// Instance variable <c>watcherDebounceTimer</c> <br /> 
        /// Debouncer timer for watcher.
        /// </summary>
        System.Threading.Timer watcherDebounceTimer = null;

        /// <summary>
        /// Instance variable <c>debounceWatcherValid</c> <br /> 
        /// </summary>
        bool debounceWatcherValid = true;

        /// <summary>
        /// Instance variable <c>FilterlistTableData</c> <br /> 
        /// Data for FilterlistTable
        /// </summary>
        public ObservableCollection<FilterlistTableItem> FilterlistTableData = new ObservableCollection<FilterlistTableItem>();

        /// <summary>
        /// Instance variable <c>closeDialogOkOrCancel</c> <br /> 
        /// False for close dialog canceled, true for close dialog confirmed
        /// </summary>
        public bool closeDialogOkOrCancel = false;

        /// <summary>
        /// Instance variable <c>closeRememberOption</c> <br /> 
        /// False for no remember option, true for remember.
        /// </summary>
        public bool closeRememberOption = false;

        /// <summary>
        /// Instance variable <c>closeOption</c> <br /> 
        /// 0 for exit program, 1 for minimize to tray.
        /// </summary>
        public byte closeOption = 0;

        /// <summary>
        /// Instance variable <c>dataSaveTimer</c> <br /> 
        /// Timer for save data in period.
        /// </summary>
        System.Threading.Timer dataSaveTimer = null;

        public MainWindow()
        {
            this.Build_NotifyIcon();

            InitializeComponent();

            this.Load_AppData();
            this.Start_Data_Save_Timer();
        }

        /// <summary>
        /// Handle window close event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Cancel event args.</param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Logger.Debug("Window Closing");

            this.appConfig.close_trigger_count += 1;
            if (this.appConfig.ask_close_option == true || 
                this.appConfig.close_trigger_count >= this.appConfig.reask_close_count)
            {
                CloseDialog closeDialog = new CloseDialog();
                closeDialog.ShowDialog();

                if (this.closeDialogOkOrCancel == false)
                {
                    if (this.closeRememberOption == false)
                    {
                        this.appConfig.ask_close_option = true;
                    }

                    e.Cancel = true;
                    return;
                }

                if (this.closeDialogOkOrCancel == true && this.closeRememberOption == true)
                {
                    // Handle close event with dialog result
                    this.appConfig.ask_close_option = false;
                    Logger.Debug("Remember choice, use new app config data to handle close event");
                    if (this.appConfig.close_option == false)
                    {
                        Logger.Debug("Quit program");
                        this.Save_AppConfig();
                        this.Save_CleanConfig();
                        this.Save_CleanHistory();
                        e.Cancel = false;
                    }
                    else
                    {
                        Logger.Debug("Minize mainwindow to system tray");
                        e.Cancel = true;
                        this.Hide();
                    }
                }
                else if (this.closeDialogOkOrCancel == true && this.closeRememberOption == false)
                {
                    // Handle close event without remember
                    this.appConfig.ask_close_option = true;
                    Logger.Debug("No remember choice, use confirm res to handle close event");
                    if (this.closeOption == 0)
                    {
                        Logger.Debug("Quit program");
                        this.Save_AppConfig();
                        this.Save_CleanConfig();
                        this.Save_CleanHistory();
                        e.Cancel = false;
                    }
                    else
                    {
                        Logger.Debug("Minize mainwindow to system tray");
                        e.Cancel = true;
                        this.Hide();
                    }
                }
            }
            else
            {
                // With default config
                if (this.appConfig.close_option == false)
                {
                    Logger.Debug("Quit program");
                    this.Save_AppConfig();
                    this.Save_CleanConfig();
                    this.Save_CleanHistory();
                    e.Cancel = false;
                }
                else
                {
                    Logger.Debug("Minize mainwindow to system tray");
                    e.Cancel = true;
                    this.Hide();
                }
            }
        }

        /// <summary>
        /// Handle window activate event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Event args.</param>
        private void Window_Activated(object sender, EventArgs e)
        {
            Logger.Debug("window get activated");
            this.Update_StatusMenu();
        }

        /// <summary>
        /// Handle window content rendered event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Event args.</param>
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            Logger.Debug("Window rendered content");

            this.Update_StatusMenu();
        }

        /// <summary>
        /// Get localed string from locale resource
        /// </summary>
        /// <returns>
        /// Localed string if exists, else name itself.
        /// </returns>
        /// (<paramref name="name"/>).
        /// <param><c>name</c> Locale name.</param>
        private string Get_Locale_From_Resource(string name)
        {
            ResourceDictionary resourceDictionary;
            resourceDictionary = System.Windows.Application.Current.Resources.MergedDictionaries[3];
            return resourceDictionary.Contains(name) ? resourceDictionary[name].ToString() : name;
        }

        /******** Notify Icon ********/
        /// <summary>
        /// Handle notifyIcon menu item click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
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

        /// <summary>
        /// Handle notifyIcon exit menu item click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_ExitMenuItem_Clicked(object sender, EventArgs e)
        {
            Logger.Debug("Exit Program");

            this.notifyIcon.Visible = false;

            Thread.Sleep(1000);

            this.notifyIcon.Dispose();

            System.Windows.Application.Current.Shutdown();

            return;
        }

        /// <summary>
        /// Build notifyIcon menu.
        /// </summary>
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

        /// <summary>
        /// Build notifyIcon .
        /// </summary>
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
        /// <summary>
        /// Get project directory
        /// </summary>
        /// <returns>
        /// Project directory in string.
        /// </returns>
        /// (<paramref name="qualifer"/>, <paramref name="organization"/>, <paramref name="application"/>).
        /// <param><c>qualifer</c> Qualifer.</param>
        /// <param><c>organization</c> Organization.</param>
        /// /// <param><c>application</c> Application.</param>
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

        /// <summary>
        /// Get project data directory
        /// </summary>
        /// <returns>
        /// Project data directory in string.
        /// </returns>
        /// (<paramref name="dataPath"/>).
        /// <param><c>dataPath</c> Path for projcet data.</param>
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

        /// <summary>
        /// Get project config directory
        /// </summary>
        /// <returns>
        /// Project config directory in string.
        /// </returns>
        /// (<paramref name="configPath"/>).
        /// <param><c>dataPath</c> Path for projcet config.</param>
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

        /// <summary>
        /// Build default app config.
        /// </summary>
        /// <returns>
        /// Default appc onfig data.
        /// </returns>
        private AppConfig Build_Default_AppConfig()
        {
            var config = new AppConfig();
            config.start_time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            config.stop_time = 0;
            config.run_time = 0;
            config.dark_mode = false;
            config.auto_start = false;
            config.close_option = false;
            config.close_trigger_count = 0;
            config.ask_close_option = true;
            config.reask_close_count = 5;
            config.is_certified_core = false;
            config.is_supported_system = false;
            config.language = "en-US";
            config.version = "0.0.1";

            return config;
        }

        /// <summary>
        /// Build default clean config.
        /// </summary>
        /// <returns>
        /// Default clean config data.
        /// </returns>
        private CleanConfig Build_Default_CleanConfig()
        {
            var config = new CleanConfig();
            config.is_monitor_running = false;
            config.is_cron_running = false;
            config.clean_state = 0;
            config.clean_trigger = 0;
            config.clean_policy = 0;
            config.clean_category = 0;
            config.cron_expression = "";
            config.filter_list = new List<CleanFilterItem>();
            config.next_runtime = new Dictionary<string, long>();
            config.last_runtime = new Dictionary<string, long>();
            config.menu_names = new List<string>();

            return config;
        }

        /// <summary>
        /// Build default clean history.
        /// </summary>
        /// <returns>
        /// Default clean history data.
        /// </returns>
        private CleanHistory Build_Default_CleanHistory()
        {
            var history = new CleanHistory();
            history.clean_snapshots = new List<CleanedSnapshotItem>();
            history.clean_snapshots_max = 9;
            history.cleaned_data = new List<CleanQuickAccessItem>();

            return history;
        }

        /// <summary>
        /// Build default app data about app config, clean config and clean history.
        /// </summary>
        public void Build_DefaultData()
        {
            this.appConfig = this.Build_Default_AppConfig();
            this.cleanConfig = this.Build_Default_CleanConfig();
            this.cleanHistory = this.Build_Default_CleanHistory();
        }

        /// <summary>
        /// Load app config data.
        /// </summary>
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

        /// <summary>
        /// Save app config data.
        /// </summary>
        /// <returns>
        /// False for failed save app config, true for success.
        /// </returns>
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

        /// <summary>
        /// Load clean config data.
        /// </summary>
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

        /// <summary>
        /// Save clean config data.
        /// </summary>
        /// <returns>
        /// False for failed save clean config, true for success.
        /// </returns>
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

        /// <summary>
        /// Load clean history data.
        /// </summary>
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

        /// <summary>
        /// Save clean history data.
        /// </summary>
         /// <returns>
        /// False for failed save clean history, true for success.
        /// </returns>
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

        /// <summary>
        /// Load app data about app config, clean config and clean history, if not exists, use default data.
        /// </summary>
        private void Load_AppData()
        {
            this.Build_DefaultData();
            this.Load_AppConfig();
            this.Load_CleanConfig();
            this.Load_CleanHistory();
        }

        /// <summary>
        /// Handle dataSaveTimer trigger event
        /// </summary>
        /// (<paramref name="state"/>).
        /// <param><c>state</c> Timer state.</param>
        private void On_DataSaveTimer_Triggered(object state)
        {
            this.Save_AppConfig();
            this.Save_CleanConfig();
            this.Save_CleanHistory();

            Logger.Debug("Save app data");
        }

        /// <summary>
        /// Start dataSaveTimer to save data in period.
        /// </summary>
        private void Start_Data_Save_Timer()
        {
            this.dataSaveTimer = new System.Threading.Timer(new TimerCallback(On_DataSaveTimer_Triggered), null, Timeout.Infinite, Timeout.Infinite);
            this.dataSaveTimer.Change(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)); // Save data every 10 minutes

            Logger.Debug("Start data save timer");
        }


        /******** Change Menu ********/
        /// <summary>
        /// Handle MenuStatus click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_MenuStatus_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Switch to status menu");

            this.Update_StatusMenu();

            this.ContainerController.SelectedIndex = 0;
        }

        /// <summary>
        /// Handle MenuFilter click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_MenuFilter_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Switch to filter menu");

            this.Refresh_Filterlist_Table();

            this.ContainerController.SelectedIndex = 1;
        }

        /// <summary>
        /// Handle MenuConfig click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_MenuConfig_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Switch to config menu");

            this.ContainerController.SelectedIndex = 2;
        }

        /************* About Status Menu ******************/
        /// <summary>
        /// Get current recent files in quick access.
        /// </summary>
        /// <returns>
        /// Dictionary about recent files, key for path, value for name.
        /// </returns>
        private Dictionary<string, string> Get_Cur_Recent_Files()
        {
            return this.quickAccessHandler.GetRecentFilesDict();
        }

        /// <summary>
        /// Get current frequent folders in quick access.
        /// </summary>
        /// <returns>
        /// Dictionary about frequent folders, key for path, value for name.
        /// </returns>
        private Dictionary<string, string> Get_Cur_Frequent_Folders()
        {
            return this.quickAccessHandler.GetFrequentFoldersDict();
        }

        /// <summary>
        /// Get all items in quick access.
        /// </summary>
        /// <returns>
        /// Dictionary about quick access, key for path, value for name.
        /// </returns>
        private Dictionary<string, string> Get_Cur_Quick_Access()
        {
            return this.quickAccessHandler.GetQuickAccessDict();
        }

        /// <summary>
        /// Get current items in clean group list.
        /// </summary>
        /// <returns>
        /// List for items in clean group list
        /// </returns>
        /// (<paramref name="group"/>).
        /// <param><c>group</c> 0 for in cleanlist, 1 for in blacklist, 2 for in whitelist.</param>
        private List<CleanQuickAccessItem> Get_Cur_CleanQuickAccessItems(byte group)
        {
            List<CleanQuickAccessItem> res = new List<CleanQuickAccessItem>();

            Dictionary<string, string> clean_source = new Dictionary<string, string>();
            if (group == 0)
            {
                if (this.cleanConfig.clean_category == 1)
                {
                    clean_source = this.Get_Cur_Frequent_Folders();
                }
                else if (this.cleanConfig.clean_category == 2)
                {
                    clean_source = this.Get_Cur_Recent_Files();
                }
                else
                {
                    clean_source = this.quickAccessHandler.GetQuickAccessDict();
                }
            }
            else
            {
                clean_source = this.quickAccessHandler.GetQuickAccessDict();
            }


            string[] clean_source_key_arr = clean_source.Keys.ToArray();
            foreach (KeyValuePair<string, string> kv in clean_source)
            {
                bool isTarget = false;
                CleanQuickAccessItem item = new CleanQuickAccessItem();
                item.name = kv.Value;
                item.path = kv.Key;
                item.item_type = 0; // Set when clean
                item.cleaned_policy = 0; // Set when clean
                item.cleaned_at = 0; // Set when clean

                // Check Item Keyword
                item.keywords = new List<string>();
                Parallel.ForEach(this.cleanConfig.filter_list, filter =>
                {
                    if (group > 0)
                    {
                        // Check quick access item in blacklist or whitelist
                        if(item.path.Contains(filter.keyword) && (group == filter.group) && (filter.state == true))
                        {
                            isTarget = true;
                            item.keywords.Add(filter.keyword);
                        }
                    } 
                    else
                    {
                        if (this.cleanConfig.clean_policy == 0)
                        {
                            // Empty current quick access
                            isTarget = true;
                        }
                        else if (this.cleanConfig.clean_policy == 1)
                        {
                            // Clean blacklist
                            if(item.path.Contains(filter.keyword) && (group == filter.group || group == 0) && (filter.state == true))
                            {
                                isTarget = true;
                                item.keywords.Add(filter.keyword);
                            }
                        }
                        else if (this.cleanConfig.clean_policy == 2)
                        {
                            // Keep whitelist, get items in whitelist first, revert later
                            if (item.path.Contains(filter.keyword) && (filter.group == 2) && (filter.state == true))
                            {
                                isTarget = true;
                            }
                        }
                    }
                });

                if (isTarget)
                {
                    res.Add(item);
                }
            }

            if (group == 0 && this.cleanConfig.clean_policy == 2)
            {
                // When keep whitelist, revert items to get the actual to clean items
                List<string> curWhitelistPaths = new List<string>();
                for (int i = 0; i < res.Count; i++)
                {
                    curWhitelistPaths.Add(res[i].path);
                }

                res.Clear();
                foreach(KeyValuePair<string, string> kv in clean_source)
                {
                    CleanQuickAccessItem item = new CleanQuickAccessItem();
                    item.name = kv.Value;
                    item.path = kv.Key;
                    item.item_type = 0; // Set when clean
                    item.cleaned_policy = 0; // Set when clean
                    item.cleaned_at = 0; // Set when clean
                    item.keywords = new List<string>();

                    if (!curWhitelistPaths.Contains(kv.Key))
                    {
                        res.Add(item);
                    }
                }
            }

            return res;
        }

        /// <summary>
        /// Show status detail in dialog.
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void Show_Status(object sender, EventArgs e)
        {
            List<string> menuStatus = new List<string>() { 
                "StatusRecentFiles", "StatusQuickAccess", "StatusFrequentFolders",
                "StatusInBlacklist", "StatusInCleanlist", "StatusInWhitelist",
                "StatusCleanedFiles", "StatusCleanTimes", "StatusCleanedFolders"
            };
            List<Dictionary<string, string>> normalData = new List<Dictionary<string, string>>()
            {
                this.Get_Cur_Recent_Files(),
                this.Get_Cur_Quick_Access(),
                this.Get_Cur_Frequent_Folders()
            };

            string cur_status = (sender as System.Windows.Controls.Button).Name;
            byte cur_status_idx = Convert.ToByte(menuStatus.IndexOf(cur_status));

            Logger.Debug("Cur name " + cur_status + " idx: " + menuStatus.IndexOf(cur_status));

            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(cur_status_idx);
            if (cur_status_idx < 3)
            {
                dialog.SetShowNoramlData(normalData[cur_status_idx]);
            }
            else if (cur_status_idx < 6)
            {
                dialog.SetShowFilterData(this.Get_Cur_CleanQuickAccessItems(Convert.ToByte(cur_status_idx - 3)));
            }
            else
            {
                dialog.SetShowCleanedData(this.cleanHistory.cleaned_data);
            }
            
            dialog.Show();
        }

        /// <summary>
        /// Update quick access status.
        /// </summary>
        private void Update_QuickAccess_Status()
        {
            this.ValueRecentFiles.Text = this.quickAccessHandler.GetRecentFilesList().Count.ToString();
            this.ValueQuickAccess.Text = this.quickAccessHandler.GetQuickAccessList().Count.ToString();
            this.ValueFrequentFolders.Text = this.quickAccessHandler.GetFrequentFoldersList().Count.ToString();
        }

        /// <summary>
        /// Update filter status.
        /// </summary>
        private void Update_Filter_Status()
        {
            this.ValueInBlacklist.Text = this.Get_Cur_CleanQuickAccessItems(1).Count.ToString();
            this.ValueInCleanlist.Text = this.Get_Cur_CleanQuickAccessItems(0).Count.ToString() + " / " + this.Get_Cur_Quick_Access().Count.ToString();
            this.ValueInWhitelist.Text = this.Get_Cur_CleanQuickAccessItems(2).Count.ToString();
        }

        /// <summary>
        /// Update history status.
        /// </summary>
        private void Update_History_Status()
        {
            this.ValueCleanedFiles.Text = this.cleanHistory.cleaned_files_cnt.ToString();
            this.ValueCleanTimes.Text = this.cleanHistory.clean_times.ToString();
            this.ValueCleanedFolders.Text = this.cleanHistory.cleaned_folders_cnt.ToString();
        }

        /// <summary>
        /// Update status menu.
        /// </summary>
        private void Update_StatusMenu()
        {
            this.Update_QuickAccess_Status();
            this.Update_Filter_Status();
            this.Update_History_Status();
        }

        /************* About Filter Menu ******************/
        /// <summary>
        /// Refresh FilterlistTable
        /// </summary>
        public void Refresh_Filterlist_Table()
        {
            this.FilterlistTableData.Clear();

            for (Int32 i = 0; i < this.cleanConfig.filter_list.Count; i++)
            {
                this.FilterlistTableData.Add(new FilterlistTableItem(this.cleanConfig.filter_list.ElementAt(i)));
            }

            this.FilterlistTable.DataContext = this.FilterlistTableData;

            this.FilterlistTable.Items.Refresh();
        }

        /// <summary>
        /// Handle FilterlistTable checkbox click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_Filter_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var CurCheckBox = sender as System.Windows.Controls.CheckBox;
            var CurCheckBoxUID = CurCheckBox.Uid;

            if (CurCheckBoxUID == "-1")
            {
                foreach (var item in this.FilterlistTableData)
                {
                    item.IsSelected = true;
                }

                CurCheckBox.Uid = "-2";
                return;
            }
            else if (CurCheckBoxUID == "-2")
            {
                foreach (var item in this.FilterlistTableData)
                {
                    item.IsSelected = false;
                }

                CurCheckBox.Uid = "-1";
                return;
            }

            foreach (var item in this.FilterlistTableData)
            {
                if (item.Id == CurCheckBoxUID)
                {
                    item.IsSelected = !item.IsSelected;
                    break;
                }
            }
        }

        /// <summary>
        /// Handle filter search button click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_Filter_SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.FilterInputText.Text == "")
            {
                this.Refresh_Filterlist_Table();
            }
            else
            {
                this.FilterlistTableData.Clear();

                for (Int32 i = 0; i < this.cleanConfig.filter_list.Count; i++)
                {
                    if (this.cleanConfig.filter_list[i].keyword.Contains(this.FilterInputText.Text))
                    {
                        this.FilterlistTableData.Add(new FilterlistTableItem(this.cleanConfig.filter_list.ElementAt(i)));
                    }
                }

                this.FilterlistTable.DataContext = this.FilterlistTableData;

                this.FilterlistTable.Items.Refresh();
            }
        }

        /// <summary>
        /// Handle filter append button click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_Filter_AppendButton_Click(object sender, RoutedEventArgs e)
        {
            FilterDialog dialog = new FilterDialog();
            dialog.SetDialogMode(0);
            dialog.ShowDialog();

            this.Refresh_Filterlist_Table();
        }

        /// <summary>
        /// Handle filter delete button click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_Filter_DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> toDeleteID = new List<string>();

            for (Int32 i = 0; i < this.FilterlistTableData.Count; i++)
            {
                if (this.FilterlistTableData[i].IsSelected)
                {
                    toDeleteID.Add(this.FilterlistTableData[i].Id);
                }
            }

            for (int i = this.cleanConfig.filter_list.Count - 1; i >= 0; i--)
            {
                if (toDeleteID.Contains(this.cleanConfig.filter_list[i].id))
                    this.cleanConfig.filter_list.RemoveAt(i);
            }

            this.Refresh_Filterlist_Table();
        }

        /// <summary>
        /// Handle filter import button click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_Filter_ImportButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = false;
            dialog.Title = "Please select file";
            dialog.Filter = "JSON file(*.json)|*.json";

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string file = dialog.FileName;

                Logger.Debug("Choose file: " + file);

                try
                {
                    var import_content = System.IO.File.ReadAllText(file);
                    var import_clean_config = JsonConvert.DeserializeObject<CleanConfig>(import_content);

                    Logger.Debug("import clean config from " + file);

                    FilterDialog tranferDialog = new FilterDialog();
                    tranferDialog.SetDialogMode(2);
                    tranferDialog.SetTransferData(import_clean_config.filter_list);
                    tranferDialog.ShowDialog();
                }
                catch (Exception err)
                {
                    Logger.Debug("invalid config file: " + err.Message);

                    return;
                }
            }
        }

        /// <summary>
        /// Handle filter export button click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_Filter_ExportButton_Click(object sender, RoutedEventArgs e)
        {
            FilterDialog tranferDialog = new FilterDialog();
            tranferDialog.SetDialogMode(3);
            tranferDialog.SetTransferData(this.cleanConfig.filter_list);
            tranferDialog.ShowDialog();
        }

        /// <summary>
        /// Handle filter edit button click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_Filter_EditButton_Click(object sender, RoutedEventArgs e)
        {
            FilterlistTableItem edit_target = default;
            for (var vis = sender as Visual; vis != null; vis = VisualTreeHelper.GetParent(vis) as Visual)
            {
                if (vis is DataGridRow)
                {
                    var row = (DataGridRow)vis;

                    edit_target = row.Item as FilterlistTableItem;
                    break;
                }
            }

            if (edit_target == null) return;
            FilterDialog dialog = new FilterDialog();
            dialog.SetEditItemData(edit_target);
            dialog.ShowDialog();

            this.Refresh_Filterlist_Table();
        }

        /************* About Config Menu ******************/

        /********** About App Config ***************/

        /// <summary>
        /// Handle app config theme selection change event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Select changed event args.</param>
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

            if (this.cleanConfig.clean_state == 2)
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

        /// <summary>
        /// Handle app config language selection change event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Select changed event args.</param>
        private void On_Language_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            List<string> default_languages = new List<string> { "en-US", "zh-CN", "zh-TW", "fr-FR", "ru-RU" };

            this.appConfig.language = default_languages[idx];

            System.Windows.Application.Current.Resources.MergedDictionaries[3] = new ResourceDictionary() { Source = new Uri($"Locale/{this.appConfig.language}.xaml", UriKind.Relative) };
        }

        /// <summary>
        /// Handle app config auto start selection change event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Select changed event args.</param>
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

        /// <summary>
        /// Handle app config close option selection change event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Select changed event args.</param>
        private void On_Closeoption_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.appConfig.close_option = (idx != 0);

            this.appConfig.ask_close_option = true;
        }

        /// <summary>
        /// Handle version click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_Version_Link_Click(object sender, RoutedEventArgs e)
        {
           try
            {
                System.Diagnostics.Process.Start("https://clean-recent.hellagur.com");
            }
            catch
            {
                Logger.Error("Failed to open web page");
            }
        }

        /********** About Clean Config ***************/
        /// <summary>
        /// Handle clean config action state selection change event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Select changed event args.</param>
        private void On_ActionState_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.cleanConfig.clean_state = (byte)idx;

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

            if (idx == 1)
            {
                // Restart trigger
                if (this.cleanConfig.clean_trigger == 0)
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
                                interval = 10;
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
                        this.cleanIntervalTimer.Change(TimeSpan.FromMinutes(interval), TimeSpan.FromMinutes(interval));

                        // this.interval_button.Content = "Stop Interval";
                        DateTime next_runtime = DateTime.Now.AddMinutes(interval);

                        Logger.Debug(string.Format("Start interval timer, every {0} minutes, next runtime: {1}", interval, next_runtime.ToString()));

                        if (this.LabelCleanInterval != null)
                        {
                            this.LabelCleanInterval.ToolTip = next_runtime.ToString();
                        }

                    }
                }
                else if (this.cleanConfig.clean_trigger == 1)
                {
                    // Start watcher;
                    this.debounceWatcherValid = true;

                    this.watcher = new FileSystemWatcher();
                    // https://learn.microsoft.com/en-us/dotnet/api/system.environment.getfolderpath?view=net-7.0
                    watcher.Path = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                    watcher.NotifyFilter = NotifyFilters.LastWrite; // trigger only new item added to quick access
                    watcher.Filter = "*.*";
                    watcher.Changed += On_Recent_QuickAccess_Changed;
                    watcher.EnableRaisingEvents = true;

                    Logger.Debug("Watcher starts watching path: " + watcher.Path);

                    if (this.LabelCleanInterval != null)
                    {
                        this.LabelCleanInterval.ToolTip = "Last runtime";
                    }
                }
            }


            App.Current.Dispatcher.Invoke((Action)(() =>
            {
                if (this.MenuNames != null)
                {
                    if (idx < 2)
                    {
                        this.CleanMethodSelector.IsEnabled = !Convert.ToBoolean(idx);
                        this.CleanIntervalSelector.IsEnabled = !Convert.ToBoolean(idx);
                        this.CleanPolicySelector.IsEnabled = !Convert.ToBoolean(idx);
                        this.CleanCategorySelector.IsEnabled = !Convert.ToBoolean(idx);
                        this.MenuNames.IsEnabled = !Convert.ToBoolean(idx);
                    }
                    else if (idx == 2)
                    {
                        this.CleanMethodSelector.IsEnabled = !Convert.ToBoolean(idx);
                        this.CleanIntervalSelector.IsEnabled = !Convert.ToBoolean(idx);
                        this.CleanPolicySelector.IsEnabled = true;
                        this.CleanCategorySelector.IsEnabled = true;
                        this.MenuNames.IsEnabled = true;
                    }

                    this.CleanMethodSelector.IsEnabled = !Convert.ToBoolean(idx);
                    this.CleanIntervalSelector.IsEnabled = !Convert.ToBoolean(idx);
                    this.CleanPolicySelector.IsEnabled = !Convert.ToBoolean(idx);
                    this.CleanCategorySelector.IsEnabled = !Convert.ToBoolean(idx);
                    this.MenuNames.IsEnabled = !Convert.ToBoolean(idx);
                }
            }));
        }

        /// <summary>
        /// Handle clean config clean method selection change event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Select changed event args.</param>
        private void On_CleanMethod_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.cleanConfig.clean_trigger = (byte)idx;
        }

        /// <summary>
        /// Handle clean config clean policy selection change event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Select changed event args.</param>
        private void On_CleanPolicy_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.cleanConfig.clean_policy = (byte)idx;
        }

        /// <summary>
        /// Handle clean config clean category selection change event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Select changed event args.</param>
        private void On_CleanCategory_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = (sender as System.Windows.Controls.ComboBox).SelectedIndex;

            this.cleanConfig.clean_category = (byte)idx;
        }

        /// <summary>
        /// Handle clean config clean interval selection change event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Select changed event args.</param>
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
        /// <summary>
        /// Handle watcher debouncer timer trigger event
        /// </summary>
        /// (<paramref name="state"/>).
        /// <param><c>state</c> Timer state.</param>
        private void On_WatcherDebouncerTimer_Trigger(object state)
        {
            this.debounceWatcherValid = true;

            Logger.Debug("Water timer triggered");
        }

        /// <summary>
        /// Handle change event in windows quick access
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> File system event args.</param>
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

            if (this.cleanConfig.clean_state == 2)
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

        /// <summary>
        /// Handle clean interval timer trigger event
        /// </summary>
        /// (<paramref name="state"/>).
        /// <param><c>state</c> Timer state.</param>
        private void On_IntervalTimer_Triggered(object state)
        {
            if (this.cleanConfig.clean_state == 2)
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

        /// <summary>
        /// Handle manual clean button click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_ManualClean_Button_Clicked(object sender, RoutedEventArgs e)
        {
            Logger.Debug("manual clean");

            // category
            List<string> clean_source = new List<string>();
            if (this.cleanConfig.clean_category == 1)
            {
                clean_source = this.quickAccessHandler.GetFrequentFoldersList();
            }
            else if (this.cleanConfig.clean_category == 2)
            {
                clean_source = this.quickAccessHandler.GetRecentFilesList();
            }
            else
            {
                clean_source = this.quickAccessHandler.GetQuickAccessList();
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

        /// <summary>
        /// Handle clean quick access event
        /// </summary>
        private void Handle_Clean_QuickAccess()
        {
            List<CleanQuickAccessItem> to_clean_list = this.Get_Cur_CleanQuickAccessItems(0);
            List<CleanQuickAccessItem> after_clean_list = new List<CleanQuickAccessItem>();
            List<string> to_clean_paths = new List<string>();

            if (to_clean_list.Count == 0) return;

            List<string> before_quick_access = this.quickAccessHandler.GetQuickAccessList();
            List<string> before_frequent_folders = this.quickAccessHandler.GetFrequentFoldersList();
            List<string> before_recent_files = this.quickAccessHandler.GetRecentFilesList();

            // Menu names
            Logger.Debug(string.Format("Detect {0} items to remove from quick access", to_clean_list.Count));
            if (this.cleanConfig.menu_names.Count > 0)
            {
                Logger.Debug("Add system menu names");
                foreach (string item in this.cleanConfig.menu_names)
                {
                    this.quickAccessHandler.AddQuickAccessMenuName(item);
                }
            }

            foreach(CleanQuickAccessItem item in to_clean_list)
            {
                to_clean_paths.Add(item.path);
            }
            Int64 clean_time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            this.quickAccessHandler.RemoveFromQuickAccess(to_clean_paths);

            List<string> after_quick_access = this.quickAccessHandler.GetQuickAccessList();

            for(int i = to_clean_list.Count - 1; i >= 0; i--)
            {
                if (after_quick_access.Contains(to_clean_list[i].path))
                {
                    // Failed remove item from quick access
                    Logger.Warn("Failed to remove " + to_clean_list[i].path + " from quick access");
                    continue;
                }

                CleanQuickAccessItem item = to_clean_list[i];
                item.cleaned_at = clean_time;
                item.cleaned_policy = this.cleanConfig.clean_policy;

                bool inQuickAccess = before_quick_access.Contains(item.path);
                bool inFrequentFolders = before_frequent_folders.Contains(item.path);
                bool inRecentFiles = before_recent_files.Contains(item.path);

                bool isUnSpecific = inQuickAccess && !inRecentFiles && !inFrequentFolders;
                bool isFrequentFolders = inFrequentFolders && !isUnSpecific && !inRecentFiles;
                bool isRecentFiles = inRecentFiles && !isUnSpecific && !inFrequentFolders;

                if (isUnSpecific) item.item_type = 0;
                if (isFrequentFolders) item.item_type = 1;
                if (isRecentFiles) item.item_type = 2;

                after_clean_list.Add(item);
            }

            // update snapshot
            var cnt_limit = this.cleanHistory.clean_snapshots_max;

            while (this.cleanHistory.clean_snapshots.Count > cnt_limit)
            {
                this.cleanHistory.clean_snapshots.RemoveAt(0);
            }

            this.cleanHistory.clean_snapshots.Add(new CleanedSnapshotItem()
            {
                cleaned_at = clean_time,
                quick_access = before_quick_access,
                cleaned_folders = before_frequent_folders,
                cleaned_files = before_recent_files,
            });

            Int64 cur_cleaned_files_cnt = 0;
            Int64 cur_cleaned_folders_cnt = 0;
            foreach(CleanQuickAccessItem item in after_clean_list)
            {
                this.cleanHistory.cleaned_data.Add(item);

                if(item.item_type == 1)
                {
                    cur_cleaned_folders_cnt += 1;
                } else if (item.item_type == 2)
                {
                    cur_cleaned_files_cnt += 1;
                }
            }

            this.cleanHistory.clean_times += 1;
            this.cleanHistory.cleaned_files_cnt += cur_cleaned_files_cnt;
            this.cleanHistory.cleaned_folders_cnt += cur_cleaned_folders_cnt;

            this.Save_CleanHistory();

            Logger.Debug(string.Format("Clean recent finished! Remove {0} items from quick access", after_clean_list.Count));
        }
    }
}
