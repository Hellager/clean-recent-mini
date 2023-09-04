using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
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

        private QuickAccessHandler quickAccessHandler = new QuickAccessHandler();

        public MainWindow()
        {
            Logger.Debug("Initialize project");

            InitializeComponent();
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

    }
}
