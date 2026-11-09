namespace RavenNest.Blazor.Services
{
    /// <summary>
    ///     What players see a skill called. The skill stored as Health is shown as Vitality (2026-11-09): health is
    ///     its own pool now (Vitality levels plus armour), and a "Health 999" read as the health itself. Stored names,
    ///     links and leaderboard addresses keep Health.
    /// </summary>
    public static class SkillNames
    {
        public static string Display(string name) =>
            string.Equals(name, "Health", System.StringComparison.OrdinalIgnoreCase) ? "Vitality" : name;
    }
}
