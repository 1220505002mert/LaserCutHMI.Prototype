namespace LaserCutHMI.Prototype.Models
{
    
    public class ParamRow
    {
        public Material Material { get; set; }
        public Gas Gas { get; set; }

        public int ThicknessMm { get; set; }
        public int PowerW { get; set; }
        public int Frequency { get; set; }
        public int Duty { get; set; }
        public double PressureBar { get; set; }
        public double CuttingHeightMm { get; set; }
    }
}
