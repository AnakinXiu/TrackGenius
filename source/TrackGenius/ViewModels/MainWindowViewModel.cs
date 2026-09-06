using JetBrains.Annotations;

namespace TrackGenius.UI.ViewModels;

public class MainWindowViewModel
{
    [NotNull]
    public RacePageViewModel RacePageViewModel { get; set; }

    [NotNull]
    public SettingPageViewModel SettingPageViewModel { get; set; }

    public MainWindowViewModel([NotNull] RacePageViewModel racePageViewModel, [NotNull] SettingPageViewModel settingPageViewModel)
    {
        RacePageViewModel = racePageViewModel;
        SettingPageViewModel = settingPageViewModel;
    }
}