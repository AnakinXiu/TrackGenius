using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TrackGenius.Communication;
using TrackGenius.Communication.interfaces;

namespace TrackGenius.UI.ViewModels
{
    public class MainFormParamViewModel
    {
        public Size ToolBarSize { get; set; }

        public Size ToolBarButtonSize { get; set; }

        public List<ISerialPortDescription> SerialPorts { get; }

        public MainFormParamViewModel()
        {
            SerialPorts = SerialPortEnumerator.GetValidPortDescriptions().ToList();
        }
    }
}
