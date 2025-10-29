using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LaserCutHMI.Prototype.Models
{
    public class CutParams : INotifyPropertyChanged
    {
        private int _powerW;
        public int PowerW { get => _powerW; set { _powerW = value; OnPropertyChanged(); } }

        private int _frequency;
        public int Frequency { get => _frequency; set { _frequency = value; OnPropertyChanged(); } }

        private int _duty;
        public int Duty { get => _duty; set { _duty = value; OnPropertyChanged(); } }

        private double _pressureBar;
        public double PressureBar { get => _pressureBar; set { _pressureBar = value; OnPropertyChanged(); } }

        private double _cuttingHeightMm;
        public double CuttingHeightMm { get => _cuttingHeightMm; set { _cuttingHeightMm = value; OnPropertyChanged(); } }

        public CutParams(int powerW = 3900, int frequency = 1000, int duty = 100, double pressureBar = 10, double heightMm = 1)
        {
            _powerW = powerW;
            _frequency = frequency;
            _duty = duty;
            _pressureBar = pressureBar;
            _cuttingHeightMm = heightMm;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
