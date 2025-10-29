using System.ComponentModel;

namespace LaserCutHMI.Prototype.Models
{
    public class GasTank : INotifyPropertyChanged
    {
        private Gas _gas;
        private bool _connected;
        private double _levelPercent;

        public Gas Gas
        {
            get => _gas; set { _gas = value; OnPropertyChanged(nameof(Gas)); }
        }
        public bool Connected
        {
            get => _connected; set { _connected = value; OnPropertyChanged(nameof(Connected)); }
        }
        public double LevelPercent
        {
            get => _levelPercent; set { _levelPercent = value; OnPropertyChanged(nameof(LevelPercent)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
