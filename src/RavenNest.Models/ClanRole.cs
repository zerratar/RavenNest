﻿using System;

namespace RavenNest.Models
{
    public class ClanRole
    {
        public ClanRole() { }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int Cape { get; set; }
        public DateTime? Joined { get; set; }
    }
}
