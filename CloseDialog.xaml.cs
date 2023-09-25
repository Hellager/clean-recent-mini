using System.Linq;
using System.Windows;
using NLog;

namespace CleanRecentMini
{
    /// <summary>
    /// Interaction logic for CloseDialog.xaml
    /// </summary>
    public partial class CloseDialog : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public CloseDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handle ConfirmButton click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow;
            mainWindow.closeDialogOkOrCancel = true;
            mainWindow.closeRememberOption = this.RememberOption.IsChecked.Value;

            if (this.RememberOption.IsChecked == true)
            {
                mainWindow.appConfig.close_option = this.MinimizeRadio.IsChecked.Value;
            }

            mainWindow.closeOption = (byte)(this.MinimizeRadio.IsChecked.Value ? 1 : 0);

            this.Close();
        }

        /// <summary>
        /// Handle CancelButton click event
        /// </summary>
        /// (<paramref name="sender"/>, <paramref name="e"/>).
        /// <param><c>sender</c> Event sender.</param>
        /// <param><c>e</c> Route event args.</param>
        private void On_CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(window => window is MainWindow) as MainWindow;
            mainWindow.closeDialogOkOrCancel = false;

            this.Close();
        }
    }
}
