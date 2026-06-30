using System;
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
using TrackGenius.Protocol.Interfaces;
using TrackGenius.UI.Commands;
using Wpf.Ui.Appearance;

namespace TrackGenius.UI.ViewModels;

public class MainFormParamViewModel : INotifyPropertyChanged
{
    public sealed record ProtocolOption(string Name, IProtocol Protocol);

    private ISerialPortDescription _selectedSerialPort;
    private readonly CommunicateService _communicateService;
    private CancellationTokenSource _messagePollingCancellationTokenSource;
    private Task _messagePollingTask;
    private ThemeType _selectedTheme;
    private ProtocolOption _selectedProtocol;
    private string _lastError;

    public List<ThemeType> Themes { get; } = Enum.GetValues(typeof(ThemeType)).Cast<ThemeType>().ToList();  

    public List<ISerialPortDescription> SerialPorts { get; }

    public List<ProtocolOption> Protocols { get; }

    public ObservableCollection<string> Messages { get; } = [];

    public string LastError
    {
        get => _lastError;
        private set => PropertyChanged.RaiseIfChanged(this, ref _lastError, value, Equals, nameof(LastError));
    }

    public bool IsPortOpened => _communicateService?.IsOpened ?? false;

    public string ButtonContent => IsPortOpened ? "Close Port" : "Open Port";

    public string IsPortOpenedString => IsPortOpened ? "Opened" : "Closed";

    public ISerialPortDescription SelectedSerialPort
    {
        get => _selectedSerialPort;
        set => PropertyChanged.RaiseIfChanged(this, ref _selectedSerialPort, value, Equals, nameof(SelectedSerialPort));
    }

    public ICommand OpenClosePortCommand { get; }

    public ProtocolOption SelectedProtocol
    {
        get => _selectedProtocol;
        set => PropertyChanged.RaiseIfChanged(this, ref _selectedProtocol, value, Equals, nameof(SelectedProtocol));
    }

    public ThemeType SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (PropertyChanged.RaiseIfChanged(this, ref _selectedTheme, value, nameof(SelectedTheme)))
            {
                ApplyTheme(value);
            }
        }
    }

    public MainFormParamViewModel(CommunicateService communicateService)
    {
        _communicateService = communicateService ?? throw new ArgumentNullException(nameof(communicateService));
        _communicateService.PortOpenStateEventHandler += (_, _) =>
        {
            OnPropertyChanged(nameof(IsPortOpenedString));
            OnPropertyChanged(nameof(ButtonContent));
        };

        SerialPorts = SerialPortEnumerator.GetValidPortDescriptions().ToList();
        Protocols = Protocol.Protocols.AvailableProtocols.Select(p => new ProtocolOption(p.ProtocolName, p)).ToList();
        _selectedProtocol = Protocols.First();
        OpenClosePortCommand = new RelayCommand(OpenClosePort);

        _selectedTheme = GetThemeTypeFromCurrentTheme();
    }

    private static ThemeType GetThemeTypeFromCurrentTheme()
    {
        return ApplicationThemeManager.GetAppTheme() switch
        {
            ApplicationTheme.Dark => ThemeType.Dark,
            _ => ThemeType.Light,
        };
    }

    private static void ApplyTheme(ThemeType theme)
    {
        var applicationTheme = theme switch
        {
            ThemeType.Dark => ApplicationTheme.Dark,
            _ => ApplicationTheme.Light,
        };

        ApplicationThemeManager.Apply(applicationTheme);
    }

    private void OpenClosePort()
    {
        if (SelectedSerialPort == null)
            return;

        if (SelectedProtocol?.Protocol == null)
            return;

        StopMessagePolling();

        try
        {
            if (_communicateService.IsOpened)
            {
                _communicateService.CloseService();
            }
            else
            {
                _communicateService.StartService(SelectedSerialPort.PortName, SelectedProtocol.Protocol);
                Messages.Clear();
                LastError = string.Empty;
                StartMessagePolling();
            }
        }
        catch (ArgumentException ex)
        {
            HandleCommunicationError(ex);
        }
        catch (ObjectDisposedException ex)
        {
            HandleCommunicationError(ex);
        }
        catch (InvalidOperationException ex)
        {
            HandleCommunicationError(ex);
        }
    }

    private void StartMessagePolling()
    {
        _messagePollingCancellationTokenSource = new CancellationTokenSource();
        _messagePollingTask = PollMessageAsync(_messagePollingCancellationTokenSource.Token);
    }

    private async Task PollMessageAsync(CancellationToken cancellationToken)
    {
        try
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

                    continue;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException ex)
        {
            HandleCommunicationError(ex);
        }
        catch (InvalidOperationException ex)
        {
            HandleCommunicationError(ex);
        }
    }

    private void StopMessagePolling()
    {
        var cts = _messagePollingCancellationTokenSource;
        _messagePollingCancellationTokenSource = null;

        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _messagePollingTask = null;
    }

    private void HandleCommunicationError(Exception exception)
    {
        LastError = exception.Message;

        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.BeginInvoke(() => Messages.Add(exception.Message));
            return;
        }

        Messages.Add(exception.Message);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}