﻿using System;

namespace RavenNest.DataModels
{
    public partial class ClanSkill : Entity<ClanSkill>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid clanId; public Guid ClanId { get => clanId; set => Set(ref clanId, value); }
        private Guid skillId; public Guid SkillId { get => skillId; set => Set(ref skillId, value); }
        private int level; public int Level { get => level; set => Set(ref level, value); }
        private decimal experience; public decimal Experience { get => experience; set => Set(ref experience, value); }
    }
}
