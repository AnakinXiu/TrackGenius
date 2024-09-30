using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;

namespace TrackGenius.UI.ViewModels;

public class NavigationBarViewModel
{
    public IList<NavigationItem> NavigationItems { get; set; }

    public NavigationBarViewModel(IEnumerable<NavigationItem> navigationItems)
    {
        NavigationItems = new ObservableCollection<NavigationItem>(navigationItems);
    }
}

public class NavigationItem
{
    public Image ItemIcon { get; set; }

    public string Title { get; set; }

    public string Tip { get; set; }

    public RelayCommand Command { get; set; }
}