using Microsoft.EntityFrameworkCore;
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

        public virtual DbSet<Appearance> Appearance { get; set; }
        public virtual DbSet<SyntyAppearance> SyntyAppearance { get; set; }
        public virtual DbSet<Character> Character { get; set; }
        public virtual DbSet<CharacterState> CharacterState { get; set; }

        public virtual DbSet<Village> Village { get; set; }
        public virtual DbSet<VillageHouse> VillageHouse { get; set; }
        public virtual DbSet<Clan> Clan { get; set; }

        //public virtual DbSet<CharacterSession> CharacterSession { get; set; }
        public virtual DbSet<GameSession> GameSession { get; set; }
        public virtual DbSet<GameEvent> GameEvent { get; set; }
        public virtual DbSet<InventoryItem> InventoryItem { get; set; }
        public virtual DbSet<MarketItem> MarketItem { get; set; }

        public virtual DbSet<ItemCraftingRequirement> ItemCraftingRequirement { get; set; }
        public virtual DbSet<Item> Item { get; set; }
        public virtual DbSet<Resources> Resources { get; set; }
        public virtual DbSet<Statistics> Statistics { get; set; }
        public virtual DbSet<Skills> Skills { get; set; }
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
            modelBuilder.Entity<Appearance>(entity =>
                entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<SyntyAppearance>(entity =>
                entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<ServerLogs>(entity =>
                    entity.Property<ServerLogSeverity>(x => x.Severity).HasConversion<int>());

            modelBuilder.Entity<CharacterState>(entity =>
                entity.Property(e => e.Id).ValueGeneratedNever());

            modelBuilder.Entity<Character>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Created).HasColumnType("datetime");

                entity.Property(e => e.Name).IsRequired();

                //entity.HasOne(d => d.State)
                //    .WithMany(p => p.Character)
                //    .HasForeignKey(d => d.StateId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_Character_CharacterState");

                //entity.HasOne(d => d.Appearance)
                //    .WithMany(p => p.Character)
                //    .HasForeignKey(d => d.AppearanceId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_Character_Appearance");

                //entity.HasOne(d => d.SyntyAppearance)
                //    .WithMany(p => p.Character)
                //    .HasForeignKey(d => d.SyntyAppearanceId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_Character_SyntyAppearance");


                //entity.HasOne(d => d.Statistics)
                //    .WithMany(p => p.Character)
                //    .HasForeignKey(d => d.StatisticsId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_Character_Statistics");


                //entity.HasOne(d => d.OriginUser)
                //    .WithMany(p => p.CharacterOriginUser)
                //    .HasForeignKey(d => d.OriginUserId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_Character_User1");

                //entity.HasOne(d => d.Resources)
                //    .WithMany(p => p.Character)
                //    .HasForeignKey(d => d.ResourcesId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_Character_Resources");

                //entity.HasOne(d => d.Skills)
                //    .WithMany(p => p.Character)
                //    .HasForeignKey(d => d.SkillsId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_Character_Skills");

                //entity.HasOne(d => d.User)
                //    .WithMany(p => p.CharacterUser)
                //    .HasForeignKey(d => d.UserId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_Character_User");
            });

            //modelBuilder.Entity<CharacterSession>(entity =>
            //{
            //    entity.Property(e => e.Id).ValueGeneratedNever();

            //    entity.Property(e => e.Started).HasColumnType("datetime");

            //    entity.Property(e => e.Ended).HasColumnType("datetime");

            //    entity.HasOne(d => d.Character)
            //        .WithMany(p => p.CharacterSession)
            //        .HasForeignKey(d => d.CharacterId)
            //        .OnDelete(DeleteBehavior.ClientSetNull)
            //        .HasConstraintName("FK_CharacterSession_Character");

            //    entity.HasOne(d => d.Session)
            //        .WithMany(p => p.CharacterSession)
            //        .HasForeignKey(d => d.SessionId)
            //        .OnDelete(DeleteBehavior.ClientSetNull)
            //        .HasConstraintName("FK_CharacterSession_GameSession");
            //});

            modelBuilder.Entity<GameEvent>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                //entity.HasOne(d => d.GameSession)
                //    .WithMany(p => p.GameEvents)
                //    .HasForeignKey(d => d.GameSessionId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_GameEvent_GameSession");
            });

            modelBuilder.Entity<GameClient>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Village>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<VillageHouse>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Clan>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<GameSession>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Started).HasColumnType("datetime");

                entity.Property(e => e.Stopped).HasColumnType("datetime").IsRequired(false);

                //entity.HasOne(d => d.User)
                //    .WithMany(p => p.GameSession)
                //    .HasForeignKey(d => d.UserId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_GameSession_User");
            });

            modelBuilder.Entity<MarketItem>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                //entity.HasOne(d => d.SellerCharacter)
                //    .WithMany(p => p.MarketItem)
                //    .HasForeignKey(d => d.SellerCharacterId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_MarketItem_Character");

                //entity.HasOne(d => d.Item)
                //    .WithMany(p => p.MarketItem)
                //    .HasForeignKey(d => d.ItemId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_MarketItem_Item");
            });


            modelBuilder.Entity<InventoryItem>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                //entity.HasOne(d => d.Character)
                //    .WithMany(p => p.InventoryItem)
                //    .HasForeignKey(d => d.CharacterId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_InventoryItem_Character");

                //entity.HasOne(d => d.Item)
                //    .WithMany(p => p.InventoryItem)
                //    .HasForeignKey(d => d.ItemId)
                //    .OnDelete(DeleteBehavior.ClientSetNull)
                //    .HasConstraintName("FK_InventoryItem_Item");
            });


            modelBuilder.Entity<ItemCraftingRequirement>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Item>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.FemaleModelId).HasMaxLength(50);

                entity.Property(e => e.MaleModelId).HasMaxLength(50);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<Resources>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Skills>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Statistics>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();

                entity.Property(e => e.Created).HasColumnType("datetime");

                entity.Property(e => e.UserId).IsRequired();
            });
        }
    }
}
