using System;

namespace RavenNest.Models
{
    public class Clan
    {
        public Guid Id { get; set; }
        public string Owner { get; set; }
        public string Name { get; set; }
        public string Logo { get; set; }
    }
}