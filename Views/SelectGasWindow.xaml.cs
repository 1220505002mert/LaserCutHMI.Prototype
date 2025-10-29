using System.Windows;
using LaserCutHMI.Prototype.Models;

namespace LaserCutHMI.Prototype.Views
{
    public partial class SelectGasWindow : Window
    {
        public Gas SelectedGas { get; private set; } = Gas.Air;

        public SelectGasWindow()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (rbOxygen.IsChecked == true) SelectedGas = Gas.Oxygen;
            else if (rbNitrogen.IsChecked == true) SelectedGas = Gas.Nitrogen;
            else SelectedGas = Gas.Air;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
