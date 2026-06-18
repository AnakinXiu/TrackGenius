using JetBrains.Annotations;

namespace TrackGenius.UI.ViewModels;

public class MainWindowViewModel
{
    [NotNull]
    public RacePageViewModel RacePageViewModel { get; set; }

    [NotNull]
    public MainFormParamViewModel MainFormParamViewModel { get; set; }

    public MainWindowViewModel([NotNull] RacePageViewModel racePageViewModel, [NotNull] MainFormParamViewModel mainFormParamViewModel)
    {
        RacePageViewModel = racePageViewModel;
        MainFormParamViewModel = mainFormParamViewModel;
    }
}