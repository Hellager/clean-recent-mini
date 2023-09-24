using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NLog;
using NanoidDotNet;
using Newtonsoft.Json;
using Microsoft.Win32;

namespace CleanRecentMini
{
    enum FilterlistDialogMode
    {
        AppendTable,
        EditTable,
        ImportData,
        ExportData,
        CleanData
    }

    /// <summary>
    /// FilterDialog.xaml 的交互逻辑
    /// </summary>
    public partial class FilterDialog : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private FilterlistDialogMode dialogmode = FilterlistDialogMode.EditTable;
        private static readonly List<string> dialogTitle = new List<string>()
        {
            "TitleAppendItem", "TitleEditItem", "TitleImportItems", "TitleExportItems", "TitleCleanItems"
        };

        // About append item
        private FilterlistTableItem filterItem;
        
        // About import/export items
        private List<CleanFilterItem> transferList = new List<CleanFilterItem>();
        public ObservableCollection<FilterlistTableItem> transferTableData = new ObservableCollection<FilterlistTableItem>();

        // About clean items;
        private List<string> cleanList = new List<string>();
        public ObservableCollection<CleanTableItem> cleanTableData = new ObservableCollection<CleanTableItem>();

        public FilterDialog()
        {
            InitializeComponent();
        }

        public void SetDialogMode(byte data)
        {
            this.dialogmode = (FilterlistDialogMode)data;
            this.FilterlistController.SelectedIndex = data;

            ResourceDictionary resourceDictionary;
            resourceDictionary = System.Windows.Application.Current.Resources.MergedDictionaries[3];
            this.Title = resourceDictionary.Contains(dialogTitle[data]) ? resourceDictionary[dialogTitle[data]].ToString() : dialogTitle[data];

            this.FilterlistController.SelectedIndex = data / 2;
        }

        public void SetEditItemData(FilterlistTableItem data)
        {
            this.filterItem = data;

            this.KeywordInput.Text = data.Keyword;
            this.StateSelector.SelectedIndex = Convert.ToInt32(data.State);
            this.GroupSelector.SelectedIndex = Convert.ToInt32(data.Group);
        }

        public void SetTransferData(List<CleanFilterItem> data)
        {
            this.transferList = data;

            this.Refresh_Transfer_Table();
        }

        public void SetCleanData(List<string> data)
        {
            this.cleanList = data;

            this.Refresh_Transfer_Table();
        }


        private void Refresh_Transfer_Table()
        {
            this.transferTableData.Clear();

            for (Int32 i = 0; i < this.transferList.Count; i++)
            {
                this.transferTableData.Add(new FilterlistTableItem(this.transferList.ElementAt(i)));
            }

            this.TransferTable.DataContext = this.transferTableData;
        }

