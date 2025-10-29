using System.ComponentModel;
using System.Collections.ObjectModel;

namespace LaserCutHMI.Prototype.Models
{
    // Sadece SystemChecks (GasTank ayrı dosyada)
    public class SystemChecks : INotifyPropertyChanged
    {
        private ObservableCollection<GasTank> _tanks = new ObservableCollection<GasTank>();

        public ObservableCollection<GasTank> Tanks
        {
            get => _tanks;
            set { _tanks = value; OnPropertyChanged(nameof(Tanks)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
