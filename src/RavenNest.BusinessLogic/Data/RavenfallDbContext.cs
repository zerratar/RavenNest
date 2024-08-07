﻿using Microsoft.EntityFrameworkCore;
using RavenNest.DataModels;
using System;

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

        public virtual DbSet<DailyAggregatedMarketplaceData> DailyAggregatedMarketplaceData { get; set; }
        public virtual DbSet<DailyAggregatedEconomyReport> DailyAggregatedEconomyReport { get; set; }

        public virtual DbSet<ExpMultiplierEvent> ExpMultiplierEvent { get; set; }
        public virtual DbSet<UserPatreon> UserPatreon { get; set; }
        public virtual DbSet<UserNotification> UserNotification { get; set; }
        public virtual DbSet<UserProperty> UserProperty { get; set; }
        public virtual DbSet<UserLoyalty> UserLoyalty { get; set; }
        public virtual DbSet<Pet> Pet { get; set; }
        public virtual DbSet<RedeemableItem> RedeemableItem { get; set; }
        public virtual DbSet<ItemRecipe> ItemRecipe { get; set; }
        public virtual DbSet<ItemRecipeIngredient> ItemRecipeIngredient { get; set; }
        public virtual DbSet<UserLoyaltyRank> UserLoyaltyRank { get; set; }
        public virtual DbSet<UserLoyaltyReward> UserLoyaltyReward { get; set; }
        public virtual DbSet<UserAccess> UserAccess { get; set; }
        public virtual DbSet<UserClaimedLoyaltyReward> UserClaimedLoyaltyReward { get; set; }
        public virtual DbSet<ServerSettings> ServerSettings { get; set; }

        public virtual DbSet<ItemDrop> ItemDrop { get; set; }
        public virtual DbSet<ResourceItemDrop> ResourceItemDrop { get; set; }
        public virtual DbSet<PatreonSettings> PatreonSettings { get; set; }
        public virtual DbSet<SyntyAppearance> SyntyAppearance { get; set; }
        public virtual DbSet<Character> Character { get; set; }
        public virtual DbSet<CharacterState> CharacterState { get; set; }
        public virtual DbSet<Village> Village { get; set; }
        public virtual DbSet<VillageHouse> VillageHouse { get; set; }
        public virtual DbSet<Clan> Clan { get; set; }
        public virtual DbSet<ClanRole> ClanRole { get; set; }
        public virtual DbSet<ClanRolePermissions> ClanRolePermissions { get; set; }
        public virtual DbSet<CharacterClanSkillCooldown> CharacterClanSkillCooldown { get; set; }
        public virtual DbSet<ClanSkill> ClanSkill { get; set; }
        public virtual DbSet<MarketItemTransaction> MarketItemTransaction { get; set; }
        public virtual DbSet<VendorTransaction> VendorTransaction { get; set; }
        public virtual DbSet<CharacterClanMembership> CharacterClanMembership { get; set; }
        public virtual DbSet<CharacterClanInvite> CharacterClanInvite { get; set; }
        public virtual DbSet<CharacterSessionActivity> CharacterSessionActivity { get; set; }

        //public virtual DbSet<CharacterSession> CharacterSession { get; set; }
        public virtual DbSet<GameSession> GameSession { get; set; }
        public virtual DbSet<GameEvent> GameEvent { get; set; }
        public virtual DbSet<InventoryItem> InventoryItem { get; set; }

        public virtual DbSet<VendorItem> VendorItem { get; set; }
        public virtual DbSet<UserBankItem> UserBankItem { get; set; }
        public virtual DbSet<ItemAttribute> ItemAttribute { get; set; }
        //public virtual DbSet<MagicItemAttribute> InventoryItemAttribute { get; set; }


        public virtual DbSet<MarketItem> MarketItem { get; set; }

        public virtual DbSet<ItemCraftingRequirement> ItemCraftingRequirement { get; set; }
        public virtual DbSet<Item> Item { get; set; }
        public virtual DbSet<NPC> NPC { get; set; }
        public virtual DbSet<NPCItemDrop> NPCItemDrop { get; set; }
        public virtual DbSet<NPCSpawn> NPCSpawn { get; set; }
        public virtual DbSet<Resources> Resources { get; set; }
        public virtual DbSet<Statistics> Statistics { get; set; }
        public virtual DbSet<Skills> Skills { get; set; }

        public virtual DbSet<CharacterSkillRecord> CharacterSkillRecord { get; set; }
        public virtual DbSet<Skill> Skill { get; set; }
        public virtual DbSet<User> User { get; set; }
        public virtual DbSet<ServerLogs> ServerLogs { get; set; }
        public virtual DbSet<GameClient> GameClient { get; set; }
        public virtual DbSet<Agreements> Agreements { get; set; }
        public virtual DbSet<CharacterStatusEffect> CharacterStatusEffect { get; set; }
        public virtual DbSet<ItemStatusEffect> ItemStatusEffect { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pet>(entity =>
            {
                entity.Property(e => e.Type).HasConversion(v => (int)v, v => (PetType)v);
                entity.Property(e => e.Tier).HasConversion(v => (int)v, v => (PetTier)v);
                entity.Property(e => e.DateOfBirth).HasColumnType("datetime");
                entity.Property(e => e.PlayTime).HasConversion<long>();
            });

            modelBuilder.Entity<DailyAggregatedMarketplaceData>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<CharacterStatusEffect>(entity =>
            {
                entity.Property(e => e.Type).HasConversion(v => (int)v, v => (StatusEffectType)v);
                entity.Property(e => e.StartUtc).HasColumnType("datetime");
                entity.Property(e => e.ExpiresUtc).HasColumnType("datetime");
                entity.Property(e => e.LastUpdateUtc).HasColumnType("datetime");
                entity.Property(e => e.Duration).HasDefaultValue(0);
                entity.Property(e => e.TimeLeft).HasDefaultValue(0);
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<ItemStatusEffect>(entity =>
            {
                entity.Property(e => e.Type).HasConversion(v => (int)v, v => (StatusEffectType)v);
                entity.Property(e => e.Island).HasConversion(v => v == null ? -1 : (int?)v.Value, v => v == null ? (Island?)null : (Island?)v.Value);
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<ItemRecipe>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<ItemRecipeIngredient>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<ServerSettings>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<DailyAggregatedEconomyReport>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<PatreonSettings>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<ItemDrop>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<ResourceItemDrop>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<Agreements>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserProperty>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserClaimedLoyaltyReward>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserAccess>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<RedeemableItem>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserLoyalty>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserLoyaltyRank>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserLoyaltyReward>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserPatreon>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserNotification>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<ExpMultiplierEvent>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<CharacterSessionActivity>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
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
            modelBuilder.Entity<ClanRolePermissions>(e => e.Property(x => x.Id).ValueGeneratedNever());
            modelBuilder.Entity<CharacterClanSkillCooldown>(e => e.Property(x => x.Id).ValueGeneratedNever());

            modelBuilder.Entity<ClanSkill>(e => e.Property(x => x.Id).ValueGeneratedNever());

            modelBuilder.Entity<MarketItemTransaction>(e => e.Property(x => x.Id).ValueGeneratedNever());
            modelBuilder.Entity<VendorTransaction>(e => e.Property(x => x.Id).ValueGeneratedNever());

            modelBuilder.Entity<CharacterClanMembership>(e => e.Property(x => x.Id).ValueGeneratedNever());
            modelBuilder.Entity<CharacterClanInvite>(e => e.Property(x => x.Id).ValueGeneratedNever());

            modelBuilder.Entity<GameSession>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Started).HasColumnType("datetime");
                entity.Property(e => e.Refreshed).HasColumnType("datetime").IsRequired(false);
                entity.Property(e => e.Stopped).HasColumnType("datetime").IsRequired(false);
            });

            modelBuilder.Entity<MarketItem>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<InventoryItem>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<VendorItem>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<UserBankItem>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            //modelBuilder.Entity<MagicItemAttribute>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
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
            modelBuilder.Entity<CharacterSkillRecord>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<Skills>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<Skill>(entity => entity.Property(e => e.Id).ValueGeneratedNever());
            modelBuilder.Entity<Statistics>(entity => entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Created).HasColumnType("datetime");
            });
        }
    }
}
