using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace LaserCutHMI.Prototype.ViewModels
{
    
    public class Debouncer
    {
        private readonly System.Timers.Timer _timer;
        private readonly TimeSpan _delay;
        private Action? _action;

        public Debouncer(TimeSpan delay)
        {
            _delay = delay;
            _timer = new System.Timers.Timer(delay.TotalMilliseconds) { AutoReset = false };
            _timer.Elapsed += Timer_Elapsed;
        }


        public void Debounce(Action action)
        {
            _action = action;
            _timer.Stop();
            _timer.Start();
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            _action?.Invoke();
        }
    }
}
