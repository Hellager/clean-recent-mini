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

namespace clean_recent_mini
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool dark_mode = false;
        private string language = "en-US";

        public MainWindow()
        {
            Logger.Debug("Initialize project");

            InitializeComponent();

            change_theme();
            change_lang();
        }

        private void change_theme()
        {
            this.dark_mode = !this.dark_mode;

            // About theme change https://github.com/AngryCarrot789/WPFDarkTheme
            if (this.dark_mode)
            {
                ThemesController.SetTheme(ThemeType.SoftDark);
            }
            else
            {
                ThemesController.SetTheme(ThemeType.LightTheme);
            }

            Logger.Debug("Set dark mode to: " + this.dark_mode);
        }

        private void change_lang()
        {
            this.language = (this.language == "en-US" ? "zh-CN" : "en-US");

            System.Windows.Application.Current.Resources.MergedDictionaries[3] = new ResourceDictionary() { Source = new Uri($"Locale/{this.language}.xaml", UriKind.Relative) };
        }
    }
}
