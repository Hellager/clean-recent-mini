﻿using System;
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

namespace clean_recent_mini
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

        }

        private void On_CancelButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}