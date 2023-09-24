using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NLog;

namespace CleanRecentMini
{
    /// <summary>
    /// Interaction logic for StatusDialog.xaml
    /// </summary>
    public partial class StatusDialog : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private byte mode = 0;

        public StatusDialog()
        {
            InitializeComponent();
        }

        public void SetShowMode(byte data)
        {
            List<string> windowTitleKeys = new List<string>() { "RecentFiles", "QuickAccess", "FrequentFolders", "InBlacklist", "InCleanlist", "InWhitelist", "CleandFiles", "CleanTimes", "CleanedFolders" };

            var mainWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow;
            ResourceDictionary resourceDictionary;
            resourceDictionary = System.Windows.Application.Current.Resources.MergedDictionaries[3];

            this.StatusController.SelectedIndex = data / 3;

            if (data == 7)
            {
                this.StatusController.SelectedIndex = 3;
            }

            this.mode = data;

            this.Title = resourceDictionary.Contains(windowTitleKeys[data]) ? resourceDictionary[windowTitleKeys[data]].ToString() : windowTitleKeys[data];
        }

        public void SetShowNoramlData(Dictionary<string, string> data)
        {
            if (this.StatusController.SelectedIndex == 0)
            {
                List<StatusTableNormalItem> table_data = new List<StatusTableNormalItem>();
                foreach (var item in data)
                {
                    table_data.Add(new StatusTableNormalItem() { Name = item.Value, Path = item.Key });
                }

                this.NormalGird.ItemsSource = table_data;
            }
        }

        public void SetShowFilterData(List<CleanQuickAccessItem> data)
        {
            if (this.StatusController.SelectedIndex == 1)
            {
                List<StatusTableFilterItem> table_data = new List<StatusTableFilterItem>();
                foreach (var item in data)
                {
                    table_data.Add(new StatusTableFilterItem()
                    {
                        Name = item.Name,
                        Path = item.Path,
                        Keywords = String.Join(", ", item.Keywords.ToArray())
                    });
                }

                this.FilterGird.ItemsSource = table_data;
            }
        }

        public void SetShowCleanedData(List<CleanedHistoryItem> data)
        {
            if (this.StatusController.SelectedIndex == 2)
            {
                List<StatusTableCleanedItem> table_data = new List<StatusTableCleanedItem>();
                foreach (var item in data)
                {
                    DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                    dateTime = dateTime.AddSeconds(item.cleaned_at).ToLocalTime();
                    if (this.mode == 6)
                    {
                        var res = item.cleaned_files;
                        foreach (var file in res)
                        {
                            table_data.Add(new StatusTableCleanedItem() { Path = file, Time = dateTime.ToString("yyyy/MM/dd HH:mm:ss") });
                        }

                    }
                    else if (this.mode == 8)
                    {
                        var res = item.cleaned_folders;
                        foreach (var folder in res)
                        {
                            table_data.Add(new StatusTableCleanedItem() { Path = folder, Time = dateTime.ToString("yyyy/MM/dd HH:mm:ss") });
                        }
                    }

                }

                this.CleanedGird.ItemsSource = table_data;
            }
            else if (this.StatusController.SelectedIndex == 2)
            {
                List<StatusTableCleanedTimesItem> table_data = new List<StatusTableCleanedTimesItem>();
                foreach (var item in data)
                {
                    DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                    dateTime = dateTime.AddSeconds(item.cleaned_at).ToLocalTime();

                    table_data.Add(new StatusTableCleanedTimesItem() { Files = item.cleaned_files.Count.ToString(), Folders = item.cleaned_folders.Count.ToString(), Time = dateTime.ToString("yyyy/MM/dd HH:mm:ss") });

                }

                this.CleanedTimeGird.ItemsSource = table_data;
            }
        }
    }
}
