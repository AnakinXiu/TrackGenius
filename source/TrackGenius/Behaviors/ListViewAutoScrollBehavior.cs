using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TrackGenius.UI.Behaviors;

public class ListViewAutoScrollBehavior : DependencyObject
{
    public static readonly DependencyProperty AutoScrollToEndProperty =
        DependencyProperty.RegisterAttached(
            "AutoScrollToEnd",
            typeof(bool),
            typeof(ListViewAutoScrollBehavior),
            new PropertyMetadata(false, OnAutoScrollToEndChanged));

    private static readonly DependencyProperty TrackedCollectionProperty =
        DependencyProperty.RegisterAttached(
            "TrackedCollection",
            typeof(INotifyCollectionChanged),
            typeof(ListViewAutoScrollBehavior),
            new PropertyMetadata(null));

    private static readonly DependencyProperty CollectionChangedHandlerProperty =
        DependencyProperty.RegisterAttached(
            "CollectionChangedHandler",
            typeof(NotifyCollectionChangedEventHandler),
            typeof(ListViewAutoScrollBehavior),
            new PropertyMetadata(null));

    public static bool GetAutoScrollToEnd(DependencyObject obj) => (bool)obj.GetValue(AutoScrollToEndProperty);

    public static void SetAutoScrollToEnd(DependencyObject obj, bool value) => obj.SetValue(AutoScrollToEndProperty, value);

    private static void OnAutoScrollToEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListView listView)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            listView.Loaded += OnListViewLoaded;
            listView.Unloaded += OnListViewUnloaded;
            listView.DataContextChanged += OnListViewDataContextChanged;
            HookCollectionChanged(listView);
            ScrollToLastItem(listView);
            return;
        }

        listView.Loaded -= OnListViewLoaded;
        listView.Unloaded -= OnListViewUnloaded;
        listView.DataContextChanged -= OnListViewDataContextChanged;
        UnhookCollectionChanged(listView);
    }

    private static void OnListViewLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        HookCollectionChanged(listView);
        ScrollToLastItem(listView);
    }

    private static void OnListViewUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        UnhookCollectionChanged(listView);
    }

    private static void OnListViewDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        HookCollectionChanged(listView);
    }

    private static void HookCollectionChanged(ListView listView)
    {
        UnhookCollectionChanged(listView);

        if (listView.ItemsSource is not INotifyCollectionChanged collection)
        {
            return;
        }

        NotifyCollectionChangedEventHandler handler = (_, args) =>
        {
            if (args.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset or NotifyCollectionChangedAction.Replace)
            {
                ScrollToLastItem(listView);
            }
        };

        collection.CollectionChanged += handler;
        listView.SetValue(TrackedCollectionProperty, collection);
        listView.SetValue(CollectionChangedHandlerProperty, handler);
    }

    private static void UnhookCollectionChanged(ListView listView)
    {
        var collection = listView.GetValue(TrackedCollectionProperty) as INotifyCollectionChanged;
        var handler = listView.GetValue(CollectionChangedHandlerProperty) as NotifyCollectionChangedEventHandler;

        if (collection != null && handler != null)
        {
            collection.CollectionChanged -= handler;
        }

        listView.ClearValue(TrackedCollectionProperty);
        listView.ClearValue(CollectionChangedHandlerProperty);
    }

    private static void ScrollToLastItem(ListView listView)
    {
        if (listView.Items.Count == 0)
        {
            return;
        }

        listView.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (listView.Items.Count == 0)
            {
                return;
            }

            listView.ScrollIntoView(listView.Items[^1]);
        }));
    }
}