using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using TrackGenius.Communication;

namespace TrackGenius.UI
{
    public class MainFormParamViewModel
    {
        public Size ToolBarSize { get; set; }

        public Size ToolBarButtonSize { get; set; }

        public List<string> SerialPorts { get; }

        public RoutedUICommand OpenComPortCommand { get; }

        public MainFormParamViewModel()
        {
            using(var serialPortWrapper = new SerialPortWrapper())
                SerialPorts = serialPortWrapper.GetValidPortNames().ToList();

            OpenComPortCommand = new RoutedUICommand("Open Serial Port", "OpenCom", typeof(MainFormParamViewModel));
        }
    }
}
