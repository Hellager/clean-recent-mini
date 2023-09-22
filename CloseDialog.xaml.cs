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
using System.Windows.Shapes;
using NLog;

namespace CleanRecentMini
{
    /// <summary>
    /// CloseDialog.xaml 的交互逻辑
    /// </summary>
    public partial class CloseDialog : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public CloseDialog()
        {
            InitializeComponent();
        }

        private void On_ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow;
            mainWindow.closeDialogOkOrCancel = true;
            mainWindow.closeRememberOption = this.AskOption.IsChecked.Value;

            if (this.AskOption.IsChecked == true) // Remember Option
            {
                // 不再询问则视为使用默认设置
                mainWindow.appConfig.close_to_tray = this.MiniRadio.IsChecked.Value;
            }

            mainWindow.closeOption = (byte)(this.MiniRadio.IsChecked.Value ? 1 : 0);

            this.Close();
        }

        private void On_CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow;
            mainWindow.closeDialogOkOrCancel = false;

            this.Close();
        }
    }
}
