using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using MoreLinq;
using TrackGenius.Communication;
using TrackGenius.UI.Forms;
using TrackGenius.UI.ViewModels;

namespace TrackGenius.UI
{
    /// <summary>
    /// Interaction logic for MainForm.xaml
    /// </summary>
    public partial class MainForm : Window
    {
        private MainWindowViewModel _viewModel;

        private readonly CommunicateService _comService;

        private readonly List<DockPanel> _mainPages;

        public MainForm(CommunicateService communicateService)
        {
            InitializeComponent();

            _comService = communicateService ?? throw new System.ArgumentNullException(nameof(communicateService));

            _mainPages = new List<DockPanel> { QuickRace, Settings };

            WindowChrome.SetWindowChrome(this, new WindowChrome()
            {
                ResizeBorderThickness = new Thickness(0, 0, 5, 5),
                CaptionHeight = 0
            });

            _viewModel = LoadMainWindowViewModel();
            DataContext = _viewModel;

            CommandBindings.Add(new CommandBinding(ApplicationCommands.New));
        }

        private MainWindowViewModel LoadMainWindowViewModel() =>
            new(SetMainPage)
            {
                MainFormParamViewModel = new MainFormParamViewModel(_comService)
                {
                    ToolBarSize = new Size(Width, 50),
                    ToolBarButtonSize = new Size(50, 50),
                }
            };

        private void NewDriver_OnClick(object sender, RoutedEventArgs e)
        {
            new DriverCreationForm().ShowDialog();
        }

        private void SetMainPage(string title)
        {
            switch (title)
            {
                case "QuickRace":
                    QuickRace.Visibility = Visibility.Visible;
                    _mainPages.Except([QuickRace]).ForEach(item =>item.Visibility = Visibility.Hidden);
                    break;
                case "Setting":
                    Settings.Visibility = Visibility.Visible;
                    _mainPages.Except([Settings]).ForEach(item => item.Visibility = Visibility.Hidden);
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
