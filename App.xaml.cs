using System.Windows;

namespace LaserCutHMI.Prototype
{
    public partial class App : Application
    {
        public App()
        {
            
            this.DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show(e.Exception.ToString(), "Unhandled exception");
                
                e.Handled = true;
            };
        }
    }
}
