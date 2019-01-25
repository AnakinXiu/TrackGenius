using System.Windows;
using TrackGenius.UI.Forms;

namespace TrackGenius.UI
{
    /// <summary>
    /// Interaction logic for MainForm.xaml
    /// </summary>
    public partial class MainForm : Window
    {
        public MainForm()
        {
            InitializeComponent();

            this.DataContext = LoadMainFormParams();
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
    }
}
