using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     An item held by a clan rather than by a player.
    ///
    ///     This was a byte for byte copy of <see cref="UserBankItem"/>, first field included, so the
    ///     owning column said userId and a row could not name the clan that held it. Nothing has
    ///     ever referenced the type, there is no DbSet for it and there is no table behind it, which
    ///     is why nothing caught it and also why correcting it now costs nothing: there is no data
    ///     to migrate.
    ///
    ///     The table now exists, created by sql/clan-bank.sql, which had to be run by hand
    ///     because this project has no EF migrations and nothing reconciles the schema at startup.
    ///     GameData loads every set eagerly at boot, so the DbSet could not be added until the
    ///     table was there: it would have taken the server down rather than degrading.
    /// </summary>
    public partial class ClanBankItem : Entity<ClanBankItem>
    {
        [PersistentData] private Guid clanId;
        [PersistentData] private Guid itemId;
        [PersistentData] private long amount;
        [PersistentData] private string name;
        [PersistentData] private string enchantment;
        [PersistentData] private string tag;
        [PersistentData] private bool soulbound;
        [PersistentData] private Guid? transmogrificationId;
        [PersistentData] private int flags;
    }
}
