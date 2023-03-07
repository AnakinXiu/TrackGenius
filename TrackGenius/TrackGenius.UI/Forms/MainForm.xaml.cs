using System.Windows;
using System.Windows.Input;
using TrackGenius.Communication;
using TrackGenius.Const;
using TrackGenius.Protocol;
using TrackGenius.UI.Forms;

namespace TrackGenius.UI
{
    /// <summary>
    /// Interaction logic for MainForm.xaml
    /// </summary>
    public partial class MainForm : Window
    {
        private MainFormParamViewModel _viewModel;

        CommunicateService _comService;

        public MainForm()
        {
            InitializeComponent();
            _viewModel = LoadMainFormParams();
            DataContext = _viewModel;

            CommandBindings.Add(new CommandBinding(ApplicationCommands.New));
        }

        private MainFormParamViewModel LoadMainFormParams() =>
            new MainFormParamViewModel()
            {
                ToolBarSize = new Size(Width, 50),
                ToolBarButtonSize = new Size(50, 50),
            };

        private void NewDriver_OnClick(object sender, RoutedEventArgs e)
        {
            new DriverCreationForm().ShowDialog();
        }

        private void OpenPort_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ComSelection.SelectedValue != null && (_comService == null || !_comService.IsOpened);
        }

        private void OpenPort_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            _comService = new CommunicateService(MessageParserFactory.GetParserByProtocal(TransponderType.Robitronic));

            var portSetting = new SerialPortSettings(38400, System.IO.Ports.StopBits.One, System.IO.Ports.Parity.None, 8);
            _comService.StartService(ComSelection.Text, portSetting);
        }

        private void StartRace_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            throw new System.NotImplementedException();
        }

        private void StartRace_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            throw new System.NotImplementedException();
        }
    }
}
