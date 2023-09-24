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

using System.Diagnostics;

namespace CleanRecentMini
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

        // Filter
        public ObservableCollection<FilterlistTableItem> FilterlistTableData = new ObservableCollection<FilterlistTableItem>();

        // Close Dialog
        public bool closeDialogOkOrCancel = false; // false for cancel, true for confirm
        public bool closeRememberOption = false;
        public byte closeOption = 0; // 0 for exit, 1 for minimize

        public MainWindow()
        {
            this.Build_NotifyIcon();

            InitializeComponent();

            this.Load_AppData();
        }

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
                    if (this.appConfig.close_to_tray == false)
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
                if (this.appConfig.close_to_tray == false)
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

        private void Window_Activated(object sender, EventArgs e)
        {
            Logger.Debug("window get activated");
            this.Update_StatusMenu();
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

            this.Refresh_Filterlist_Table();

            this.ContainerController.SelectedIndex = 1;
        }

        private void On_MenuConfig_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Switch to config menu");

            this.ContainerController.SelectedIndex = 2;
        }

        /************* About Status Menu ******************/
        private Dictionary<string, string> Get_Cur_Recent_Files()
        {
            return this.quickAccessHandler.GetRecentFilesDict();
        }

        private Dictionary<string, string> Get_Cur_Frequent_Folders()
        {
            return this.quickAccessHandler.GetFrequentFoldersDict();
        }

        private Dictionary<string, string> Get_Cur_Quick_Access()
        {
            return this.quickAccessHandler.GetQuickAccessDict();
        }

        private Dictionary<string, string> Get_Cur_In_Blacklist()
        {
            Dictionary<string, string> InBlacklist = new Dictionary<string, string>();

            Dictionary<string, string> clean_source = new Dictionary<string, string>();
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
                clean_source = this.Get_Cur_Recent_Files();
            }

            foreach (var item in clean_source)
            {
                for (Int32 j = 0; j < this.cleanConfig.filter_list.Count; j++)
                {
                    if (item.Key.Contains(this.cleanConfig.filter_list[j].keyword))
                    {
                        if (this.cleanConfig.filter_list[j].group == 0 &&
                            this.cleanConfig.filter_list[j].state == true)
                        {
                            InBlacklist.Add(item.Key, item.Value);
                        }
                    }
                }
            }

            return InBlacklist;
        }

        private Dictionary<string, string> Get_Cur_In_Whitelist()
        {
            Dictionary<string, string> InWhitelist = new Dictionary<string, string>();

            Dictionary<string, string> clean_source = new Dictionary<string, string>();
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
                clean_source = this.Get_Cur_Recent_Files();
            }

            foreach (var item in clean_source)
            {
                for (Int32 j = 0; j < this.cleanConfig.filter_list.Count; j++)
                {
                    if (item.Key.Contains(this.cleanConfig.filter_list[j].keyword))
                    {
                        if (this.cleanConfig.filter_list[j].group == 1 &&
                            this.cleanConfig.filter_list[j].state == true)
                        {
                            InWhitelist.Add(item.Key, item.Value);
                        }
                    }
                }
            }

            return InWhitelist;
        }

        private Dictionary<string, string> Get_Cur_In_Cleanlist()
        {
            Dictionary<string, string> InCleanlist = new Dictionary<string, string>();

            Dictionary<string, string> clean_source = new Dictionary<string, string>();
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
                clean_source = this.Get_Cur_Recent_Files();
            }

            if (this.cleanConfig.clean_policy == 2)
            {
                InCleanlist = clean_source;
            }
            else
            {
                foreach (var item in clean_source)
                {
                    for (Int32 j = 0; j < this.cleanConfig.filter_list.Count; j++)
                    {
                        if (item.Key.Contains(this.cleanConfig.filter_list[j].keyword))
                        {
                            if (this.cleanConfig.filter_list[j].group == this.cleanConfig.clean_policy &&
                                this.cleanConfig.filter_list[j].state == true)
                            {
                                InCleanlist.Add(item.Key, item.Value);
                            }
                        }
                    }
                }
            }


            return InCleanlist;
        }

        private List<CleanQuickAccessItem> Get_Cur_CleanQuickAccessItems(byte group)
        {
            // group 0 for in cleanlist, 1 for in blacklist, 2 for in whitelist
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
            for(int i = 0; i < clean_source_key_arr.Length; i++)
            {
                bool isTarget = false;
                CleanQuickAccessItem item = new CleanQuickAccessItem();
                item.Name = clean_source[clean_source_key_arr[i]];
                item.Path = clean_source_key_arr[i];
                item.Type = 0; // Set when clean
                item.CleanedGroup = 0; // Set when clean
                item.CleanedTime = 0; // Set when clean

                // Check Item Type
                //Stopwatch sw = new Stopwatch();
                //sw.Start();
                //bool inRecentFiles = this.Get_Cur_Recent_Files().ContainsKey(item.Path);
                //bool inFrequentFolders = this.Get_Cur_Frequent_Folders().ContainsKey(item.Path);
                //bool inQuickAccess = this.Get_Cur_Quick_Access().ContainsKey(item.Path);
                //sw.Stop();
                //Logger.Debug("Cur idx " + i + ", time: " + sw.ElapsedMilliseconds);
                //sw.Reset();

                //bool isUnSpecific = inQuickAccess && !inRecentFiles && !inFrequentFolders;
                //bool isFrequentFolders = inFrequentFolders && !isUnSpecific && !inRecentFiles;
                //bool isRecentFiles = inRecentFiles && !isUnSpecific && !inFrequentFolders;

                //if (isUnSpecific) item.Type = 0;
                //if (isFrequentFolders) item.Type = 1;
                //if (isRecentFiles) item.Type = 2;

                // Check Item Keyword
                item.Keywords = new List<string>();
                Parallel.ForEach(this.cleanConfig.filter_list, filter =>
                {
                    if (item.Path.Contains(filter.keyword))
                    {
                        if (group == 0)
                        {
                            if (this.cleanConfig.clean_policy == 2 || (filter.group == this.cleanConfig.clean_policy &&
                                filter.state == true))
                            {
                                isTarget = true;
                                item.Keywords.Add(filter.keyword);
                            }
                        }
                        else
                        {
                            isTarget = true;
                            item.Keywords.Add(filter.keyword);
                        }
                    }
                });

                if (isTarget)
                {
                    res.Add(item);
                }
            }

            return res;
        }

        private void Show_Recent_Files(object sender, EventArgs e)
        {
            var res = this.Get_Cur_Recent_Files();

            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(0);
            dialog.SetShowNoramlData(res);
            dialog.Show();
        }

        private void Show_Frequent_Folders(object sender, EventArgs e)
        {
            var res = this.Get_Cur_Frequent_Folders();

            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(2);
            dialog.SetShowNoramlData(res);
            dialog.Show();
        }

        private void Show_Quick_Access(object sender, EventArgs e)
        {
            var res = this.Get_Cur_Quick_Access();

            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(1);
            dialog.SetShowNoramlData(res);
            dialog.Show();
        }

        private void Show_In_Blacklist(object sender, EventArgs e)
        {
            var res = this.Get_Cur_CleanQuickAccessItems(1);

            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(3);
            dialog.SetShowFilterData(res);
            dialog.Show();
        }

        private void Show_In_Cleanlist(object sender, EventArgs e)
        {
            var res = this.Get_Cur_CleanQuickAccessItems(0);

            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(4);
            dialog.SetShowFilterData(res);
            dialog.Show();
        }

        private void Show_In_Whitelist(object sender, EventArgs e)
        {
            var res = this.Get_Cur_CleanQuickAccessItems(2);

            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(5);
            dialog.SetShowFilterData(res);
            dialog.Show();
        }

        private void Show_Cleaned_Files(object sender, EventArgs e)
        {
            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(6);
            dialog.SetShowCleanedData(this.cleanHistory.cleaned_data);
            dialog.Show();
        }

        private void Show_Clean_Times(object sender, EventArgs e)
        {
            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(7);
            dialog.SetShowCleanedData(this.cleanHistory.cleaned_data);
            dialog.Show();
        }

        private void Show_Cleaned_Folders(object sender, EventArgs e)
        {
            StatusDialog dialog = new StatusDialog();
            dialog.SetShowMode(8);
            dialog.SetShowCleanedData(this.cleanHistory.cleaned_data);
            dialog.Show();
        }

        private void Update_QuickAccess_Status()
        {
            this.ValueRecentFiles.Text = this.quickAccessHandler.GetRecentFilesList().Count.ToString();
            this.ValueQuickAccess.Text = this.quickAccessHandler.GetQuickAccessList().Count.ToString();
            this.ValueFrequentFolders.Text = this.quickAccessHandler.GetFrequentFoldersList().Count.ToString();
        }

        private void Update_Filter_Status()
        {
            this.ValueInBlacklist.Text = this.Get_Cur_In_Blacklist().Count.ToString();
            this.ValueInCleanlist.Text = this.Get_Cur_In_Cleanlist().Count.ToString() + " / " + this.Get_Cur_Quick_Access().Count.ToString();
            this.ValueInWhitelist.Text = this.Get_Cur_In_Whitelist().Count.ToString();
        }

        private void Update_History_Status()
        {
            Int32 cleaned_times = 0, cleaned_files = 0, cleaned_folders = 0;
            cleaned_times = this.cleanHistory.cleaned_data.Count;
            foreach (CleanedHistoryItem history in this.cleanHistory.cleaned_data)
            {
                cleaned_files += history.cleaned_files.Count;
                cleaned_folders += history.cleaned_folders.Count;
            }

            this.ValueCleanedFiles.Text = cleaned_files.ToString();
            this.ValueCleanTimes.Text = cleaned_times.ToString();
            this.ValueCleanedFolders.Text = cleaned_folders.ToString();
        }

        private void Update_StatusMenu()
        {
            this.Update_QuickAccess_Status();
            this.Update_Filter_Status();
            this.Update_History_Status();
        }

        /************* About Filter Menu ******************/
        private void Refresh_Filterlist_Table()
        {
            this.FilterlistTableData.Clear();

            for (Int32 i = 0; i < this.cleanConfig.filter_list.Count; i++)
            {
                this.FilterlistTableData.Add(new FilterlistTableItem(this.cleanConfig.filter_list.ElementAt(i)));
            }

            this.FilterlistTable.DataContext = this.FilterlistTableData;

            this.FilterlistTable.Items.Refresh();
        }

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

        private void On_Filter_AppendButton_Click(object sender, RoutedEventArgs e)
        {
            FilterDialog dialog = new FilterDialog();
            dialog.SetDialogMode(0);
            dialog.ShowDialog();

            this.Refresh_Filterlist_Table();
        }

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

        private void On_Filter_ExportButton_Click(object sender, RoutedEventArgs e)
        {
            FilterDialog tranferDialog = new FilterDialog();
            tranferDialog.SetDialogMode(3);
            tranferDialog.SetTransferData(this.cleanConfig.filter_list);
            tranferDialog.ShowDialog();
        }

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

            if (idx == 1)
            {
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

        private void Handle_Clean_QuickAccess()
        {
            // category
            List<string> cleanSource = new List<string>();
            if (this.cleanConfig.clean_category == 1)
            {
                cleanSource = this.quickAccessHandler.GetFrequentFoldersList();
            }
            else if (this.cleanConfig.clean_category == 2)
            {
                cleanSource = this.quickAccessHandler.GetRecentFilesList();
            }
            else
            {
                cleanSource = this.quickAccessHandler.GetQuickAccessList();
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
                    this.quickAccessHandler.AddQuickAccessMenuName(item);
                }
            }

            var clean_time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            this.cleanHistory.run_time = clean_time;

            var cleaned_files = new List<string>();
            var cleaned_folders = new List<string>();
            var failed_clean_files = new List<string>();
            var failed_clean_floders = new List<string>();
            var quick_access_snapshot = this.quickAccessHandler.GetQuickAccessList();
            var before_recent_files = this.quickAccessHandler.GetRecentFilesList();
            var before_frequent_folders = this.quickAccessHandler.GetFrequentFoldersList();
            
            this.quickAccessHandler.RemoveFromQuickAccess(cleanList);

            var after_recent_files = this.quickAccessHandler.GetRecentFilesList();
            var after_frequent_folders = this.quickAccessHandler.GetFrequentFoldersList();

            for (int i = 0; i < cleanList.Count; i++)
            {
                if (before_recent_files.Contains(cleanList[i]))
                {
                    if (!after_recent_files.Contains(cleanList[i]))
                    {
                        cleaned_files.Add(cleanList[i]);
                    }
                    else
                    {
                        failed_clean_files.Add(cleanList[i]);
                    }
                }
                else if (before_frequent_folders.Contains(cleanList[i]))
                {
                    if (!after_frequent_folders.Contains(cleanList[i]))
                    {
                        cleaned_folders.Add(cleanList[i]);
                    }
                    else
                    {
                        failed_clean_floders.Add(cleanList[i]);
                    }
                }
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
                quick_access = quick_access_snapshot,
                cleaned_files = cleaned_files,
                cleaned_folders = cleaned_folders
            });

            this.cleanHistory.cleaned_data.Add(new CleanedHistoryItem()
            {
                cleaned_at = clean_time,
                cleaned_files = cleaned_files,
                cleaned_folders = cleaned_folders
            });

            this.Save_CleanHistory();

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
