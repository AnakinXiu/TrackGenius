using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace TrackGenius.UI.Views.Controls
{
    public partial class RaceDataListControl : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(RaceDataListControl),
            new PropertyMetadata(null));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public RaceDataListControl()
        {
            InitializeComponent();
        }
    }
}
