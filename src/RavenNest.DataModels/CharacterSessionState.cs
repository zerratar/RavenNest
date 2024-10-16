﻿using System;
using System.Collections.Concurrent;

namespace RavenNest.DataModels
{
    public class CharacterSessionState
    {
        public Guid SessionId { get; set; }
        public Guid CharacterId { get; set; }
        public DateTime LastTaskUpdate { get; set; }
        public float SyncTime { get; set; }
        public ExpSkillGainCollection ExpGain { get; set; } = new ExpSkillGainCollection();
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Health { get; set; }
        public bool Compromised { get; set; }

        //Default to now, as it will be used as "first grab" 10s "ago"
        public DateTime LastExpUpdate { get; set; } = DateTime.UtcNow.AddSeconds(-10);
        public DateTime LastExpSaveRequest { get; set; } = DateTime.UtcNow.AddSeconds(-10);
        public DateTime LastStateUpdate { get; set; } = DateTime.UtcNow.AddSeconds(-10);
        public DateTime LastStateSaveRequest { get; set; } = DateTime.UtcNow.AddSeconds(-10);

        public DateTime LastEnchantmentTryAt { get; set; }
        //public DateTime EnchantmentCooldown { get; set; }
        public DateTime SailingRewardAttempted { get; set; }
    }

    public class SessionState
    {
        public float SyncTime { get; set; }
        public string ClientVersion { get; set; }
        public ConcurrentDictionary<Guid, NPCState> NPCStates { get; set; } = new ConcurrentDictionary<Guid, NPCState>();
        public DateTime LastStateRequest { get; set; }
        public DateTime LastExpRequest { get; set; }
        public bool IsConnectedToClient { get; set; }

        public readonly ConcurrentDictionary<string, object> Variables = new ConcurrentDictionary<string, object>();
        public object this[string varName]
        {
            get
            {
                if (Variables.TryGetValue(varName, out var v))
                    return v;
                return null;
            }
            set => Variables[varName] = value;
        }

        public T GetOrDefault<T>(string name)
        {
            if (Variables.TryGetValue(name, out var v))
                return (T)v;
            return default;
        }

        public class NPCState
        {
            public Guid NpcId { get; set; }
            public Guid InstanceId { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
            public int Health { get; set; }
        }
    }
    public class ExpSkillGainCollection
    {
        public ExpGain Attack { get; set; } = new ExpGain();
        public ExpGain Defense { get; set; } = new ExpGain();
        public ExpGain Strength { get; set; } = new ExpGain();
        public ExpGain Health { get; set; } = new ExpGain();
        public ExpGain Woodcutting { get; set; } = new ExpGain();
        public ExpGain Fishing { get; set; } = new ExpGain();
        public ExpGain Mining { get; set; } = new ExpGain();
        public ExpGain Crafting { get; set; } = new ExpGain();
        public ExpGain Cooking { get; set; } = new ExpGain();
        public ExpGain Farming { get; set; } = new ExpGain();
        public ExpGain Slayer { get; set; } = new ExpGain();
        public ExpGain Magic { get; set; } = new ExpGain();
        public ExpGain Ranged { get; set; } = new ExpGain();
        public ExpGain Sailing { get; set; } = new ExpGain();
        public ExpGain Healing { get; set; } = new ExpGain();
        public ExpGain Gathering { get; set; } = new ExpGain();
        public ExpGain Alchemy { get; set; } = new ExpGain();
    }

    public class ExpGain
    {
        public double Amount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastUpdate { get; set; }

        public double ExpPerHour
        {
            get
            {
                var elapsed = DateTime.UtcNow - StartTime;
                return Amount / elapsed.TotalHours;
            }
        }

        public void AddExperience(double amount)
        {
            if (StartTime <= DateTime.UnixEpoch)
                StartTime = DateTime.UtcNow;
            if (amount > 0)
                Amount += amount;
            LastUpdate = DateTime.UtcNow;
        }
    }
}
