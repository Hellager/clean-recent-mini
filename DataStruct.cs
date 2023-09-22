using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace CleanRecentMini
{
    public struct CleanFilterItemLabel
    {
        public string key;
        public string name;
    }

    public struct CleanFilterItem
    {
        public string id;
        public byte group;
        public byte category;
        public bool state;
        public string keyword;
        public Int64 create_at;
        public Int64 update_at;
        public string author;
        public byte level;
        public List<CleanFilterItemLabel> labels;
    }

    public struct CleanedHistoryItem
    {
        public Int64 cleaned_at;
        public List<string> cleaned_files;
        public List<string> cleaned_folders;
    }

    public struct CleanedSnapshotItem
    {
        public Int64 cleaned_at;
        public List<string> quick_access;
        public List<string> cleaned_files;
        public List<string> cleaned_folders;
    }

    public struct AppConfig
    {
        public Int64 start_time;
        public Int64 stop_time;
        public Int64 run_time;
        public bool dark_mode;
        public bool auto_start;
        public bool close_to_tray;
        public ushort close_trigger_count;
        public bool ask_close_option;
        public ushort reask_close_count;
        public bool is_certified_core;
        public bool is_supported_system;
        public string language;
        public string version;
    }

    public struct CleanConfig
    {
        public bool is_monitor_running;
        public bool is_cron_running;
        public byte action_state;
        public byte clean_method;
        public byte clean_policy;
        public byte clean_category;
        public string cron_expression;
        public List<CleanFilterItem> filter_list;
        public Dictionary<string, Int64> next_runtime;
        public Dictionary<string, Int64> last_runtime;
        public List<string> command_names;
    }

    public struct CleanHistory
    {
        public Int64 start_time;
        public Int64 stop_time;
        public Int64 run_time;
        public ushort clean_snapshots_max;
        public List<CleanedSnapshotItem> clean_snapshots;
        public List<CleanedHistoryItem> cleaned_data;
    }

    public struct ExportFilterList
    {
        public List<CleanFilterItem> filter_list;
    }

    public class FilterlistTableItem : INotifyPropertyChanged
    {
        private string id { get; set; }
        private byte group { get; set; }
        private string group_desp { get; set; }
        private byte category { get; set; }
        private string category_desp { get; set; }
        private bool state { get; set; }
        private string state_desp { get; set; }
        private string keyword { get; set; }
        private Int64 create_at { get; set; }
        private Int64 update_at { get; set; }
        private string author { get; set; }
        private ushort level { get; set; }
        private List<CleanFilterItemLabel> labels { get; set; }
        private bool is_selected { get; set; }

        public string Id
        {
            get { return id; }
            set { this.id = value; OnPropertyChanged("ID"); }
        }

        public byte Group
        {
            get { return group; }
            set { this.group = value; OnPropertyChanged("Group"); }
        }

        public string GroupDesp
        {
            get { return group_desp; }
            set { this.group_desp = value; OnPropertyChanged("GroupDesp"); }
        }

        public byte Category
        {
            get { return category; }
            set { this.category = value; OnPropertyChanged("Category"); }
        }

        public string CategoryDesp
        {
            get { return category_desp; }
            set { this.category_desp = value; OnPropertyChanged("CategoryDesp"); }
        }

        public bool State
        {
            get { return state; }
            set { this.state = value; OnPropertyChanged("State"); }
        }

        public string StateDesp
        {
            get { return state_desp; }
            set { this.state_desp = value; OnPropertyChanged("StateDesp"); }
        }

        public string Keyword
        {
            get { return keyword; }
            set { this.keyword = value; OnPropertyChanged("Keyword"); }
        }

        public Int64 CreateAt
        {
            get { return create_at; }
            set { this.create_at = value; OnPropertyChanged("CreateAt"); }
        }

        public Int64 UpdateAt
        {
            get { return update_at; }
            set { this.update_at = value; OnPropertyChanged("UpdateAt"); }
        }

        public string Author
        {
            get { return author; }
            set { this.author = value; OnPropertyChanged("Author"); }
        }

        public ushort Level
        {
            get { return level; }
            set { this.level = value; OnPropertyChanged("Level"); }
        }

        public List<CleanFilterItemLabel> Labels
        {
            get { return labels; }
            set { this.labels = value; OnPropertyChanged("Labels"); }
        }

        public bool IsSelected
        {
            get { return is_selected; }
            set { this.is_selected = value; OnPropertyChanged("IsSelected"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(name));
                if (name != "IsSelected")
                {
                    this.update_at = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                }
            }
        }

        public FilterlistTableItem(CleanFilterItem data)
        {
            List<string> GroupDespEnumKeys = new List<string>() { "Blacklist", "Whitelist" };
            List<string> CategoryDespEnumKeys = new List<string>() { "QuickAccess", "RecentFiles", "FrequentFolders" };
            List<string> StateDespEnumKeys = new List<string>() { "Disabled", "Enabled" };

            this.Id = data.id;
            this.Group = data.group;
            this.Category = data.category;
            this.State = data.state;
            this.Keyword = data.keyword;
            this.CreateAt = data.create_at;
            this.UpdateAt = data.update_at;
            this.Author = data.author;
            this.Level = data.level;
            this.Labels = data.labels;
            this.IsSelected = false;

            ResourceDictionary resourceDictionary;
            resourceDictionary = System.Windows.Application.Current.Resources.MergedDictionaries[3];
            this.GroupDesp = resourceDictionary.Contains(GroupDespEnumKeys[data.group]) && resourceDictionary[GroupDespEnumKeys[data.group]].ToString() != null ?
                resourceDictionary[GroupDespEnumKeys[data.group]].ToString() : GroupDespEnumKeys[data.group];
            this.CategoryDesp = resourceDictionary.Contains(CategoryDespEnumKeys[data.category]) && resourceDictionary[CategoryDespEnumKeys[data.category]].ToString() != null ?
                resourceDictionary[CategoryDespEnumKeys[data.category]].ToString() : CategoryDespEnumKeys[data.category];
            this.StateDesp = resourceDictionary.Contains(StateDespEnumKeys[Convert.ToByte(data.state)]) && resourceDictionary[StateDespEnumKeys[Convert.ToByte(data.state)]].ToString() != null ?
                resourceDictionary[StateDespEnumKeys[Convert.ToByte(data.state)]].ToString() : StateDespEnumKeys[Convert.ToByte(data.state)];
        }
    }

    public class CleanTableItem : INotifyPropertyChanged
    {
        private string id { get; set; }
        private string path { get; set; }
        private bool is_selected { get; set; }

        public string Id
        {
            get { return id; }
            set { this.id = value; OnPropertyChanged("ID"); }
        }

        public string Path
        {
            get { return path; }
            set { this.path = value; OnPropertyChanged("Path"); }
        }

        public bool IsSelected
        {
            get { return is_selected; }
            set { this.is_selected = value; OnPropertyChanged("IsSelected"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public CleanTableItem(string id, string path)
        {
            this.Id = id;
            this.Path = path;
            this.is_selected = true;
        }
    }

    public class StatusTableItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

    public class StatusTableCleanedItem
    {
        public string Path { get; set; }
        public string Time { get; set; }
    }

    public class StatusTableCleanedTimesItem
    {
        public string Files { get; set; }
        public string Folders { get; set; }
        public string Time { get; set; }
    }
    internal class DataStruct
    {
    }
}
