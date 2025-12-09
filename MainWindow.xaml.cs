using System.Windows;
using LaserCutHMI.Prototype.ViewModels;

namespace LaserCutHMI.Prototype
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            
        }

        
        private MainViewModel VM => (MainViewModel)DataContext;
    }
}
