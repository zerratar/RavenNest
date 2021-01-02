﻿using Microsoft.EntityFrameworkCore;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public partial class RavenfallDbContext : DbContext
    {
        private readonly string connectionString;

        public RavenfallDbContext(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public RavenfallDbContext(DbContextOptions<RavenfallDbContext> options)
            : base(options)
        {
        }
        public virtual DbSet<ExpMultiplierEvent> ExpMultiplierEvent { get; set; }
        public virtual DbSet<UserPatreon> UserPatreon { get; set; }
        public virtual DbSet<UserNotification> UserNotification { get; set; }
        public virtual DbSet<UserLoyalty> UserLoyalty { get; set; }
        public virtual DbSet<UserLoyaltyRank> UserLoyaltyRank { get; set; }
        public virtual DbSet<UserLoyaltyReward> UserLoyaltyReward { get; set; }
        public virtual DbSet<UserClaimedLoyaltyReward> UserClaimedLoyaltyReward { get; set; }

        public virtual DbSet<Appearance> Appearance { get; set; }
        public virtual DbSet<SyntyAppearance> SyntyAppearance { get; set; }
        public virtual DbSet<Character> Character { get; set; }
        public virtual DbSet<CharacterState> CharacterState { get; set; }

        public virtual DbSet<Village> Village { get; set; }
        public virtual DbSet<VillageHouse> VillageHouse { get; set; }
        public virtual DbSet<Clan> Clan { get; set; }
        public virtual DbSet<ClanRole> ClanRole { get; set; }
        public virtual DbSet<ClanSkill> ClanSkill { get; set; }
        public virtual DbSet<CharacterClanMembership> CharacterClanMembership { get; set; }
        public virtual DbSet<CharacterClanInvite> CharacterClanInvite { get; set; }
        public virtual DbSet<CharacterSessionActivity> CharacterSessionActivity { get; set; }

        //public virtual DbSet<CharacterSession> CharacterSession { get; set; }
        public virtual DbSet<GameSession> GameSession { get; set; }
        public virtual DbSet<GameEvent> GameEvent { get; set; }
        public virtual DbSet<InventoryItem> InventoryItem { get; set; }
        public virtual DbSet<ItemAttribute> ItemAttribute { get; set; }
        public virtual DbSet<InventoryItemAttribute> InventoryItemAttribute { get; set; }


        public virtual DbSet<MarketItem> MarketItem { get; set; }

        public virtual DbSet<ItemCraftingRequirement> ItemCraftingRequirement { get; set; }
        public virtual DbSet<Item> Item { get; set; }
        public virtual DbSet<NPC> NPC { get; set; }
        public virtual DbSet<NPCItemDrop> NPCItemDrop { get; set; }
        public virtual DbSet<NPCSpawn> NPCSpawn { get; set; }
        public virtual DbSet<Resources> Resources { get; set; }
        public virtual DbSet<Statistics> Statistics { get; set; }
        public virtual DbSet<Skills> Skills { get; set; }
        public virtual DbSet<Skill> Skill { get; set; }
        public virtual DbSet<User> User { get; set; }
        public virtual DbSet<ServerLogs> ServerLogs { get; set; }
        public virtual DbSet<GameClient> GameClient { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserClaimedLoyaltyReward>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserLoyalty>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserLoyaltyRank>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserLoyaltyReward>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserPatreon>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserNotification>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<ExpMultiplierEvent>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<CharacterSessionActivity>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<Appearance>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<SyntyAppearance>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<ServerLogs>(entity => entity.Property<ServerLogSeverity>(x => x.Severity).HasConversion<int>());

            modelBuilder.Entity<CharacterState>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<Character>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Created).HasColumnType("datetime");
                entity.Property(e => e.Name).IsRequired();
            });

            modelBuilder.Entity<GameEvent>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<GameClient>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<NPC>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<NPCItemDrop>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<NPCSpawn>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<Village>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<VillageHouse>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<Clan>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<ClanRole>(e => e.Property(x => x.Id).ValueGeneratedNever());
            modelBuilder.Entity<ClanSkill>(e => e.Property(x => x.Id).ValueGeneratedNever());            
            modelBuilder.Entity<CharacterClanMembership>(e => e.Property(x => x.Id).ValueGeneratedNever());
            modelBuilder.Entity<CharacterClanInvite>(e => e.Property(x => x.Id).ValueGeneratedNever());

            modelBuilder.Entity<GameSession>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Started).HasColumnType("datetime");
                entity.Property(e => e.Stopped).HasColumnType("datetime").IsRequired(false);
            });

            modelBuilder.Entity<MarketItem>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<InventoryItem>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<InventoryItemAttribute>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<ItemAttribute>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<ItemCraftingRequirement>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<Item>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.FemaleModelId).HasMaxLength(50);
                entity.Property(e => e.MaleModelId).HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            });

            modelBuilder.Entity<Resources>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<Skills>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<Skill>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<Statistics>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Created).HasColumnType("datetime");
                entity.Property(e => e.UserId).IsRequired();
            });
        }
    }
}
