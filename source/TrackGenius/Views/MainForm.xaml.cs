using System;
using System.Windows.Input;
using TrackGenius.Communication;
using TrackGenius.Protocol.Interfaces;
using TrackGenius.UI.ViewModels;
using TrackGenius.UI.Views.Pages;
using Wpf.Ui.Controls;

namespace TrackGenius.UI
{
    /// <summary>
    /// Interaction logic for MainForm.xaml
    /// </summary>
    public partial class MainForm : FluentWindow
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly CommunicateService _comService;

        public MainForm(CommunicateService communicateService, IProtocol protocol)
        {
            InitializeComponent();

            _comService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
            var currentProtocol = protocol ?? throw new ArgumentNullException(nameof(protocol));

            _viewModel = new MainWindowViewModel(new RacePageViewModel(), new MainFormParamViewModel(_comService, currentProtocol));

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
                    settingsPage.DataContext = _viewModel.MainFormParamViewModel;
                    break;
                case QuickRacePage quickRacePage:
                    quickRacePage.DataContext = _viewModel.RacePageViewModel;
                    break;
            }
        }

        private void StartRace_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
        }

        private void StartRace_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        { }
    }
}
