using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaserCutHMI.Prototype.Models;

namespace LaserCutHMI.Prototype.Services
{
    public interface ISystemCheckService
    {
        Task<ChecksSnapshot> RefreshAsync();
    }

    public class SystemCheckService : ISystemCheckService
    {
        private readonly Random _rnd = new();
        private bool RandOk(double p) => _rnd.NextDouble() < p;

        public Task<ChecksSnapshot> RefreshAsync()
        {
            var snap = new ChecksSnapshot
            {
                DoorsClosed = RandOk(0.90),
                VentilationOn = RandOk(0.90),
                TableHasSheet = RandOk(0.90),
                LaserHeadOk = RandOk(0.90),
                ResonatorOn = RandOk(0.90),

                // Rezonatör gücü sabit 4000 W
                ResonatorPowerAvailableW = 4000,

                Tanks = new List<GasTank>
                {
                    new GasTank { Gas = Gas.Air,       Connected = RandOk(0.90), LevelPercent = 100.0 },
                    new GasTank { Gas = Gas.Oxygen,    Connected = RandOk(0.90), LevelPercent = 100.0 },
                    new GasTank { Gas = Gas.Nitrogen,  Connected = RandOk(0.90), LevelPercent = 100.0 }
                }
            };
            return Task.FromResult(snap);
        }
    }
}
