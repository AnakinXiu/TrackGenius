using System.Windows;

namespace TrackGenius.UI.ViewModels;

public class MainWindowViewModel
{
        public NavigationBarViewModel NavigationBarViewModel { get; set; }

        public MainWindowViewModel()
        {
            var items = new[]
            {
                new NavigationItem
                {
                    Title = "Home",
                    Tip = "Home Tip",
                    Command = new RelayCommand(() => { MessageBox.Show("Home Click."); })
                },
                new NavigationItem
                {
                    Title = "Home",
                    Tip = "Home Tip",
                    Command = new RelayCommand(() => { MessageBox.Show("Home Click."); })
                }
            };

            NavigationBarViewModel = new NavigationBarViewModel(items);
    }

}