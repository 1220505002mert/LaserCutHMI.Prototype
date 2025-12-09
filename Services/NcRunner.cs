using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LaserCutHMI.Prototype.Services
{
    public interface INcRunner
    {
        int CurrentIndex { get; }
        bool IsRunning { get; }
        Task RunAsync(IList<string> lines, int startIndex, Func<int, Task> onLine, CancellationToken ct);
        void EStop(); // acil durdur: index’i koru
        void Stop();  // tamamen iptal: index’i sıfırla
    }

    public class NcRunner : INcRunner
    {
        private CancellationTokenSource? _cts;
        private bool _estopped;

        public int CurrentIndex { get; private set; } = 0;
        public bool IsRunning { get; private set; }

        public async Task RunAsync(IList<string> lines, int startIndex, Func<int, Task> onLine, CancellationToken ct)
        {
            _estopped = false;
            IsRunning = true;
            CurrentIndex = Math.Clamp(startIndex, 0, Math.Max(0, lines.Count - 1));

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _cts = linked;

            for (int i = CurrentIndex; i < lines.Count; i++)
            {
                CurrentIndex = i;
                ct.ThrowIfCancellationRequested();
                await onLine(i);
                await Task.Delay(60, ct); 
                if (_estopped) break;
            }

            IsRunning = false;
        }

        public void EStop()
        {
            _estopped = true;
            _cts?.Cancel(); 
            IsRunning = false;
        }

        public void Stop()
        {
            _estopped = false;
            _cts?.Cancel();
            IsRunning = false;
            CurrentIndex = 0;
        }
    }
}
