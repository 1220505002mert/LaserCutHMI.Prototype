using System.Collections.Generic;

namespace LaserCutHMI.Prototype.Models
{
    public class ChecksSnapshot
    {
        public bool DoorsClosed { get; set; }
        public bool VentilationOn { get; set; }
        public bool TableHasSheet { get; set; }
        public bool LaserHeadOk { get; set; }
        public bool ResonatorOn { get; set; }
        public int ResonatorPowerAvailableW { get; set; }
        public List<GasTank> Tanks { get; set; } = new();

        public bool BasicsOk(bool gasOk, int requiredPowerW)
            => DoorsClosed && VentilationOn && TableHasSheet && LaserHeadOk
               && ResonatorOn && (ResonatorPowerAvailableW >= requiredPowerW) && gasOk;
    }
}
