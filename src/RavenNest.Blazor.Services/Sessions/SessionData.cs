using System;

namespace RavenNest.Sessions
{
    public class SessionData
    {
        public System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> Data { get; }
            = new System.Collections.Concurrent.ConcurrentDictionary<string, byte[]>();
        public DateTime Created { get; } = DateTime.UtcNow;
        public DateTime LastModified { get; private set; }
        public DateTime LastAccessed { get; private set; }
        public string GetString(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                    return null;
                if (!Data.TryGetValue(key, out var data))
                    return null;
                return System.Text.Encoding.UTF8.GetString(data);
            }
            finally
            {
                LastAccessed = DateTime.UtcNow;
            }
        }

        public void SetString(string key, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                    return;

                var bytes = new byte[0];
                if (!string.IsNullOrEmpty(value))
                    bytes = System.Text.Encoding.UTF8.GetBytes(value);
                Data[key] = bytes;
            }
            finally
            {
                LastAccessed = DateTime.UtcNow;
                LastModified = DateTime.UtcNow;
            }
        }

        internal void Clear()
        {
            Data.Clear();
            LastAccessed = DateTime.UtcNow;
            LastModified = DateTime.UtcNow;
        }
    }
}
