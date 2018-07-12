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
using MahApps.Metro.Controls;


namespace TrackGenius
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            
        }

        private void OnSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("ShowSettings");
        }

        private void UIElement_OnMouseEnter(object sender, MouseEventArgs e)
        {
            NavHome.Background = new SolidColorBrush(Color.FromRgb(128, 128, 128));
        }

        private void Nav_Home_OnMouseLeave(object sender, MouseEventArgs e)
        {
            NavHome.Background = null;
        }

        private void Nav_Home_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NavHome.Visibility = Visibility.Collapsed;
        }
    }
}
