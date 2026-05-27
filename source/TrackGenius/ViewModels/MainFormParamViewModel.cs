using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using TrackGenius.Communication;
using TrackGenius.Communication.interfaces;
using TrackGenius.Const;
using TrackGenius.Protocol;
using TrackGenius.UI.Commands;

namespace TrackGenius.UI.ViewModels
{
    public class MainFormParamViewModel : INotifyPropertyChanged
    {
        private ISerialPortDescription _selectedSerialPort;
        private CommunicateService _communicateService;

        public Size ToolBarSize { get; set; }

        public Size ToolBarButtonSize { get; set; }

        public List<ISerialPortDescription> SerialPorts { get; }

        public bool IsPortOpened => _communicateService?.IsOpened ?? false;

        public string IsPortOpenedString => IsPortOpened ? "Opened" : "Closed";

        public ISerialPortDescription SelectedSerialPort
        {
            get => _selectedSerialPort;
            set => PropertyChanged.RaiseIfChanged(this, ref _selectedSerialPort, value, Equals, nameof(SelectedSerialPort));
        }

        public ICommand OpenPortCommand { get; }

        public MainFormParamViewModel()
        {
            SerialPorts = SerialPortEnumerator.GetValidPortDescriptions().ToList();
            OpenPortCommand = new RelayCommand(OpenPort);
        }

        private void OpenPort()
        {
            var robitronicMessageParser = new RobitronicMessageParser();
            _communicateService = new CommunicateService(robitronicMessageParser);
            _communicateService.PortOpenStateEventHandler += (sender, args) => OnPropertyChanged(nameof(IsPortOpenedString));
            _communicateService.StartService(SelectedSerialPort.PortName);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