        private void On_Item_State_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            // No need to do something
        }

        private void On_Item_Group_Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            // No need to do something
        }

        private void On_Transfer_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var CurCheckBox = sender as System.Windows.Controls.CheckBox;
            var CurCheckBoxUID = CurCheckBox.Uid;

            if (CurCheckBoxUID == "-1")
            {
                foreach (var item in this.transferTableData)
                {
                    item.IsSelected = true;
                }

                CurCheckBox.Uid = "-2";
                return;
            }
            else if (CurCheckBoxUID == "-2")
            {
                foreach (var item in this.transferTableData)
                {
                    item.IsSelected = false;
                }

                CurCheckBox.Uid = "-1";
                return;
            }


            foreach (var item in this.transferTableData)
            {
                if (item.Id == CurCheckBoxUID)
                {
                    item.IsSelected = !item.IsSelected;
                    break;
                }
            }
        }

        private void On_Clean_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var CurCheckBox = sender as System.Windows.Controls.CheckBox;
            var CurCheckBoxUID = CurCheckBox.Uid;

            if (CurCheckBoxUID == "-1")
            {
                foreach (var item in this.cleanTableData)
                {
                    item.IsSelected = true;
                }

                CurCheckBox.Uid = "-2";
                return;
            }
            else if (CurCheckBoxUID == "-2")
            {
                foreach (var item in this.cleanTableData)
                {
                    item.IsSelected = false;
                }

                CurCheckBox.Uid = "-1";
                return;
            }


            foreach (var item in this.cleanTableData)
            {
                if (item.Id == CurCheckBoxUID)
                {
                    item.IsSelected = !item.IsSelected;
                    break;
                }
            }
        }

        private void On_ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow;

            switch (dialogmode)
            {
                case FilterlistDialogMode.AppendTable:
                    {
                        if (this.KeywordInput.Text != "")
                        {
                            CleanFilterItem appendItem = new CleanFilterItem()
                            {
                                id = Nanoid.Generate(size: 8),
                                group = (byte)this.GroupSelector.SelectedIndex,
                                category = 0,
                                state = Convert.ToBoolean(this.StateSelector.SelectedIndex),
                                keyword = this.KeywordInput.Text,
                                create_at = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
                                update_at = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
                                author = "default",
                                level = 0,
                                labels = new List<CleanFilterItemLabel>()
                            };

                            for (int i = 0; i < mainWindow.cleanConfig.filter_list.Count; i++)
                            {
                                if (mainWindow.cleanConfig.filter_list[i].id == appendItem.id)
                                {
                                    Logger.Debug("Repeat Id");
                                    appendItem.id = Nanoid.Generate(size: 8);
                                }

                                if (mainWindow.cleanConfig.filter_list[i].keyword == appendItem.keyword)
                                {
                                    Logger.Debug("Repeat keyword");
                                    return;
                                }
                            }

                            mainWindow.cleanConfig.filter_list.Add(appendItem);

                            Logger.Debug("Try append new item");
                        }
                        else
                        {
                            Logger.Debug("Keyword should not be empty!");
                        }
                    }
                    break;

                case FilterlistDialogMode.EditTable:
                    {
                        if (this.KeywordInput.Text != this.filterItem.Keyword ||
                            this.StateSelector.SelectedIndex != Convert.ToInt32(this.filterItem.State) ||
                            this.GroupSelector.SelectedIndex != Convert.ToInt32(this.filterItem.Group))
                        {
                            for (int i = 0; i < mainWindow.cleanConfig.filter_list.Count; i++)
                            {
                                if (mainWindow.cleanConfig.filter_list[i].id == this.filterItem.Id)
                                {
                                    var origin = mainWindow.cleanConfig.filter_list[i];

                                    origin.keyword = this.KeywordInput.Text;
                                    origin.state = Convert.ToBoolean(this.StateSelector.SelectedIndex);
                                    origin.group = (byte)this.GroupSelector.SelectedIndex;

                                    mainWindow.cleanConfig.filter_list[i] = origin;

                                    Logger.Debug("Confirm item update");
                                }
                            }

                            Logger.Debug("Update filterlist");
                        }
                        else
                        {
                            Logger.Debug("No need to update");
                        }
                    }
                    break;

                case FilterlistDialogMode.ImportData:
                    {
                        int add_item_cnt = 0, repeat_item_cnt = 0;
                        Dictionary<string, string> cur_filter_item_dict = new Dictionary<string, string>(); // id: keyword
                        for (Int32 i = 0; i < mainWindow.cleanConfig.filter_list.Count; i++)
                        {
                            cur_filter_item_dict.Add(mainWindow.cleanConfig.filter_list[i].id, mainWindow.cleanConfig.filter_list[i].keyword);
                        }

                        for (Int32 i = 0; i < this.transferTableData.Count; i++)
                        {
                            if (this.transferTableData[i].IsSelected)
                            {
                                if (!cur_filter_item_dict.Values.Contains(this.transferTableData[i].Keyword))
                                {
                                    if (cur_filter_item_dict.Keys.Contains(this.transferTableData[i].Id))
                                    {
                                        this.transferTableData[i].Id = Nanoid.Generate(size: 8);
                                    }

                                    mainWindow.cleanConfig.filter_list.Add(new CleanFilterItem()
                                    {
                                        id = this.transferTableData[i].Id,
                                        group = (byte)this.transferTableData[i].Group,
                                        category = this.transferTableData[i].Category,
                                        state = Convert.ToBoolean(this.transferTableData[i].State),
                                        keyword = this.transferTableData[i].Keyword,
                                        create_at = this.transferTableData[i].CreateAt,
                                        update_at = this.transferTableData[i].UpdateAt,
                                        author = this.transferTableData[i].Author,
                                        level = (byte)this.transferTableData[i].Level,
                                        labels = this.transferTableData[i].Labels
                                    });

                                    add_item_cnt++;
                                }
                                else
                                {
                                    repeat_item_cnt++;
                                }
                                
                            }
                        }

                        mainWindow.Refresh_Filterlist_Table();

                        Logger.Debug("Total " + add_item_cnt + " items added, " + repeat_item_cnt + " repeated.");

                    }
                    break;

                case FilterlistDialogMode.ExportData:
                    {
                        ExportFilterList export_data = new ExportFilterList();
                        List<CleanFilterItem> export_list = new List<CleanFilterItem>();
                        for (Int32 i = 0; i < this.transferTableData.Count; i++)
                        {
                            if (this.transferTableData[i].IsSelected)
                            {
                                export_list.Add(new CleanFilterItem()
                                {
                                    id = this.transferTableData[i].Id,
                                    group = (byte)this.transferTableData[i].Group,
                                    category = this.transferTableData[i].Category,
                                    state = Convert.ToBoolean(this.transferTableData[i].State),
                                    keyword = this.transferTableData[i].Keyword,
                                    create_at = this.transferTableData[i].CreateAt,
                                    update_at = this.transferTableData[i].UpdateAt,
                                    author = this.transferTableData[i].Author,
                                    level = (byte)this.transferTableData[i].Level,
                                    labels = this.transferTableData[i].Labels
                                });
                            }
                        }

                        SaveFileDialog save_dialog = new SaveFileDialog();
                        save_dialog.Filter = "JSON File |*.json";
                        save_dialog.Title = "Save filter list to";
                        // save_dialog.CheckFileExists = true;

                        save_dialog.ShowDialog();

                        if (save_dialog.FileName != "")
                        {
                            System.IO.FileStream fs = (System.IO.FileStream)save_dialog.OpenFile();

                            export_data.filter_list = export_list;

                            var config_content = JsonConvert.SerializeObject(export_data);
                            Byte[] config = new UTF8Encoding(true).GetBytes(config_content);
                            fs.Write(config, 0, config.Length);

                            Logger.Debug("Export filter lsit to " + save_dialog.FileName);
                        }

                    }
                    break;

                case FilterlistDialogMode.CleanData:
                    {
                        List<string> confirm_clean_list = new List<string>();

                        for (Int32 i = 0; i < this.cleanTableData.Count; i++)
                        {
                            if (this.cleanTableData[i].IsSelected)
                            {
                                confirm_clean_list.Add(this.cleanTableData[i].Path);
                            }
                        }

                        Logger.Debug(string.Format("{0} item confirmed to clean", confirm_clean_list.Count));
                    }
                    break;

                default:
                    {
                        Logger.Debug("No matching mode!");
                    }
                    break;
            }

            this.Close();

        }

        private void On_CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.transferList.Clear();
            this.cleanList.Clear();
            this.transferTableData.Clear();
            this.cleanTableData.Clear();

            this.Close();
        }
    }
}
