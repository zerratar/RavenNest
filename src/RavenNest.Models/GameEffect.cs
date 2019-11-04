using System;
using System.Collections.Generic;
using System.Text;

namespace RavenNest.Models
{
    public enum GameEffectTarget
    {
        Self,
        SelfPet,
        Friendly,
        FriendlyPet,
        Enemy,
        EnemyPet
    }

    public class Ability
    {
        public Guid Id { get; set; }
        public int RequiredSkill { get; set; }
        public int RequiredSkillLevel { get; set; }
        public int Cost { get; set; }        
        public GameEffect Effect { get; set; }
    }

    public class GameEffect
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public GameEffectTarget Target { get; set; }
        public float Radius { get; set; }
    }
}
