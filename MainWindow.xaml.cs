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

        // XAML’de (Window ...) Loaded="Window_Loaded" bağlıysa çalışır.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Uygulama açılışında yapılacak ek işlemler varsa buraya ekleyebilirsin.
        }

        // XAML’de (Window ...) Closing="Window_Closing" bağlıysa çalışır.
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Kapanışta temizlik yapılacaksa (log, onay, vs.) buraya ekleyebilirsin.
            // Not: ViewModel’de özel bir Dispose gerekmiyor.
        }

        // İstersen ViewModel’e hızlı erişim için yardımcı özellik:
        private MainViewModel VM => (MainViewModel)DataContext;
    }
}
