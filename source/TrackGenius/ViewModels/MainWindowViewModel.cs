using System;

namespace TrackGenius.UI.ViewModels;

public class MainWindowViewModel
{

    public NavigationBarViewModel NavigationBarViewModel { get; set; }

    public MainFormParamViewModel MainFormParamViewModel { get; set; }

    public MainWindowViewModel(Action<string> setMainPage)
    {
        var items = new[]
        {
            new NavigationItem
            {
                Title = "QuickRace",
                Tip = "QuickRace Tip"
            },
            new NavigationItem
            {
                Title = "Setting",
                Tip = "Setting Tip"
            }
        };

        NavigationBarViewModel = new NavigationBarViewModel(items, setMainPage);
    }
}