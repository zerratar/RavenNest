﻿using RavenNest.Models;
using System;

namespace RavenNest.Sessions
{
    public class SessionData
    {
        public System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> Data { get; }
            = new System.Collections.Concurrent.ConcurrentDictionary<string, byte[]>();
        public DateTime Created { get; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; }
        public DateTime LastAccessed { get; set; }

        public SessionInfo SessionInfo { get; set; }
        public string Id { get; set; }

        public string this[string key]
        {
            get => GetString(key);
            set => SetString(key, value);
        }

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

                var bytes = Array.Empty<byte>();
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

        public void Clear()
        {
            Data.Clear();
            SessionInfo = null;
            LastAccessed = DateTime.UtcNow;
            LastModified = DateTime.UtcNow;
        }
    }
}
