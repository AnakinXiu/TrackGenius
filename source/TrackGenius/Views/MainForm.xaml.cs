using System;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using TrackGenius.Communication;
using TrackGenius.Core;
using TrackGenius.Speech;
using TrackGenius.UI.Pages;
using TrackGenius.UI.ViewModels;
using Wpf.Ui.Controls;

namespace TrackGenius.UI
{
    /// <summary>
    /// Interaction logic for MainForm.xaml
    /// </summary>
    public partial class MainForm : FluentWindow
    {
        [NotNull]
        private readonly ILogger _userBehaviorLogger;
        [NotNull]
        private readonly CommunicateService _comService;
        [NotNull]
        private readonly IRaceConnectionService _connectionService;
        private readonly MainWindowViewModel _viewModel;

        public MainForm(CommunicateService communicateService,
            IRaceConnectionService connectionService,
            ILogger userBehaviorLogger,
            RaceAnnouncer speechAnnouncer)
        {
            InitializeComponent();

            _userBehaviorLogger = userBehaviorLogger ?? throw new ArgumentNullException(nameof(userBehaviorLogger));
            _comService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));

            var raceEngineFactory = new RaceEngineFactory(connectionService, _comService);
            _viewModel = new MainWindowViewModel(
                new RacePageViewModel(raceEngineFactory, connectionService, speechAnnouncer),
                new SettingPageViewModel(_comService, connectionService, _userBehaviorLogger));

            DataContext = _viewModel;

            RootNavigation.Navigated += OnNavigated;
            CommandBindings.Add(new CommandBinding(ApplicationCommands.New));
        }

        private void OnNavigated(NavigationView sender, NavigatedEventArgs args)
        {
            // Pages are instantiated by the NavigationView via their parameterless ctor.
            // Inject the appropriate view-model on each navigation.

            switch (args.Page)
            {
                case SettingsPage settingsPage:
                    settingsPage.DataContext = _viewModel.SettingPageViewModel;
                    break;
                case QuickRacePage quickRacePage:
                    quickRacePage.DataContext = _viewModel.RacePageViewModel;
                    break;
            }
        }

        private void TogglePane_OnClick(object sender, RoutedEventArgs e)
        {
            RootNavigation.IsPaneOpen = !RootNavigation.IsPaneOpen;
        }

        private void StartRace_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
        }

        private void StartRace_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        { }
    }
}
