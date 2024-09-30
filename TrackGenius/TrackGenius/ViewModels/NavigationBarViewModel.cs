using System.Collections.Generic;
using System.Drawing;

namespace TrackGenius.UI.ViewModels;

public class NavigationBarViewModel
{
    public List<NavigationItem> NavigationItems { get; set; }
}

public class NavigationItem
{
    public Image ItemIcon { get; set; }

    public string Title { get; set; }

    public string Tip { get; set; }

    public RelayCommand Command { get; set; }

    public NavigationItem(Image itemIcon)
    {
        ItemIcon = itemIcon;
    }
}