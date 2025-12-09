using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaserCutHMI.Prototype.ViewModels
{
    
    public interface ICache
    {
        bool TryGet<T>(string key, out T? value);
        void Set<T>(string key, T value, TimeSpan ttl);
    }

    public class MemoryCacheLite : ICache
    {
        private readonly Dictionary<string, (object Value, DateTime ExpiresAt)> _map = new();
        private readonly object _gate = new();

        public bool TryGet<T>(string key, out T? value)
        {
            value = default;
            lock (_gate)
            {
                if (_map.TryGetValue(key, out var entry))
                {
                    if (entry.ExpiresAt > DateTime.UtcNow)
                    {
                        value = (T)entry.Value;
                        return true;
                    }
                    else
                    {
                       
                        _map.Remove(key);
                    }
                }
            }
            return false;
        }

        public void Set<T>(string key, T value, TimeSpan ttl)
        {
            if (value == null) return;

            var entry = (Value: (object)value, ExpiresAt: DateTime.UtcNow.Add(ttl));
            lock (_gate)
            {
                _map[key] = entry;
            }
        }
    }
}
