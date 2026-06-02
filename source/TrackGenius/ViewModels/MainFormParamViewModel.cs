using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TrackGenius.Communication;
using TrackGenius.Communication.interfaces;
using TrackGenius.Const;
using TrackGenius.UI.Commands;

namespace TrackGenius.UI.ViewModels
{
    public class MainFormParamViewModel : INotifyPropertyChanged
    {
        private ISerialPortDescription _selectedSerialPort;
        private CommunicateService _communicateService;
        private CancellationTokenSource _messagePollingCancellationTokenSource;
        private Task _messagePollingTask;

        public Size ToolBarSize { get; set; }

        public Size ToolBarButtonSize { get; set; }

        public List<ISerialPortDescription> SerialPorts { get; }

        public ObservableCollection<string> Messages { get; } = [];

        public bool IsPortOpened => _communicateService?.IsOpened ?? false;

        public string IsPortOpenedString => IsPortOpened ? "Opened" : "Closed";

        public ISerialPortDescription SelectedSerialPort
        {
            get => _selectedSerialPort;
            set => PropertyChanged.RaiseIfChanged(this, ref _selectedSerialPort, value, Equals, nameof(SelectedSerialPort));
        }

        public ICommand OpenPortCommand { get; }

        public MainFormParamViewModel(CommunicateService communicateService)
        {
            _communicateService = communicateService ?? throw new System.ArgumentNullException(nameof(communicateService));
            _communicateService.PortOpenStateEventHandler += (_, _) => OnPropertyChanged(nameof(IsPortOpenedString));
            SerialPorts = SerialPortEnumerator.GetValidPortDescriptions().ToList();
            OpenPortCommand = new RelayCommand(OpenPort);
        }

        private void OpenPort()
        {
            if (SelectedSerialPort == null)
                return;

            StopMessagePolling();
            Messages.Clear();

            _communicateService.StartService(SelectedSerialPort.PortName);
           
            StartMessagePolling();
        }

        private void StartMessagePolling()
        {
            _messagePollingCancellationTokenSource = new CancellationTokenSource();
            _messagePollingTask = PollMessageAsync(_messagePollingCancellationTokenSource.Token);
        }

        private async Task PollMessageAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_communicateService.TryGetNextMessage(out var message))
                {
                    var messageText = message.Deserialize();
                    if (Application.Current?.Dispatcher is { } dispatcher)
                    {
                        dispatcher.BeginInvoke(() => Messages.Add(messageText));
                    }
                    else
                    {
                        Messages.Add(messageText);
                    }
                    else
                    {
                        Messages.Add(messageText);
                    }

                    continue;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        private void StopMessagePolling()
        {
            _messagePollingCancellationTokenSource?.Cancel();

            try
            {
                _messagePollingTask?.Wait(System.TimeSpan.FromSeconds(1));
            }
            catch (System.AggregateException ex) when (ex.InnerExceptions.All(err => err is TaskCanceledException or System.OperationCanceledException))
            {
            }
            finally
            {
                _messagePollingCancellationTokenSource?.Dispose();
                _messagePollingCancellationTokenSource = null;
                _messagePollingTask = null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
