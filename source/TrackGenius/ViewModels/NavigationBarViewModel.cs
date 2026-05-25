using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using TrackGenius.UI.Commands;

namespace TrackGenius.UI.ViewModels;

public class NavigationBarViewModel
{
    private readonly Action<string> _onNavigationItemClick;
    public IList<NavigationItem> NavigationItems { get; set; }

    public RelayCommand<object> NaviBarClickCommand { get; set; }

    public NavigationBarViewModel(IEnumerable<NavigationItem> navigationItems, Action<string> onNavigationItemClick)
    {
        _onNavigationItemClick = onNavigationItemClick;
        NavigationItems = new ObservableCollection<NavigationItem>(navigationItems);
        NaviBarClickCommand = new RelayCommand<object>(NavigationItemClick);
    }

    private void NavigationItemClick(object selectedItem)
    {
        if (selectedItem is not NavigationItem navigationItem) 
            return;

        _onNavigationItemClick(navigationItem.Title);
    }
}

public class NavigationItem
{
    public Image ItemIcon { get; set; }

    public string Title { get; set; }

    public string Tip { get; set; }
}