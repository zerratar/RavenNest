using System;
using System.Collections.Generic;

namespace RavenNest.Models.TcpApi
{
    public class AuthenticationRequest
    {
        public string SessionToken { get; set; }
    }

    public class CharacterUpdate
    {
        // Starting with Guid seem to kill things?
        public Guid CharacterId { get; set; }
        public short Health { get; set; }
        public Island Island { get; set; }
        public CharacterState State { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public SkillUpdate[] Skills
        {
            get; set;
        }
    }

    public class SkillUpdate
    {
        public byte Index { get; set; }
        public short Level { get; set; }
        public double Experience { get; set; }
    }

    public enum Island : byte
    {
        Ferry = 0,
        Home = 1,
        Away = 2,
        Ironhill = 3,
        Kyo = 4,
        Heim = 5
    }

    public enum CharacterState : byte
    {
        None = 0,
        Raid = 1,
        Arena = 2,
        Dungeon = 3,
        Onsen = 4,
        Duel = 5,
        StreamRaidWar = 6,
    }
}
