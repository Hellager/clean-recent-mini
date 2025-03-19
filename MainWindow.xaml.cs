using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using Newtonsoft.Json;
using Application = System.Windows.Application;
using NotifyIcon = System.Windows.Forms.NotifyIcon;


namespace CleanRecentMini
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private FileSystemWatcher _watcher;
        private AppSettings _settings;
        private const string SettingsPath = "settings.json";
        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            InitializeTrayIcon();
            InitializeFileWatcher();
            Hide(); // 初始隐藏窗口
        }
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    _settings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(SettingsPath));
                }
                else
                {
                    _settings = new AppSettings();
                    SaveSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
            SetStartup(_settings.IsStartupEnabled);
        }
        private void SaveSettings()
        {
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(_settings));
        }
        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("/Assets/icon.ico", UriKind.Relative)).Stream),
                Visible = true,
                ContextMenu = new ContextMenu()
            };
            var stealthItem = new MenuItem("无痕模式", ToggleStealthMode)
            {
                Checked = _settings.IsStealthModeEnabled
            };

            var startupItem = new MenuItem("开机自启动", ToggleStartup)
            {
                Checked = _settings.IsStartupEnabled
            };
            var exitItem = new MenuItem("退出", ExitApplication);
            _notifyIcon.ContextMenu.MenuItems.Add(stealthItem);
            _notifyIcon.ContextMenu.MenuItems.Add(startupItem);
            _notifyIcon.ContextMenu.MenuItems.Add(exitItem);
        }
        private void InitializeFileWatcher()
        {
            if (_settings.IsStealthModeEnabled)
            {
                _watcher = new FileSystemWatcher
                {
                    Path = Environment.GetFolderPath(Environment.SpecialFolder.Recent),
                    Filter = "*.automaticDestinations-ms",
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += OnFileChanged;
            }
        }
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now}] 检测到快速访问变更: {e.FullPath}");
        }
        private void ToggleStealthMode(object sender, EventArgs e)
        {
            _settings.IsStealthModeEnabled = !_settings.IsStealthModeEnabled;
            ((MenuItem)sender).Checked = _settings.IsStealthModeEnabled;
            if (_settings.IsStealthModeEnabled)
            {
                InitializeFileWatcher();
            }
            else
            {
                _watcher?.Dispose();
                _watcher = null;
            }
            SaveSettings();
        }
        private void ToggleStartup(object sender, EventArgs e)
        {
            _settings.IsStartupEnabled = !_settings.IsStartupEnabled;
            ((MenuItem)sender).Checked = _settings.IsStartupEnabled;
            SetStartup(_settings.IsStartupEnabled);
            SaveSettings();
        }
        private void SetStartup(bool enable)
        {
            const string appName = "StealthApp";
            var registryKey = Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (enable)
            {
                registryKey?.SetValue(appName, System.Windows.Forms.Application.ExecutablePath);
            }
            else
            {
                registryKey?.DeleteValue(appName, false);
            }
        }
        private void ExitApplication(object sender, EventArgs e)
        {
            _notifyIcon.Dispose();
            _watcher?.Dispose();
            Application.Current.Shutdown();
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; // 阻止窗口关闭
            Hide();         // 最小化到托盘
            base.OnClosing(e);
        }
    }
    // AppSettings.cs
    public class AppSettings
    {
        public bool IsStealthModeEnabled { get; set; } = false;
        public bool IsStartupEnabled { get; set; } = false;
    }

}
