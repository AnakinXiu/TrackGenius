using System;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TrackGenius.UI.Persistence;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UI.Views.Controls
{
    public partial class RaceDataListControl : UserControl
    {
        private readonly RaceDataColumnPreferencesStore _preferencesStore = new();

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(RaceDataListControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        // Read by XAML cell/header Visibility bindings. Set before InitializeComponent via initializer.
        public RaceDataColumnSettings ColumnSettings { get; } = new();

        public RaceDataListControl()
        {
            InitializeComponent();
            LoadColumnPreferences();
            BuildHeaderContextMenu();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not RaceDataListControl control)
                return;
            if (e.NewValue is not IEnumerable source)
                return;

            var view = CollectionViewSource.GetDefaultView(source);
            using (view.DeferRefresh())
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(
                    new SortDescription(nameof(RaceDataItem.RacerPosition), ListSortDirection.Ascending));
            }

            if (view is ICollectionViewLiveShaping liveShaping)
            {
                liveShaping.LiveSortingProperties.Add(nameof(RaceDataItem.RacerPosition));
                liveShaping.IsLiveSorting = true;
            }
        }

        private void BuildHeaderContextMenu()
        {
            var menu = new ContextMenu();
            foreach (var option in ColumnSettings.All)
            {
                var item = new MenuItem
                {
                    Header = option.Label,
                    IsCheckable = true,
                    IsEnabled = option.CanHide,
                    IsChecked = option.IsVisible,
                };
                item.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(RaceDataColumnOption.IsVisible))
                {
                    Source = option,
                    Mode = BindingMode.TwoWay,
                });
                menu.Items.Add(item);
            }
            HeaderBorder.ContextMenu = menu;
        }

        private void LoadColumnPreferences()
        {
            ColumnSettings.ApplyHiddenKeys(_preferencesStore.Load());

            // Subscribe AFTER applying, so the initial load does not trigger a save.
            foreach (var option in ColumnSettings.All)
                option.PropertyChanged += OnOptionVisibilityChanged;
        }

        private void OnOptionVisibilityChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RaceDataColumnOption.IsVisible))
                SaveColumnPreferences();
        }

        private void SaveColumnPreferences()
        {
            try
            {
                _preferencesStore.Save(ColumnSettings.GetHiddenKeys());
            }
            catch
            {
                // Persistence is best-effort; never crash the UI over a failed save.
            }
        }
    }
}
