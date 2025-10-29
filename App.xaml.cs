using System.Windows;

namespace LaserCutHMI.Prototype
{
    public partial class App : Application
    {
        public App()
        {
            // Başlangıçta yakalanmayan XAML hatası olduğunda görünmeden kapanmasın, mesaj verelim:
            this.DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show(e.Exception.ToString(), "Unhandled exception");
                // İsterseniz uygulamayı kapatın:
                // Current.Shutdown();
                e.Handled = true;
            };
        }
    }
}
