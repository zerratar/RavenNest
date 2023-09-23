using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using MessagePack;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Game.Processors.Tasks;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public partial class GameData : IDisposable
    {
        #region Settings
        private const int BackupInterval = 60 * 60 * 1000; // once per hour
        private const int SaveInterval = 10000;
        private const int SaveMaxBatchSize = 50;
        public const float SessionTimeoutSeconds = 1f;
        #endregion

        #region Private members

        private readonly IRavenfallDbContextProvider db;
        private readonly ITcpSocketApiConnectionProvider tcpConnectionProvider;
        private readonly ILogger logger;
        private readonly IKernel kernel;
        private readonly IQueryBuilder queryBuilder;

        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, CharacterSessionState>> characterSessionStates = new();

        private readonly ConcurrentDictionary<Guid, SessionState> sessionStates = new();

        private readonly EntitySet<ServerSettings> serverSettings;

        private readonly EntitySet<Agreements> agreements;
        private readonly EntitySet<DailyAggregatedMarketplaceData> dailyAggregatedMarketplaceData;
        private readonly EntitySet<DailyAggregatedEconomyReport> dailyAggregatedEconomyReport;

        private readonly EntitySet<UserLoyalty> loyalty;
        private readonly EntitySet<UserProperty> userProperties;
        private readonly EntitySet<UserLoyaltyRank> loyaltyRanks;
        private readonly EntitySet<UserLoyaltyReward> loyaltyRewards;
        private readonly EntitySet<UserClaimedLoyaltyReward> claimedLoyaltyRewards;
        private readonly EntitySet<UserNotification> notifications;

        private readonly EntitySet<VendorItem> vendorItems;

        private readonly EntitySet<Achievement> achievements;
        private readonly EntitySet<UserAchievement> userAchievements;
        private readonly EntitySet<CharacterAchievement> characterAchievements;
        private readonly EntitySet<CharacterStatusEffect> characterStatusEffects;

        private readonly EntitySet<CharacterClanInvite> clanInvites;
        private readonly EntitySet<Clan> clans;
        private readonly EntitySet<ClanRole> clanRoles;
        private readonly EntitySet<ClanSkill> clanSkills;
        private readonly EntitySet<ClanRolePermissions> clanRolePermissions;
        private readonly EntitySet<CharacterClanSkillCooldown> characterClanSkillCooldown;

        private readonly EntitySet<MarketItemTransaction> marketTransactions;
        private readonly EntitySet<VendorTransaction> vendorTransaction;

        private readonly EntitySet<CharacterClanMembership> clanMemberships;

        private readonly EntitySet<UserPatreon> patreons;
        private readonly EntitySet<UserAccess> userAccess;
        private readonly EntitySet<CharacterSessionActivity> characterSessionActivities;
        private readonly EntitySet<SyntyAppearance> syntyAppearances;
        private readonly EntitySet<Character> characters;
        private readonly EntitySet<CharacterState> characterStates;
        private readonly EntitySet<GameSession> gameSessions;
        private readonly EntitySet<ExpMultiplierEvent> expMultiplierEvents;
        private readonly EntitySet<GameEvent> gameEvents;
        private readonly EntitySet<ItemStatusEffect> itemStatusEffects;
        private readonly EntitySet<Pet> pets;

        private readonly EntitySet<UserBankItem> userBankItems;
        private readonly EntitySet<InventoryItem> inventoryItems;
        private readonly EntitySet<ResourceItemDrop> resourceItemDrops;
        private readonly EntitySet<ItemDrop> itemDrops;

        private readonly EntitySet<ItemAttribute> itemAttributes;
        private readonly EntitySet<RedeemableItem> redeemableItems;

        private readonly EntitySet<ItemRecipe> itemRecipes;
        private readonly EntitySet<ItemRecipeIngredient> itemRecipeIngredients;

        private readonly EntitySet<MarketItem> marketItems;
        private readonly EntitySet<Item> items;
        private readonly EntitySet<NPC> npcs;
        private readonly EntitySet<NPCItemDrop> npcItemDrops;
        private readonly EntitySet<NPCSpawn> npcSpawns;
        private readonly EntitySet<ItemCraftingRequirement> itemCraftingRequirements;
        private readonly EntitySet<Resources> resources;
        private readonly EntitySet<Statistics> statistics;
        private readonly EntitySet<CharacterSkillRecord> characterSkillRecords;
        private readonly EntitySet<Skills> characterSkills;
        private readonly EntitySet<Skill> skills;
        private readonly EntitySet<PatreonSettings> patreonSettings;

        private readonly EntitySet<User> users;
        private readonly EntitySet<GameClient> gameClients;
        private readonly EntitySet<Village> villages;
        private readonly EntitySet<VillageHouse> villageHouses;

        private readonly IEntitySet[] entitySets;
        private readonly GameDataBackupProvider backupProvider;
        private ITimeoutHandle scheduleHandler;
        private ITimeoutHandle backupHandler;
        private TypedItems typedItems;
        private ObsoleteItems obsoleteItems;

        #endregion

        #region Public members
        public GameClient Client { get; private set; }
        public object SyncLock { get; } = new object();
        public bool InitializedSuccessful { get; } = false;

        public BotStats Bot { get; set; } = new BotStats();
        public PatreonSettings Patreon { get; set; }
        #endregion

        #region Game Data Construction

        public GameData(
            GameDataBackupProvider backupProvider,
            GameDataMigration dataMigration,
            IRavenfallDbContextProvider db,
            ILogger<GameData> logger,
            IKernel kernel,
            IQueryBuilder queryBuilder,
            ITcpSocketApiConnectionProvider tcpConnectionProvider)
        {
            try
            {
                this.db = db;
                this.logger = logger;
                this.kernel = kernel;
                this.queryBuilder = queryBuilder;
                this.backupProvider = backupProvider;
                this.tcpConnectionProvider = tcpConnectionProvider;

                var stopWatch = new Stopwatch();
                stopWatch.Start();

                #region Data Restoration
                IEntityRestorePoint restorePoint = backupProvider.GetRestorePoint(new[] {
                        typeof(UserLoyalty),
                        typeof(ClanRole),
                        typeof(Clan),
                        typeof(ClanRolePermissions),
                        typeof(CharacterClanSkillCooldown),
                        //typeof(CharacterClanInvite),
                        typeof(CharacterClanMembership),
                        typeof(CharacterSkillRecord),
                        typeof(ClanSkill),
                        typeof(UserClaimedLoyaltyReward),
                        typeof(UserPatreon),
                        typeof(UserProperty),
                        typeof(SyntyAppearance),
                        typeof(Character),
                        typeof(CharacterState),
                        typeof(InventoryItem),
                        typeof(Item),
                        typeof(User),
                        typeof(ItemAttribute),
                        typeof(RedeemableItem),
                        typeof(Pet),
                        typeof(ResourceItemDrop),
                        typeof(ItemDrop),
                        typeof(VendorItem),
                        //typeof(UserNotification),
                        typeof(MarketItemTransaction),
                        typeof(VendorTransaction),
                        typeof(UserAccess),
                        typeof(GameSession),
                        typeof(Village),
                        typeof(VillageHouse),
                        typeof(Resources),
                        typeof(Skills),
                        typeof(PatreonSettings),
                        typeof(Statistics),
                        typeof(MarketItem),
                        typeof(ItemCraftingRequirement),
                        typeof(CharacterSessionActivity),
                        typeof(Agreements),
                        typeof(ServerSettings),
                        typeof(UserBankItem),
                        typeof(ExpMultiplierEvent),
                        typeof(ItemRecipe),
                        typeof(ItemRecipeIngredient),
                        typeof(CharacterStatusEffect),
                        typeof(ItemStatusEffect)
                });


                logger.LogInformation($"Checking for restore points.");
                if (restorePoint != null)
                {
                    if (dataMigration.TryMigrate(this.db, restorePoint, out var migrated, out var failed))
                    {
                        // if all was good, clear whole restore point
                        backupProvider.ClearRestorePoint();
                    }
                    else
                    {
                        foreach (var migratedType in migrated)
                        {
                            backupProvider.ClearRestorePoint(migratedType);
                        }

                        var failedTypeNames = failed.Select(x => x.Name).ToArray();
                        var errorMessage = failedTypeNames.Length + " table(s) failed to migrate: " + string.Join(", ", failedTypeNames) + ". Server start interrupted to prevent dataloss.";
                        logger.LogError(errorMessage);
                        throw new DataMigrationException(errorMessage);
                    }
                }
                #endregion

                logger.LogInformation($"Loading dataset from database.");
                #region Data Load
                using (var ctx = this.db.Get())
                {
                    agreements = new EntitySet<Agreements>(restorePoint?.Get<Agreements>() ?? ctx.Agreements.ToList());

                    serverSettings = new EntitySet<ServerSettings>(restorePoint?.Get<ServerSettings>() ?? ctx.ServerSettings.ToList());

                    itemStatusEffects = new EntitySet<DataModels.ItemStatusEffect>(restorePoint?.Get<ItemStatusEffect>() ?? ctx.ItemStatusEffect.ToList());
                    itemStatusEffects.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    characterStatusEffects = new EntitySet<DataModels.CharacterStatusEffect>(restorePoint?.Get<CharacterStatusEffect>() ?? ctx.CharacterStatusEffect.ToList());
                    characterStatusEffects.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

                    itemDrops = new EntitySet<ItemDrop>(restorePoint?.Get<ItemDrop>() ?? ctx.ItemDrop.ToList());
                    itemDrops.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    resourceItemDrops = new EntitySet<ResourceItemDrop>(restorePoint?.Get<ResourceItemDrop>() ?? ctx.ResourceItemDrop.ToList());
                    resourceItemDrops.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    patreonSettings = new EntitySet<PatreonSettings>(restorePoint?.Get<PatreonSettings>() ?? ctx.PatreonSettings.ToList());

                    dailyAggregatedMarketplaceData = new EntitySet<DailyAggregatedMarketplaceData>(ctx.DailyAggregatedMarketplaceData.ToList());
                    dailyAggregatedEconomyReport = new EntitySet<DailyAggregatedEconomyReport>(ctx.DailyAggregatedEconomyReport.ToList());

                    loyalty = new EntitySet<UserLoyalty>(restorePoint?.Get<UserLoyalty>() ?? ctx.UserLoyalty.ToList());
                    loyalty.RegisterLookupGroup(nameof(User), x => x.UserId);
                    loyalty.RegisterLookupGroup("Streamer", x => x.StreamerUserId);

                    pets = new EntitySet<Pet>(restorePoint?.Get<Pet>() ?? ctx.Pet.ToList());
                    pets.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

                    itemRecipes = new EntitySet<DataModels.ItemRecipe>(restorePoint?.Get<DataModels.ItemRecipe>() ?? ctx.ItemRecipe.ToList());
                    itemRecipes.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    itemRecipeIngredients = new EntitySet<DataModels.ItemRecipeIngredient>(restorePoint?.Get<DataModels.ItemRecipeIngredient>() ?? ctx.ItemRecipeIngredient.ToList());
                    itemRecipeIngredients.RegisterLookupGroup(nameof(ItemRecipe), x => x.RecipeId);
                    itemRecipeIngredients.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    redeemableItems = new EntitySet<RedeemableItem>(restorePoint?.Get<RedeemableItem>() ?? ctx.RedeemableItem.ToList());
                    redeemableItems.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    userProperties = new EntitySet<UserProperty>(restorePoint?.Get<UserProperty>() ?? ctx.UserProperty.ToList());
                    userProperties.RegisterLookupGroup(nameof(User), x => x.UserId);

                    loyaltyRanks = new EntitySet<UserLoyaltyRank>(ctx.UserLoyaltyRank.ToList());

                    loyaltyRewards = new EntitySet<UserLoyaltyReward>(ctx.UserLoyaltyReward.ToList());
                    //loyaltyRewards.RegisterLookupGroup(nameof(UserLoyaltyRank), x => x.RankId);

                    userAccess = new EntitySet<UserAccess>(restorePoint?.Get<UserAccess>() ?? ctx.UserAccess.ToList());
                    userAccess.RegisterLookupGroup(nameof(User), x => x.UserId);

                    claimedLoyaltyRewards = new EntitySet<UserClaimedLoyaltyReward>(restorePoint?.Get<UserClaimedLoyaltyReward>() ?? ctx.UserClaimedLoyaltyReward.ToList());
                    claimedLoyaltyRewards.RegisterLookupGroup(nameof(User), x => x.UserId);
                    claimedLoyaltyRewards.RegisterLookupGroup(nameof(UserLoyaltyReward), x => x.RewardId);
                    claimedLoyaltyRewards.RegisterLookupGroup(nameof(Character), x => x.CharacterId.GetValueOrDefault());

                    characterSessionActivities = new EntitySet<CharacterSessionActivity>(restorePoint?.Get<CharacterSessionActivity>() ?? ctx.CharacterSessionActivity.ToList());
                    characterSessionActivities.RegisterLookupGroup(nameof(GameSession), x => x.SessionId);
                    characterSessionActivities.RegisterLookupGroup(nameof(Character), x => x.CharacterId);
                    characterSessionActivities.RegisterLookupGroup(nameof(User), x => x.UserId);

                    clanInvites = new EntitySet<CharacterClanInvite>(
                        restorePoint?.Get<CharacterClanInvite>() ?? ctx.CharacterClanInvite.ToList());
                    clanInvites.RegisterLookupGroup(nameof(Clan), x => x.ClanId);
                    clanInvites.RegisterLookupGroup(nameof(Character), x => x.CharacterId);
                    clanInvites.RegisterLookupGroup(nameof(User), x => x.InviterUserId.GetValueOrDefault());

                    patreons = new EntitySet<UserPatreon>(restorePoint?.Get<UserPatreon>() ?? ctx.UserPatreon.ToList());
                    patreons.RegisterLookupGroup(nameof(User), x => x.UserId.GetValueOrDefault());

                    notifications = new EntitySet<UserNotification>(
                        restorePoint?.Get<UserNotification>() ??
                        ctx.UserNotification.ToList());
                    notifications.RegisterLookupGroup(nameof(User), x => x.UserId);

                    expMultiplierEvents = new EntitySet<ExpMultiplierEvent>(
                        ctx.ExpMultiplierEvent.ToList());

                    syntyAppearances = new EntitySet<SyntyAppearance>(restorePoint?.Get<SyntyAppearance>() ?? ctx.SyntyAppearance.ToList());
                    characters = new EntitySet<Character>(restorePoint?.Get<Character>() ?? ctx.Character.ToList());

                    characters.RegisterLookupGroup(nameof(User), x => x.UserId);
                    characters.RegisterLookupGroup(nameof(GameSession), x => x.UserIdLock.GetValueOrDefault());

                    characterStates = new EntitySet<CharacterState>(restorePoint?.Get<CharacterState>() ?? ctx.CharacterState.ToList());
                    gameSessions = new EntitySet<GameSession>(restorePoint?.Get<GameSession>() ?? ctx.GameSession.ToList());
                    gameSessions.RegisterLookupGroup(nameof(User), x => x.UserId);

                    // we can still store the game events, but no need to load them on startup as the DB will quickly be filled.
                    // and take a long time to load
                    gameEvents = new EntitySet<GameEvent>(new List<GameEvent>(), false);
                    //gameEvents.RegisterLookupGroup(nameof(GameSession), x => x.GameSessionId);
                    gameEvents.RegisterLookupGroup(nameof(User), x => x.UserId);

                    userBankItems = new EntitySet<UserBankItem>(restorePoint?.Get<UserBankItem>() ?? ctx.UserBankItem.ToList());
                    userBankItems.RegisterLookupGroup(nameof(User), x => x.UserId);

                    vendorItems = new EntitySet<VendorItem>(restorePoint?.Get<VendorItem>() ?? ctx.VendorItem.ToList());
                    vendorItems.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    inventoryItems = new EntitySet<InventoryItem>(restorePoint?.Get<InventoryItem>() ?? ctx.InventoryItem.ToList());
                    inventoryItems.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

                    itemAttributes = new EntitySet<ItemAttribute>(restorePoint?.Get<ItemAttribute>() ?? ctx.ItemAttribute.ToList());

                    marketItems = new EntitySet<MarketItem>(restorePoint?.Get<MarketItem>() ?? ctx.MarketItem.ToList());
                    marketItems.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    items = new EntitySet<Item>(restorePoint?.Get<Item>() ?? ctx.Item.ToList());

                    npcs = new EntitySet<NPC>(ctx.NPC.ToList());
                    npcSpawns = new EntitySet<NPCSpawn>(ctx.NPCSpawn.ToList());
                    npcSpawns.RegisterLookupGroup(nameof(NPC), x => x.NpcId);

                    npcItemDrops = new EntitySet<NPCItemDrop>(ctx.NPCItemDrop.ToList());
                    npcItemDrops.RegisterLookupGroup(nameof(NPC), x => x.NpcId);

                    itemCraftingRequirements = new EntitySet<ItemCraftingRequirement>(
                        restorePoint?.Get<ItemCraftingRequirement>() ??
                        ctx.ItemCraftingRequirement.ToList());
                    itemCraftingRequirements.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    clans = new EntitySet<Clan>(restorePoint?.Get<Clan>() ?? ctx.Clan.ToList());
                    clans.RegisterLookupGroup(nameof(User), x => x.UserId);

                    clanRoles = new EntitySet<ClanRole>(restorePoint?.Get<ClanRole>() ?? ctx.ClanRole.ToList());
                    clanRoles.RegisterLookupGroup(nameof(Clan), x => x.ClanId);

                    clanRolePermissions = new EntitySet<ClanRolePermissions>(restorePoint?.Get<ClanRolePermissions>() ?? ctx.ClanRolePermissions.ToList());
                    clanRolePermissions.RegisterLookupGroup(nameof(ClanRole), x => x.ClanRoleId);

                    characterClanSkillCooldown = new EntitySet<CharacterClanSkillCooldown>(restorePoint?.Get<CharacterClanSkillCooldown>() ?? ctx.CharacterClanSkillCooldown.ToList());
                    characterClanSkillCooldown.RegisterLookupGroup(nameof(Character), x => x.CharacterId);
                    characterClanSkillCooldown.RegisterLookupGroup(nameof(Skill), x => x.SkillId);

                    clanMemberships = new EntitySet<CharacterClanMembership>(
                        restorePoint?.Get<CharacterClanMembership>() ?? ctx.CharacterClanMembership.ToList());
                    clanMemberships.RegisterLookupGroup(nameof(Clan), x => x.ClanId);
                    clanMemberships.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

                    villages = new EntitySet<Village>(restorePoint?.Get<Village>() ?? ctx.Village.ToList());
                    villages.RegisterLookupGroup(nameof(User), x => x.UserId);

                    villageHouses = new EntitySet<VillageHouse>(
                        restorePoint?.Get<VillageHouse>() ??
                        ctx.VillageHouse.ToList());

                    villageHouses.RegisterLookupGroup(nameof(Village), x => x.VillageId);

                    resources = new EntitySet<Resources>(
                        restorePoint?.Get<Resources>() ??
                        ctx.Resources.ToList());

                    statistics = new EntitySet<Statistics>(
                        restorePoint?.Get<Statistics>() ??
                        ctx.Statistics.ToList());

                    characterSkillRecords = new EntitySet<CharacterSkillRecord>(
                        restorePoint?.Get<CharacterSkillRecord>() ??
                        ctx.CharacterSkillRecord.ToList());

                    characterSkillRecords.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

                    characterSkills = new EntitySet<Skills>(restorePoint?.Get<Skills>() ?? ctx.Skills.ToList());
                    clanSkills = new EntitySet<ClanSkill>(restorePoint?.Get<ClanSkill>() ?? ctx.ClanSkill.ToList());
                    clanSkills.RegisterLookupGroup(nameof(Clan), x => x.ClanId);

                    vendorTransaction = new EntitySet<VendorTransaction>(
                        restorePoint?.Get<VendorTransaction>() ??
                        ctx.VendorTransaction.ToList());
                    vendorTransaction.RegisterLookupGroup(nameof(Item), x => x.ItemId);
                    vendorTransaction.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

                    marketTransactions = new EntitySet<MarketItemTransaction>(restorePoint?.Get<MarketItemTransaction>() ?? ctx.MarketItemTransaction.ToList());
                    marketTransactions.RegisterLookupGroup(nameof(Item), x => x.ItemId);
                    marketTransactions.RegisterLookupGroup(nameof(Character) + "Seller", x => x.SellerCharacterId);
                    marketTransactions.RegisterLookupGroup(nameof(Character) + "Buyer", x => x.BuyerCharacterId);


                    skills = new EntitySet<Skill>(ctx.Skill.ToList());
                    users = new EntitySet<User>(restorePoint?.Get<User>() ?? ctx.User.ToList());
                    gameClients = new EntitySet<GameClient>(ctx.GameClient.ToList());

                    Client = gameClients.Entities.First();
                    Patreon = patreonSettings.Entities.FirstOrDefault();

                    entitySets = new IEntitySet[]
                    {
                        redeemableItems,
                        serverSettings,
                        itemAttributes,
                        dailyAggregatedMarketplaceData,
                        dailyAggregatedEconomyReport,
                        pets,
                        patreons, loyalty, loyaltyRewards, loyaltyRanks, claimedLoyaltyRewards,
                        expMultiplierEvents, notifications,
                        syntyAppearances, characters, characterStates,
                        userProperties, vendorTransaction,
                        userBankItems,
                        characterSkillRecords,
                        clanRolePermissions,
                        characterClanSkillCooldown,
                        patreonSettings,
                        resourceItemDrops,

                        itemRecipes, itemRecipeIngredients,
                        itemStatusEffects, characterStatusEffects,
                        gameClients,
                        userAccess,
                        vendorItems,
                        itemDrops,
                        items, // so we can update items
                        gameSessions, /*gameEvents, */ inventoryItems, marketItems, marketTransactions,
                        resources, statistics, characterSkills, clanSkills, users, villages, villageHouses,
                        clans, clanRoles, clanMemberships, clanInvites, agreements,
                        npcs, npcSpawns, npcItemDrops, itemCraftingRequirements, characterSessionActivities
                    };
                }
                #endregion

                #region Post Data Load - Transformations

                logger.LogInformation($"Post processing dataset.");

                MigrateTwitchUserAccess();
                RemoveCharactersWithoutSkills();

                //RemoveDanglingEntities();

                EnsureCharacterSkillRecords();
                EnsureMagicAttributes();
                //EnsureResources();

                //FixVillageLevels();
                //TransformExperience();
                //RemoveBadUsers(users);

                ProcessInventoryItems(inventoryItems);
                //RemoveEmptyPlayers();

                EnsureClanLevels(clans);
                EnsureExpMultipliersWithinBounds(expMultiplierEvents);
                EnsureCraftingRequirements(items);
                //MergeLoyaltyData(loyalty);

                MergeClans();

                RemoveDuplicatedClanMembers();

                //MergeAccounts();
                //UpgradeVillageLevels();
                //MergeVillages();

                MergeInventoryItems();
                ApplyVendorPrices();
                //ConvertUserResources();

                #endregion

                stopWatch.Stop();
                logger.LogInformation($"All database entries loaded in {stopWatch.Elapsed.TotalSeconds} seconds.");
                logger.LogInformation("GameData initialized... Starting kernel...");
                kernel.Start();
                InitializedSuccessful = true;
                CreateBackup();
                ScheduleNextSave();
            }
            catch (Exception exc)
            {
                InitializedSuccessful = false;
                System.IO.File.WriteAllText("ravenfall-error.log", "[" + DateTime.UtcNow + "] " + exc.ToString());
            }
        }

        public void ReturnMarketplaceItem(MarketItem i)
        {
            var c = GetCharacter(i.SellerCharacterId);
            if (c == null)
            {
                // character does no longer exist. we cant return to sender..
                Remove(i);
                return;
            }

            var existingStacks = GetInventoryItems(i.SellerCharacterId, i.ItemId).FirstOrDefault(x => string.IsNullOrEmpty(x.Enchantment));
            if (existingStacks != null)
            {
                existingStacks.Amount += i.Amount;
                Remove(i);
                return;
            }

            Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                CharacterId = i.SellerCharacterId,
                Enchantment = i.Enchantment,
                Flags = i.Flags,
                ItemId = i.ItemId,
                Name = i.Name,
                Tag = i.Tag,
                TransmogrificationId = i.TransmogrificationId,
                Amount = i.Amount
            });
        }

        public static DataModels.UserBankItem CreateBankItem(Guid userId, DataModels.InventoryItem item, long amount)
        {
            return new DataModels.UserBankItem
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Enchantment = item.Enchantment,
                Flags = item.Flags ?? 0,
                ItemId = item.ItemId,
                Name = item.Name,
                Soulbound = item.Soulbound,
                Tag = item.Tag,
                TransmogrificationId = item.TransmogrificationId,
                UserId = userId
            };
        }


        public void SendToStash(InventoryItem stack)
        {
            var character = GetCharacter(stack.CharacterId);
            if (character == null)
            {
                return;
            }

            var canBeStacked = PlayerInventory.CanBeStacked(stack);
            if (canBeStacked)
            {
                var bankItems = GetUserBankItems(character.UserId);
                var existing = bankItems.FirstOrDefault(x => PlayerInventory.CanBeStacked(x, stack));
                if (existing != null)
                {
                    existing.Amount += stack.Amount.GetValueOrDefault();
                    Remove(stack);
                    return;
                }
            }

            Add(CreateBankItem(character.UserId, stack, stack.Amount.GetValueOrDefault()));
        }

        private void ConvertUserResources()
        {
            var resDrops = GetResourceItemDrops();
            var miningDrops = resDrops.Where(x => x.Skill == (int)RavenNest.Models.Skill.Mining).ToList();
            var farmingDrops = resDrops.Where(x => x.Skill == (int)RavenNest.Models.Skill.Farming).ToList();
            var fishingDrops = resDrops.Where(x => x.Skill == (int)RavenNest.Models.Skill.Fishing).ToList();
            var woodcuttingDrops = resDrops.Where(x => x.Skill == (int)RavenNest.Models.Skill.Woodcutting).ToList();
            var count = 0;

            foreach (var i in GetMarketItems(obsoleteItems.OreIngot.Id))
            {
                ReturnMarketplaceItem(i);
            }

            foreach (var i in GetMarketItems(obsoleteItems.WoodPlank.Id))
            {
                ReturnMarketplaceItem(i);
            }

            foreach (var user in this.users.Entities)
            {
                count++;
                if (user.Resources == null)
                    continue;

                var chars = GetCharactersByUserId(user.Id);

                var miningLevel = 1;
                var fishingLevel = 1;
                var farmingLevel = 1;
                var woodcuttingLevel = 1;

                // move items from marketplace into inventory

                foreach (var c in chars)
                {
                    ConvertItemResource(c, miningLevel, obsoleteItems.OreIngot.Id, miningDrops);
                    ConvertItemResource(c, woodcuttingLevel, obsoleteItems.WoodPlank.Id, woodcuttingDrops);

                    var skills = GetCharacterSkills(c.SkillsId);
                    miningLevel = Math.Max(skills.MiningLevel, miningLevel);
                    fishingLevel = Math.Max(skills.FishingLevel, fishingLevel);
                    farmingLevel = Math.Max(skills.FarmingLevel, farmingLevel);
                    woodcuttingLevel = Math.Max(skills.WoodcuttingLevel, woodcuttingLevel);
                }


                var resources = GetResources(user.Resources.Value);
                if (resources == null || (resources.Ore == 0 && resources.Wood == 0 && resources.Wheat == 0 && resources.Fish == 0))
                    continue;

                // Based on the drop levels and time for them to be dropped
                // calculate how much items to add to player's stash.

                ConvertResource(user, miningLevel, () => resources.Ore, value => resources.Ore = value, miningDrops);
                ConvertResource(user, farmingLevel, () => resources.Wheat, value => resources.Wheat = value, farmingDrops);
                ConvertResource(user, woodcuttingLevel, () => resources.Wood, value => resources.Wood = value, woodcuttingDrops);
                ConvertResource(user, fishingLevel, () => resources.Fish, value => resources.Fish = value, fishingDrops);
            }
        }

        private void ConvertItemResource(
            Character c,
            int level,
            Guid itemId,
            List<ResourceItemDrop> drops)
        {

            var ofType = GetInventoryItems(c.Id, itemId);
            if (ofType == null || ofType.Count == 0)
            {
                return;
            }

            // 1. move to stash.
            foreach (var v in ofType)
            {
                SendToStash(v);
            }

            // 2. load from stash
            var items = GetUserBankItems(c.UserId);
            var bankItemsOfType = items.AsList(x => x.ItemId == itemId);
            if (bankItemsOfType.Count == 0) return;
            var totalCount = bankItemsOfType.Sum(x => x.Amount);
            if (totalCount == 0) return;

            var seconds = totalCount * 50d; // these are worth 10x of ores/wood, so instead of * 5, we do * 50
            if (seconds > 0 && seconds < 10) seconds = 10;

            var validDrops = drops
                .Where(x => x.LevelRequirement <= level && x.Cooldown.HasValue && x.Cooldown.Value > 0)
                .OrderBy(x => x.Cooldown)
                .ToList();

            // Create a dictionary for easy lookup and modification
            var stashItems = new Dictionary<Guid, int>();

            // Calculate total cooldown for one complete cycle
            var totalCycleCooldown = validDrops.Sum(x => x.Cooldown.Value);

            // Calculate number of complete cycles
            var completeCycles = (int)(seconds / totalCycleCooldown);

            if (completeCycles > 0)
            {
                // Assign items for complete cycles
                foreach (var drop in validDrops)
                {
                    if (stashItems.ContainsKey(drop.ItemId))
                        stashItems[drop.ItemId] += completeCycles;
                    else
                        stashItems[drop.ItemId] = completeCycles;
                }

                seconds -= completeCycles * totalCycleCooldown;
            }

            // For remaining seconds, add items in sequence until the seconds run out.
            while (seconds > 0)
            {
                bool itemAdded = false;
                foreach (var drop in validDrops)
                {
                    if (seconds < drop.Cooldown.Value) continue;

                    if (stashItems.ContainsKey(drop.ItemId))
                        stashItems[drop.ItemId]++;
                    else
                        stashItems[drop.ItemId] = 1;

                    seconds -= drop.Cooldown.Value;
                    itemAdded = true;
                }

                // If we're unable to add any item in a full loop, then break out of the while loop
                if (!itemAdded) break;
            }

            // Now apply changes in one batch
            BatchUpdateStashItems(c.UserId, stashItems);

            // and delete the stash items
            foreach (var b in bankItemsOfType)
            {
                Remove(b);
            }
        }

        private void ConvertResource(
            User user,
            int level,
            Func<double> getValue,
            Action<double> setValue,
            List<ResourceItemDrop> drops)
        {
            var count = getValue();
            if (count == 0) return;

            var seconds = count * 5;
            if (seconds > 0 && seconds < 10) seconds = 10;

            var validDrops = drops
                .Where(x => x.LevelRequirement <= level && x.Cooldown.HasValue && x.Cooldown.Value > 0)
                .OrderBy(x => x.Cooldown)
                .ToList();

            // Create a dictionary for easy lookup and modification
            var stashItems = new Dictionary<Guid, int>();

            // Calculate total cooldown for one complete cycle
            var totalCycleCooldown = validDrops.Sum(x => x.Cooldown.Value);

            // Calculate number of complete cycles
            var completeCycles = (int)(seconds / totalCycleCooldown);

            if (completeCycles > 0)
            {
                // Assign items for complete cycles
                foreach (var drop in validDrops)
                {
                    if (stashItems.ContainsKey(drop.ItemId))
                        stashItems[drop.ItemId] += completeCycles;
                    else
                        stashItems[drop.ItemId] = completeCycles;
                }

                seconds -= completeCycles * totalCycleCooldown;
            }

            // For remaining seconds, add items in sequence until the seconds run out.
            while (seconds > 0)
            {
                bool itemAdded = false;
                foreach (var drop in validDrops)
                {
                    if (seconds < drop.Cooldown.Value) continue;

                    if (stashItems.ContainsKey(drop.ItemId))
                        stashItems[drop.ItemId]++;
                    else
                        stashItems[drop.ItemId] = 1;

                    seconds -= drop.Cooldown.Value;
                    itemAdded = true;
                }

                // If we're unable to add any item in a full loop, then break out of the while loop
                if (!itemAdded) break;
            }

            // Now apply changes in one batch
            BatchUpdateStashItems(user.Id, stashItems);

            setValue(0);
        }

        private void BatchUpdateStashItems(Guid userId, Dictionary<Guid, int> items)
        {
            foreach (var item in items)
            {
                var stashItem = GetStashItem(userId, item.Key);
                if (stashItem != null)
                    stashItem.Amount += item.Value;
                else
                    Add(new UserBankItem
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        ItemId = item.Key,
                        Amount = item.Value
                    });
            }
        }

        //public static volatile bool VillageExpMigrationCompleted = false;
        //private void FixVillageLevels()
        //{
        //    VillageExpMigrationCompleted = false;
        //    try
        //    {
        //        var maxExp = GameMath.ExperienceForLevel(GameMath.MaxVillageLevel);
        //        if (this.villages.Entities.All(x =>
        //            x.Experience < maxExp ||
        //            x.Level <= GameMath.MaxVillageLevel))
        //        {
        //            return;
        //        }

        //        foreach (var entity in this.villages.Entities)
        //        {
        //            var level = entity.Level;
        //            if (level > GameMath.MaxVillageLevel)
        //            {
        //                entity.Level = GameMath.MaxVillageLevel;
        //            }

        //            var nextLevel = level + 1;
        //            var expRatio = entity.Experience / GameMath.OldExperienceForLevel(nextLevel);
        //            if (expRatio > 1)
        //            {
        //                expRatio = 0.75;
        //            }

        //            var exp = Math.Truncate(GameMath.ExperienceForLevel(nextLevel) * expRatio);
        //            if (exp > maxExp)
        //            {
        //                exp = maxExp - 1L;
        //            }

        //            entity.Experience = (long)exp;
        //        }
        //    }
        //    finally
        //    {
        //        VillageExpMigrationCompleted = true;
        //    }
        //}

        protected ItemDrop EnsureDrop(Item item, double dropRate, int tier = 0, int slayerLevelRequirement = 0)
        {
            return EnsureDrop(null, null, item, dropRate, dropRate, tier, slayerLevelRequirement);
        }

        protected ItemDrop EnsureDrop(Item item, double maxDrop, double minDrop, int tier = 0, int slayerLevelRequirement = 0)
        {
            return EnsureDrop(null, null, item, maxDrop, minDrop, tier, slayerLevelRequirement);
        }

        protected ItemDrop EnsureDrop(int? monthStart, int? monthsLength, Item item, double maxDrop, double minDrop, int tier = 0, int slayerLevelRequirement = 0)
        {
            var existing = itemDrops[nameof(Item), item.Id].FirstOrDefault();
            if (existing != null)
            {
                if (existing.SlayerLevelRequirement != slayerLevelRequirement) existing.SlayerLevelRequirement = slayerLevelRequirement;
                if (existing.MaxDropRate != maxDrop) existing.MaxDropRate = maxDrop;
                if (existing.MinDropRate != minDrop) existing.MinDropRate = minDrop;
                if (existing.DropStartMonth != monthStart) existing.DropStartMonth = monthStart;
                if (existing.DropDurationMonths != monthsLength) existing.DropDurationMonths = monthsLength;
                if (existing.Tier != tier) existing.Tier = tier;
                return existing;
            }

            var drop = new ItemDrop
            {
                Id = Guid.NewGuid(),
                ItemId = item.Id,
                DropStartMonth = monthStart,
                DropDurationMonths = monthsLength,
                MinDropRate = minDrop,
                MaxDropRate = maxDrop,
                SlayerLevelRequirement = slayerLevelRequirement,
                Tier = tier,
                UniqueDrop = false
            };
            Add(drop);
            return drop;
        }

        private void MergeInventoryItems()
        {
            bool CanMergeInvItem(InventoryItem i)
            {
                return !i.Equipped && string.IsNullOrEmpty(i.Enchantment) && string.IsNullOrEmpty(i.Tag) && i.TransmogrificationId == null;
            }

            bool CanMergeStashItem(UserBankItem i)
            {
                return string.IsNullOrEmpty(i.Enchantment) && string.IsNullOrEmpty(i.Tag) && i.TransmogrificationId == null;
            }

            foreach (var ubi in this.userBankItems.Entities)
            {
                if (!ubi.Soulbound)
                {
                    // see if we should be soulbound or not.
                    var item = GetItem(ubi.ItemId);
                    if (item == null || !item.Soulbound) continue;
                    ubi.Soulbound = item.Soulbound;
                }
            }

            foreach (var u in this.users.Entities)
            {
                var items = GetUserBankItems(u.Id);
                var mergable = new Dictionary<Guid, DataModels.UserBankItem>();
                foreach (var item in items)
                {
                    if (!CanMergeStashItem(item))
                    {
                        continue;
                    }

                    if (mergable.TryGetValue(item.ItemId, out var other))
                    {
                        other.Amount += item.Amount;
                        item.Amount = 0;
                        Remove(item);
                    }
                    else
                    {
                        mergable[item.ItemId] = item;
                    }
                }
            }

            foreach (var c in this.characters.Entities)
            {
                var items = GetInventoryItems(c.Id);
                var mergable = new Dictionary<Guid, DataModels.InventoryItem>();
                foreach (var item in items)
                {
                    if (!item.Soulbound)
                    {
                        var i = GetItem(item.ItemId);
                        if (item.Soulbound != i.Soulbound)
                        {
                            item.Soulbound = i.Soulbound;
                        }
                    }

                    if (!CanMergeInvItem(item))
                    {
                        continue;
                    }

                    if (mergable.TryGetValue(item.ItemId, out var other))
                    {
                        other.Amount += item.Amount;
                        item.Amount = 0;
                        Remove(item);
                    }
                    else
                    {
                        mergable[item.ItemId] = item;
                    }
                }
            }
        }

        public void RemoveDanglingEntities()
        {
            // appearances

            foreach (var act in characterSessionActivities.Entities)
            {
                if (GetCharacter(act.CharacterId) == null)
                {
                    Remove(act);
                }
            }

            foreach (var appearance in syntyAppearances.Entities)
            {
                var match = false;
                foreach (var character in characters.Entities)
                {
                    if (character.SyntyAppearanceId == appearance.Id)
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    Remove(appearance);
                }
            }

            foreach (var res in resources.Entities)
            {
                var match = false;
                foreach (var village in villages.Entities)
                {
                    if (village.ResourcesId == res.Id)
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    Remove(res);
                }
            }
        }

        public List<List<User>> GetDuplicateUsers()
        {
            return users.Entities.GroupBy(u => u.UserName)
                .Where(group => group.Count() > 1)
                .Select(group => group.ToList())
                .ToList();
        }

        private User MergeUser(List<User> group)
        {
            // move everything over to one and the same user. Its okay if its the latest one,

            var targetUser = group.OrderByDescending(x =>
            {
                var chars = GetCharactersByUserId(x.Id);
                var total = chars.Count;
                foreach (var c in chars)
                {
                    total += GetInventoryItems(c.Id).Count;
                }
                return total;
            }).FirstOrDefault();

            var targetUserAccess = GetUserAccess(targetUser.Id);

            // with a target user, one more time we will go through all characters for all users in the group
            // we will end up with more than 3 characters, but its okay. We will remove empty ones later
            foreach (var user in group)
            {
                // no need to process the target user
                if (user.Id == targetUser.Id)
                {
                    continue;
                }

                if (user.PatreonTier > targetUser.PatreonTier)
                {
                    targetUser.PatreonTier = user.PatreonTier;
                    MergeUserPatreonData(targetUser, user);
                }

                if (string.IsNullOrEmpty(targetUser.PasswordHash) && !string.IsNullOrEmpty(user.PasswordHash))
                {
                    targetUser.PasswordHash = user.PasswordHash;
                }

                // All your characters are now mine!
                var characters = GetCharactersByUserId(user.Id);
                foreach (var c in characters)
                {
                    c.UserId = targetUser.Id;
                }

                // if we have user bank items, move them over.
                foreach (var item in GetUserBankItems(user.Id))
                {
                    item.UserId = targetUser.Id;
                }

                // since only twitch is currently being used AND we already generate these records. Remove them
                foreach (var ua in GetUserAccess(user.Id))
                {
                    Remove(ua);
                }

                // Remove all properties, the props we have are related to access tokens like pub-sub,
                // but that will be updated automatically as the user logs back in again using twitch
                foreach (var prop in GetUserProperties(user.Id))
                {
                    Remove(prop);
                }

                Remove(user);
            }

            return targetUser;
        }

        private void MergeUserPatreonData(User targetUser, User user)
        {
            var sourcePatreon = GetPatreonUser(user.Id);
            if (sourcePatreon == null)
            {
                return;
            }

            var targetPatreon = GetPatreonUser(targetUser.Id);
            if (targetPatreon == null)
            {
                sourcePatreon.UserId = targetUser.Id;
                return;
            }

            if (sourcePatreon.Updated > targetPatreon.Updated && sourcePatreon.Tier > targetPatreon.Tier)
            {
                targetPatreon.Tier = sourcePatreon.Tier;
                targetPatreon.Scope = sourcePatreon.Scope;
                targetPatreon.RefreshToken = sourcePatreon.RefreshToken;
                targetPatreon.TokenType = sourcePatreon.TokenType;
                targetPatreon.AccessToken = sourcePatreon.AccessToken;
                targetPatreon.Email = sourcePatreon.Email;
                targetPatreon.FullName = sourcePatreon.FullName;
                targetPatreon.FirstName = sourcePatreon.FirstName;
                targetPatreon.ExpiresIn = sourcePatreon.ExpiresIn;
            }

            Remove(sourcePatreon);
        }

        public Dictionary<string, object> GetUserSettings(Guid userId)
        {
            var props = GetUserProperties(userId);
            if (props == null)
            {
                return null;
            }

            var settings = new Dictionary<string, object>();
            foreach (var prop in props)
            {
                settings[prop.PropertyKey] = prop.Value;
            }

            // add platform identifiers
            var access = GetUserAccess(userId);
            foreach (var a in access)
            {
                settings[a.Platform.ToLower() + "_id"] = a.PlatformId;
                settings[a.Platform.ToLower() + "_name"] = a.PlatformUsername;
            }

            var user = GetUser(userId);

            settings["is_admin"] = user.IsAdmin.GetValueOrDefault();
            settings["is_moderator"] = user.IsModerator.GetValueOrDefault();
            settings["ravenfall_id"] = user.Id;
            settings["ravenfall_name"] = user.UserName;
            return settings;
        }

        private void MigrateTwitchUserAccess()
        {
            logger.LogInformation($"Migrating old Twitch data to User Access.");
            foreach (var user in users.Entities)
            {
                UserAccess tua = GetUserAccess(user.Id, "twitch");
                if (tua == null && !string.IsNullOrEmpty(user.UserId) && !user.UserName.Contains("#"))
                {
                    var now = DateTime.UtcNow;
                    tua = new UserAccess();
                    tua.Id = Guid.NewGuid();
                    tua.UserId = user.Id;
                    tua.Platform = "twitch";
                    tua.PlatformId = user.UserId;
                    tua.PlatformUsername = user.UserName;
                    tua.Updated = now;
                    tua.Created = now;
                    Add(tua);
                }

                if (!string.IsNullOrEmpty(user.UserId))
                {
                    user.UserId = null;
                }
            }
        }

        private void RemoveCharactersWithoutSkills()
        {
            foreach (var c in this.characters.Entities)
            {
                var skills = GetCharacterSkills(c.SkillsId);
                if (skills != null)
                {
                    continue;
                }

                // make sure inventory is empty too.
                var items = GetInventoryItems(c.Id);
                if (items.Count != 0)
                {
                    // Move all items to another character in the same account.
                    var otherCharacters = GetCharactersByUserId(c.UserId);
                    var targetCharacter = otherCharacters.OrderBy(x => x.CharacterIndex).FirstOrDefault(x => GetCharacterSkills(x.SkillsId) != null);
                    if (targetCharacter != null)
                    {
                        foreach (var item in items)
                        {
                            item.CharacterId = targetCharacter.Id;
                        }
                    }
                }

                var appearance = GetAppearance(c.SyntyAppearanceId);
                if (appearance != null)
                {
                    Remove(appearance);
                }

                var statistics = GetStatistics(c.StatisticsId);
                if (statistics != null)
                {
                    Remove(statistics);
                }

                var state = GetCharacterState(c.StateId);
                if (state != null)
                {
                    Remove(state);
                }

                var invites = GetClanInvitesByCharacter(c.Id);
                foreach (var invite in invites)
                {
                    if (invite != null)
                        Remove(invite);
                }

                Remove(c);
            }
        }

        private void EnsureCharacterSkillRecords()
        {
            logger.LogInformation($"Ensuring all characters have skill records.");
            var addedRecords = 0;

            // seem like top 1000 currently is as low as 89 (min level), keeping it at 75 here
            // will at least ensure that we don't build up too many Skill Records in the db, since these wont show up on the website anyway.

            var minSkillRecordLevel = 75;

            foreach (var c in this.characters.Entities)
            {
                var skills = GetCharacterSkills(c.SkillsId);
                if (skills == null)
                {
                    logger.LogError(c.Id + " - " + c.Name + " - " + c.Identifier + " does not have any skills! SkillsId: " + c.SkillsId);



                    continue;
                }

                var records = GetCharacterSkillRecords(c.Id);

                foreach (var skill in skills.GetSkills())
                {
                    var existingRecord = records?.FirstOrDefault(x => x.SkillIndex == skill.Index);

                    if (skill.Level < minSkillRecordLevel)
                    {
                        if (existingRecord != null)
                        {
                            // this is a shitty move, but it will save us space in the db.
                            // delete this.
                            Remove(existingRecord);
                        }

                        continue;
                    }

                    if (existingRecord == null)
                    {
                        Add(new CharacterSkillRecord
                        {
                            CharacterId = c.Id,
                            Id = Guid.NewGuid(),
                            SkillExperience = skill.Experience,
                            SkillIndex = skill.Index,
                            SkillLevel = skill.Level,
                            SkillName = skill.Name,
                            DateReached = DateTime.UtcNow // since they didnt have one before, at least we can pretend it was added now.
                        });
                        addedRecords++;
                    }
                }
            }

            if (addedRecords > 0)
            {
                logger.LogError("(Not actual error) " + addedRecords + " character skill records added.");
            }
        }


        private void EnsureResources()
        {
            foreach (var village in villages.Entities)
            {
                var resources = GetResources(village.ResourcesId);
                if (resources == null)
                {
                    resources = new DataModels.Resources
                    {
                        Id = Guid.NewGuid(),
                    };
                    Add(resources);
                    village.ResourcesId = resources.Id;
                }
            }

            HashSet<Guid> userIdMissing = new HashSet<Guid>();
            HashSet<Guid> charProcessed = new HashSet<Guid>();

            int deletedCharacters = 0;
            int movedCharacters = 0;
            var beforeProcess = this.characters.Entities.ToArray();
            foreach (var c in beforeProcess)
            {
                if (charProcessed.Contains(c.Id))
                {
                    continue;
                }

                var user = GetUser(c.UserId);
                if (user == null)
                {
                    userIdMissing.Add(c.UserId);
                    // seem like we might have missing users, these are probably really old records from back in 2020.
                    // but make sure to clean these up as it will definitely cause an exception elsewhere.
                    var maybe = this.GetUserByUsername(c.Name);
                    if (maybe != null)
                    {
                        var otherChars = GetCharactersByUserId(maybe.Id);
                        var otherAvailable = PlayerManager.MaxCharacterCount - otherChars.Count;
                        var missingChars = characters[nameof(User), c.UserId];// GetCharactersByUserId(c.UserId);

                        if (otherAvailable >= missingChars.Count)
                        {
                            // we can fit these in our existing account, so lets change owner.
                            foreach (var oc in missingChars)
                            {
                                movedCharacters++;
                                charProcessed.Add(oc.Id);
                                var newCharIndex = otherChars.Count;

                                oc.UserId = maybe.Id;
                                oc.UserIdLock = null;
                                oc.CharacterIndex = newCharIndex;
                                oc.Identifier = oc.CharacterIndex.ToString();
                            }

                            continue;
                        }

                        foreach (var oc in missingChars)
                        {
                            CascadeRemoveCharacter(c);
                            deletedCharacters++;
                            charProcessed.Add(oc.Id);
                        }

                        continue;
                    }

                    CascadeRemoveCharacter(c);
                    deletedCharacters++;
                    charProcessed.Add(c.Id);
                }
            }

            var ohNo = userIdMissing.Count;

            foreach (var user in this.users.Entities)
            {
                if (user.Resources != null && user.Resources.Value != Guid.Empty)
                {
                    continue;
                }

                var chars = GetCharactersByUserId(user.Id);

                var coins = 0d;
                var wheat = 0d;
                var fish = 0d;
                var wood = 0d;
                var ore = 0d;

                var res = new DataModels.Resources
                {
                    Coins = coins,
                    Wheat = wheat,
                    Fish = fish,
                    Wood = wood,
                    Ore = ore,
                    Id = Guid.NewGuid(),
                };

                Add(res);

                user.Resources = res.Id;
            }
        }
        #endregion

        #region Data Transformations

        private double GetResourceCost(Item item)
        {
            var it = (ItemType)item.Type;
            var material = GetMaterial(item);

            if (item.Category == (int)ItemCategory.Resource && (it == ItemType.Cooking || it == ItemType.Alchemy || it == ItemType.Mining))
            {
                // ....

                if (material == ItemMaterial.None)
                {
                    // unidentified.
                    // should we go by level?
                    if (item.Name.StartsWith("Lionite"))
                    {
                        material = ItemMaterial.Lionsbane;
                    }

                    if (item.Name.StartsWith("Ether "))
                    {
                        material = ItemMaterial.Ether;
                    }
                }
            }

            switch (material)
            {
                case ItemMaterial.Bronze: return 4;
                case ItemMaterial.Iron: return 14;
                case ItemMaterial.Steel: return 50;
                case ItemMaterial.Black: return 96;
                case ItemMaterial.Mithril: return 134;
                case ItemMaterial.Adamantite: return 187;
                case ItemMaterial.Rune: return 260;
                case ItemMaterial.Dragon: return 360;
                case ItemMaterial.Abraxas: return 500;
                case ItemMaterial.Phantom: return 700;
                case ItemMaterial.Lionsbane: return 980;
                case ItemMaterial.Ether: return 1370;
                case ItemMaterial.Ancient: return 1900;
                case ItemMaterial.Atlarus: return 2600;
            }

            // still 1? check drop level requirements and use that
            var drop = resourceItemDrops[nameof(Item), item.Id].FirstOrDefault();
            if (drop != null && drop.LevelRequirement > 0)
            {
                return drop.LevelRequirement;
            }

            // what about crafting level?
            var recipe = GetItemRecipeByItem(item.Id);
            if (recipe != null && recipe.RequiredLevel > 0)
            {
                return recipe.RequiredLevel;
            }

            return 1;
        }

        private ItemMaterial GetMaterial(RavenNest.DataModels.Item item)
        {
            var mat = (ItemMaterial)item.Material;
            if (mat != ItemMaterial.None)
                return mat;

            if (item.Type == (int)ItemType.None || item.Material == (int)ItemMaterial.None)
            {
                var itemNameMaterial = "";
                if (item.Name.EndsWith("Token"))
                {
                    return ItemMaterial.None;
                }
                if (item.Name.Contains(' '))
                {
                    itemNameMaterial = item.Name.Split(' ')[0];
                }
                if (itemNameMaterial == "Ethereum")
                {
                    return ItemMaterial.Ether;
                }
                if (itemNameMaterial.ToLower() == "lionite")
                {
                    return ItemMaterial.Lionsbane;
                }

                if (itemNameMaterial.ToLower() == "abraxas")
                {
                    return ItemMaterial.Abraxas;
                }

                if (!string.IsNullOrEmpty(itemNameMaterial) && item.Material == (int)ItemMaterial.None)
                {
                    if (Enum.TryParse<ItemMaterial>(itemNameMaterial, true, out var res))
                    {
                        return res;
                    }
                }
            }
            return ItemMaterial.None;
        }

        private static long RoundTo1000(long value)
        {
            if (value < 10000L) return value;
            return (value / 1000L) * 1000L;
        }

        private void ApplyVendorPrices()
        {
            const double resourceMargins = 1.25;
            const double redeemableReduction = 0.5;
            foreach (var item in items.Entities)
            {
                if (item.Category != (int)ItemCategory.Resource)
                {
                    continue;
                }

                var resourceCost = GetResourceCost(item);
                if (resourceCost > 1)
                {
                    item.ShopSellPrice = (long)resourceCost;
                }
            }

            foreach (var item in items.Entities)
            {
                // if items has crafting materials.
                // calculate vendor price based that.

                if ((ItemCategory)item.Category == ItemCategory.Pet)
                {
                    item.ShopBuyPrice = 5000;
                    item.ShopSellPrice = 1000;
                    continue;
                }

                if (item.ShopSellPrice == 0)
                {
                    item.ShopSellPrice = 1; // minimum price.
                }

                if (item.Category == (int)ItemCategory.Resource)
                {
                    continue;
                }

                var recipe = GetItemRecipeByItem(item.Id);
                if (recipe == null)
                {
                    continue;
                }

                var ingredients = GetRecipeIngredients(recipe.Id);
                var newPrice = 0L;
                foreach (var r in ingredients)
                {
                    var targetItem = GetItem(r.ItemId);
                    if (targetItem != null)
                    {

                        if (targetItem.ShopSellPrice > 1)
                        {
                            newPrice += (targetItem.ShopSellPrice * r.Amount);
                        }
                        else
                        {
                            newPrice += (long)(GetResourceCost(targetItem) * r.Amount);
                        }
                    }
                }
                item.ShopSellPrice = RoundTo1000((long)(newPrice * resourceMargins));
            }
        }

        public Item GetOrCreateItem(string name, ItemCategory category, ItemType type, params string[] nameLookup)
        {
            return GetOrCreateItem(items.Entities, name, category, type, nameLookup);
        }

        public Item GetOrCreateItem(IReadOnlyList<Item> items, string name, ItemCategory category, ItemType type, params string[] nameLookup)
        {
            return GetOrCreateItem(items, name, null, category, type, nameLookup);
        }

        public Item GetOrCreateItem(IReadOnlyList<Item> items, string name, string description, ItemCategory category, ItemType type, params string[] nameLookup)
        {
            var item = items.FirstOrDefault(x =>
            {
                if (x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;

                if (nameLookup != null && nameLookup.Length > 0)
                {
                    foreach (var nm in nameLookup)
                    {
                        if (x.Name.Equals(nm, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                return false;
            });

            if (item != null)
            {
                if (item.Name != name)
                {
                    var path = GetImageFilePath(item.Name);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var newPath = GetImageFilePath(name, false);
                        if (!File.Exists(newPath))
                        {
                            File.Move(path, newPath);
                        }
                    }

                    item.Name = name;
                }

                if (!string.IsNullOrEmpty(description) && item.Description != description) item.Description = description;
                if (item.Category != (int)category) item.Category = (int)category;
                if (item.Type != (int)type) item.Type = (int)type;
                return item;
            }
            item = new Item
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                Category = (int)category,
                Type = (int)type,
                RequiredCraftingLevel = 1000,
                RequiredCookingLevel = 1000,
                Craftable = false,
            };
            Add(item);
            return item;
        }

        private string GetImageFilePath(string itemName, bool mustExist = true)
        {
            var path = "/imgs/items/";
            if (!System.IO.Directory.Exists("/imgs/items/"))
                path = "wwwroot" + path;

            if (!string.IsNullOrEmpty(itemName))
            {
                var fileNamePath = Path.Combine(path, itemName.ToLower().Replace("'", "").Replace(' ', '-') + ".png");
                if (!mustExist || File.Exists(fileNamePath))
                {
                    return fileNamePath;
                }
            }

            return null;
        }

        public ItemStatusEffect GetOrCreateItemStatusEffect(IReadOnlyList<ItemStatusEffect> effects, Item item, StatusEffectType type, Island island, bool allowMultipleEffects = false)
        {
            return GetOrCreateItemStatusEffect(effects, item.Id, type, island, -1, 0, 0, allowMultipleEffects);
        }

        public ItemStatusEffect GetOrCreateItemStatusEffect(IReadOnlyList<ItemStatusEffect> effects, Item item, StatusEffectType type, float amountPercent, int minAmount, bool allowMultipleEffects = false)
        {
            return GetOrCreateItemStatusEffect(effects, item.Id, type, null, -1, amountPercent, minAmount, allowMultipleEffects);
        }

        public ItemStatusEffect GetOrCreateItemStatusEffect(IReadOnlyList<ItemStatusEffect> effects, Item item, StatusEffectType type, float duration, float amountPercent, int minAmount, bool allowMultipleEffects = false)
        {
            return GetOrCreateItemStatusEffect(effects, item.Id, type, null, duration, amountPercent, minAmount, allowMultipleEffects);
        }

        public ItemStatusEffect GetOrCreateItemStatusEffect(IReadOnlyList<ItemStatusEffect> effects,
            Guid itemId, StatusEffectType type, Island? island,
            float duration, float amountPercent, int minAmount,
            bool allowMultipleEffects = false)
        {
            var effect = effects.FirstOrDefault(x => x.ItemId == itemId && (!allowMultipleEffects || x.Type == type));
            if (effect != null)
            {
                if (effect.Amount != amountPercent)
                    effect.Amount = amountPercent;
                if (effect.Duration != duration)
                    effect.Duration = duration;
                if (effect.Type != type)
                    effect.Type = type;
                if (effect.MinAmount != minAmount)
                    effect.MinAmount = minAmount;
                if (effect.Island != island)
                    effect.Island = island;
                return effect;
            }

            effect = new ItemStatusEffect
            {
                Id = Guid.NewGuid(),
                Amount = amountPercent,
                MinAmount = minAmount,
                Island = island,
                Duration = duration,
                ItemId = itemId,
                Type = type
            };

            Add(effect);
            return effect;
        }

        public void EnsureItemStatusEffects(Item item, params StatusEffect[] effectsToAdd)
        {
            EnsureItemStatusEffects(item.Id, effectsToAdd);
        }

        public void EnsureItemStatusEffects(Guid itemId, params StatusEffect[] effectsToAdd)
        {
            var effects = itemStatusEffects[nameof(Item), itemId].ToList();
            if (effects.Count != effectsToAdd.Length)
            {
                foreach (var fx in effects) Remove(fx);
                foreach (var fx in effectsToAdd) Add(ItemEffect(itemId, fx));
                return;
            }

            for (var i = 0; i < effectsToAdd.Length; ++i)
            {
                var existing = effects[i];
                var fx = effectsToAdd[i];
                if (existing.Type != fx.Type)
                    existing.Type = fx.Type;
                if (existing.Amount != fx.Amount)
                    existing.Amount = fx.Amount;
                if (existing.MinAmount != fx.MinAmount)
                    existing.MinAmount = fx.MinAmount;
                if (existing.Duration != fx.Duration)
                    existing.Duration = fx.Duration;
            }
        }

        public StatusEffect Effect(StatusEffectType type, float amountPercent, int minAmount)
        {
            return Effect(type, -1, amountPercent, minAmount);
        }

        public StatusEffect Effect(StatusEffectType type, float duration, float amountPercent, int minAmount)
        {
            return new StatusEffect
            {
                Amount = amountPercent,
                Duration = duration,
                MinAmount = minAmount,
                Type = type
            };
        }

        private IReadOnlyList<ItemStatusEffect> ItemEffects(Guid itemId, params StatusEffect[] fx)
        {
            return fx.AsList(x => ItemEffect(itemId, x));
        }

        private ItemStatusEffect ItemEffect(Guid itemId, StatusEffect fx)
        {
            return new ItemStatusEffect
            {
                Amount = fx.Amount,
                Duration = fx.Duration,
                Id = Guid.NewGuid(),
                ItemId = itemId,
                MinAmount = fx.MinAmount,
                Type = fx.Type
            };
        }

        private void UpgradeItemsAndRemoveDrops(TypedItems typedItems, ObsoleteItems obsoleteItems)
        {
            // luckily, we only need to upgrade resource types and then remove any associated item drop.

            var lookup = new Dictionary<Guid, Item>();
            lookup[obsoleteItems.Lionite.Id] = typedItems.LioniteBar;
            lookup[obsoleteItems.Ethereum.Id] = typedItems.EthereumBar;
            //lookup[obsoleteItems.OreIngot.Id] = typedItems.IronBar;
            lookup[obsoleteItems.DragonScale.Id] = typedItems.DragonBar;
            lookup[obsoleteItems.IronNugget.Id] = typedItems.IronBar;
            lookup[obsoleteItems.SteelNugget.Id] = typedItems.SteelBar;
            lookup[obsoleteItems.MithrilNugget.Id] = typedItems.MithrilBar;
            lookup[obsoleteItems.AdamantiteNugget.Id] = typedItems.AdamantiteBar;
            lookup[obsoleteItems.RuneNugget.Id] = typedItems.RuneBar;
            lookup[obsoleteItems.PhantomCore.Id] = typedItems.PhantomBar;
            lookup[obsoleteItems.AbraxasSpirit.Id] = typedItems.AbraxasBar;
            lookup[obsoleteItems.AncientHeart.Id] = typedItems.AncientBar;
            lookup[obsoleteItems.AtlarusLight.Id] = typedItems.AtlarusBar;

            var drops = GetItemDrops();
            foreach (var drop in drops)
            {
                if (obsoleteItems.IsObsoleteItem(drop.ItemId))
                {
                    Remove(drop);
                }
            }

            var resDrops = GetResourceItemDrops();
            foreach (var drop in resDrops)
            {
                if (obsoleteItems.IsObsoleteItem(drop.ItemId))
                {
                    Remove(drop);
                }
            }

            //// we will just replace the item Ids directly.
            //// we have a post processing step later that merges all items.
            //foreach (var ubi in userBankItems.Entities)
            //{
            //    if (lookup.TryGetValue(ubi.ItemId, out var conv))
            //    {
            //        ubi.ItemId = conv.Id;
            //        ubi.Name = conv.Name;
            //    }
            //}

            //foreach(var ubi in marketItems.Entities)
            //{
            //    if (lookup.TryGetValue(ubi.ItemId, out var conv))
            //    {
            //        ubi.ItemId = conv.Id;
            //        ubi.Name = conv.Name;
            //    }
            //}

            //foreach (var ubi in inventoryItems.Entities)
            //{
            //    if (lookup.TryGetValue(ubi.ItemId, out var conv))
            //    {
            //        ubi.ItemId = conv.Id;
            //        ubi.Name = conv.Name;
            //    }
            //}
        }

        private void EnsureDropRate(int level, Item item, double cooldown, double dropChance, RavenNest.Models.Skill skill)
        {
            var existingDrop = resourceItemDrops[nameof(Item), item.Id].FirstOrDefault();
            if (existingDrop != null)
            {
                if (existingDrop.Cooldown != cooldown) existingDrop.Cooldown = cooldown;
                if (existingDrop.DropChance != dropChance) existingDrop.DropChance = dropChance;
                if (existingDrop.LevelRequirement != level) existingDrop.LevelRequirement = level;
                if (existingDrop.Skill != (int)skill) existingDrop.Skill = (int)skill;
                return;
            }

            item.Level = Math.Max(level, item.Level);
            Add(new ResourceItemDrop
            {
                DropChance = dropChance,
                ItemId = item.Id,
                ItemName = item.Name,
                LevelRequirement = level,
                Skill = (int)skill,
                Cooldown = cooldown,
                Id = Guid.NewGuid(),
            });
        }


        public ItemRecipe EnsureCookingRecipe(int levelRequirement, string name, string description, Item success, Item failed, double minSuccessRate, double maxSuccessRate, params Item[] ingredients)
        {
            return EnsureRecipe(RavenNest.Models.Skill.Cooking, levelRequirement, 1, name, description, success, failed, minSuccessRate, maxSuccessRate, Ingredient.FromArray(ingredients));
        }
        public ItemRecipe EnsureCookingRecipeGuaranteed(int levelRequirement, Item success, params Ingredient[] ingredients)
        {
            return EnsureRecipe(RavenNest.Models.Skill.Cooking, levelRequirement, 1, success.Name, success.Description, success, null, 1, 1, ingredients);
        }

        public ItemRecipe EnsureCookingRecipeGuaranteed(int levelRequirement, int amount, string name, string description, Item success, params Item[] ingredients)
        {
            return EnsureRecipe(RavenNest.Models.Skill.Cooking, levelRequirement, amount, name, description, success, null, 1, 1, Ingredient.FromArray(ingredients));
        }

        public ItemRecipe EnsureCookingRecipeGuaranteed(int levelRequirement, string name, string description, Item success, params Item[] ingredients)
        {
            return EnsureRecipe(RavenNest.Models.Skill.Cooking, levelRequirement, 1, name, description, success, null, 1, 1, Ingredient.FromArray(ingredients));
        }

        public ItemRecipe EnsureCookingRecipe(int levelRequirement, Item success, Item failed, double minSuccessRate, double maxSuccessRate, params Item[] ingredients)
        {
            return EnsureRecipe(RavenNest.Models.Skill.Cooking, levelRequirement, 1, null, null, success, failed, minSuccessRate, maxSuccessRate, Ingredient.FromArray(ingredients));
        }

        public ItemRecipe EnsureAlchemyRecipe(int levelRequirement, Item target, params Item[] ingredients)
        {
            return EnsureRecipe(RavenNest.Models.Skill.Alchemy, levelRequirement, 1, null, null, target, null, 1, 1, Ingredient.FromArray(ingredients));
        }

        public ItemRecipe EnsureCraftingRecipe(int levelRequirement, Item target, params Item[] ingredients)
        {
            return EnsureRecipe(RavenNest.Models.Skill.Crafting, levelRequirement, 1, null, null, target, null, 1, 1, Ingredient.FromArray(ingredients));
        }

        public ItemRecipe EnsureCraftingRecipe(int levelRequirement, Item target, params Ingredient[] ingredients)
        {
            return EnsureRecipe(RavenNest.Models.Skill.Crafting, levelRequirement, 1, null, null, target, null, 1, 1, ingredients);
        }

        public ItemRecipe EnsureRecipe(RavenNest.Models.Skill skill, int levelRequirement, int amount,
            string name, string description,
            Item target, Item failed, double minSuccessRate, double maxSuccessRate, params Ingredient[] ingredients)
        {
            var fixedSuccessRate = minSuccessRate == maxSuccessRate;
            var recipe = GetItemRecipeByItem(target.Id);
            if (recipe != null)
            {
                if (!string.IsNullOrEmpty(name) && name != recipe.Name) recipe.Name = name;
                if (!string.IsNullOrEmpty(description) && description != recipe.Description) recipe.Description = name;
                if (recipe.Amount != amount) recipe.Amount = amount;
                if (recipe.RequiredLevel != levelRequirement) recipe.RequiredLevel = levelRequirement;
                if (recipe.MinSuccessRate != minSuccessRate) recipe.MinSuccessRate = minSuccessRate;
                if (recipe.MaxSuccessRate != maxSuccessRate) recipe.MaxSuccessRate = maxSuccessRate;
                recipe.FixedSuccessRate = fixedSuccessRate;

                var existingIngredients = GetRecipeIngredients(recipe.Id);
                if (existingIngredients.Count != ingredients.Length || existingIngredients.Sum(x => x.Amount) != ingredients.Sum(x => x.Amount) || existingIngredients.Any(x => GetItem(x.ItemId) == null))
                {
                    // update requirements, easiest is to clear em out and add them again.
                    foreach (var i in existingIngredients) Remove(i);
                    foreach (var r in ingredients) AddRecipeIngredient(recipe, r);
                }
                if (levelRequirement != target.Level) target.Level = Math.Max(levelRequirement, target.Level);
                return recipe;
            }

            target.Level = Math.Max(levelRequirement, target.Level);

            // todo: check if we have the same ingredients, if not then clear out all ingredients and replace it with the ones provided.
            //       that way we can easily update things.

            recipe = new ItemRecipe
            {
                Id = Guid.NewGuid(),
                ItemId = target.Id,
                Name = target.Name,
                Description = target.Description,
                PreparationTime = 0,
                Amount = amount,
                FailedItemId = failed?.Id,
                RequiredLevel = levelRequirement,
                FixedSuccessRate = fixedSuccessRate,
                MaxSuccessRate = maxSuccessRate,
                MinSuccessRate = minSuccessRate,
                RequiredSkill = (int)skill,
            };

            if (!string.IsNullOrEmpty(name)) recipe.Name = name;
            if (!string.IsNullOrEmpty(description)) recipe.Description = name;

            Add(recipe);

            foreach (var r in ingredients) AddRecipeIngredient(recipe, r);

            return recipe;
        }

        private void AddRecipeIngredient(ItemRecipe recipe, Ingredient r)
        {
            Add(new ItemRecipeIngredient
            {
                Id = Guid.NewGuid(),
                Amount = r.Amount,
                RecipeId = recipe.Id,
                ItemId = r.Item.Id
            });
        }

        private void EnsureCraftingRequirements(EntitySet<Item> items)
        {
            //return;

            // ensure we have all base items
            var knownItems = GetKnownItems();

            // rename known 2h staff to Staff

            foreach (var item in inventoryItems.Entities)
            {
                if (item.Name != null && item.Name.Contains("2H Staff"))
                {
                    item.Name = item.Name.Replace("2H Staff", "Staff");
                }
            }

            foreach (var item in userBankItems.Entities)
            {
                if (item.Name != null && item.Name.Contains("2H Staff"))
                {
                    item.Name = item.Name.Replace("2H Staff", "Staff");
                }
            }
        }

        private void AddOrReplace(IReadOnlyList<ItemCraftingRequirement> requirements, ItemCraftingRequirement itemCraftingRequirement)
        {
            var existing = requirements.FirstOrDefault(x => x.ResourceItemId == itemCraftingRequirement.ResourceItemId);
            if (existing != null)
            {
                if (existing.Amount != itemCraftingRequirement.Amount)
                    existing.Amount = itemCraftingRequirement.Amount;

                return;
            }

            Add(itemCraftingRequirement);
        }

        private void EnsureExpMultipliersWithinBounds(EntitySet<ExpMultiplierEvent> expMultiplierEvents)
        {
            foreach (var multi in expMultiplierEvents.Entities)
            {
                if (multi.StartedByPlayer)
                {
                    if (multi.Multiplier > ServerManager.MaxExpMultiplier)
                    {
                        multi.Multiplier = ServerManager.MaxExpMultiplier;
                    }

                    var runTime = multi.EndTime - multi.StartTime;
                    var maxRunTime = TimeSpan.FromMinutes(ServerManager.ExpMultiplierStartTimeMinutes + (ServerManager.MaxExpMultiplier * ServerManager.ExpMultiplierMinutesPerScroll));
                    if (runTime > maxRunTime)
                    {
                        multi.EndTime = multi.StartTime.Add(runTime);
                    }
                }
            }
        }

        private void EnsureClanLevels(EntitySet<Clan> clans)
        {
            foreach (var clan in clans.Entities)
            {
                if (clan.Level == 0)
                    clan.Level = 1;
            }
        }

        private void MergeClans()
        {
            var toRemove = new List<Clan>();
            foreach (var u in users.Entities)
            {
                var userClans = clans[nameof(User), u.Id]; //get list of clans on that this user is in

                if (userClans.Count > 1) //If this user is in more than one clan
                {
                    var highestClanLevelFirst = userClans.OrderByDescending(x => x.Level).ToArray(); //Order Clan By Highest level
                    var clanToKeep = highestClanLevelFirst[0]; //Keeping the highest rank
                    var clanToKeepMembership = GetClanMemberships(clanToKeep.Id).ToDictionary(x => x.CharacterId); //Abby: Get list CharacterClan on clan to keep id

                    var availableClanRoles = GetClanRoles(clanToKeep.Id);

                    var expToAdd = 0d;
                    // calculate the total amount of exp needed to be added as we merge.
                    // and update clan references between users                    

                    for (var i = 1; i < highestClanLevelFirst.Length; ++i) //skip first element, loop each clan
                    {
                        var clan = highestClanLevelFirst[i]; //this clan

                        var memberships = GetClanMemberships(clan.Id).ToDictionary(x => x.CharacterId); //Get List of members
                        foreach (var membershipKeyValue in memberships)
                        {
                            var value = membershipKeyValue.Value;
                            if (clanToKeepMembership.TryGetValue(membershipKeyValue.Key, out var membershipToKeep))
                            {
                                if (membershipToKeep.ClanRoleId == value.ClanRoleId)
                                {
                                    // remove this one and continue. since we already have the same member of same role.
                                    Remove(value);
                                    continue;
                                }

                                // we have the same member, but the role is different. 
                                var roleA = GetClanRole(membershipToKeep.ClanRoleId);
                                var roleB = GetClanRole(value.ClanRoleId);

                                // take the highest ranked one and then remove the membership.
                                if (roleA.Level < roleB.Level)
                                {
                                    var matchingRole = availableClanRoles.FirstOrDefault(x => x.Level == roleB.Level);
                                    if (matchingRole != null)
                                        membershipToKeep.ClanRoleId = matchingRole.Id;
                                }

                                Remove(value);
                            }
                            else
                            {
                                membershipKeyValue.Value.ClanId = clanToKeep.Id;
                                var roleB = GetClanRole(value.ClanRoleId);
                                var matchingRole = availableClanRoles.FirstOrDefault(x => x.Level == roleB.Level);

                                // take give the clan role of same level or lower level if non matching exists.
                                membershipToKeep.ClanRoleId = matchingRole != null ? matchingRole.Id : availableClanRoles.OrderByDescending(x => x.Level).FirstOrDefault(x => x.Level <= roleB.Level).Id;
                            }
                        }

                        expToAdd += clan.Experience;

                        for (var lv = 1; lv < clan.Level; lv++)
                        {
                            expToAdd += GameMath.ExperienceForLevel(lv + 1);
                        }

                        toRemove.Add(clan);
                        // ignore clan skills as we don't have it available just yet.
                        // ...
                    }

                    // apply the exp
                    clanToKeep.Experience += expToAdd;

                    var nextLevel = GameMath.ExperienceForLevel(clanToKeep.Level + 1);
                    var oldLevel = clanToKeep.Level;

                    while (clanToKeep.Experience >= nextLevel)
                    {
                        clanToKeep.Experience -= nextLevel;
                        ++clanToKeep.Level;
                        nextLevel = GameMath.ExperienceForLevel(clanToKeep.Level + 1);
                    }
                }
            }

            foreach (var r in toRemove)
            {
                Remove(r);
            }

            logger.LogInformation(toRemove.Count + " clans was merged.");
        }

        private void RemoveDuplicatedClanMembers()
        {
            var memberToRemove = new List<CharacterClanMembership>();
            foreach (var clan in clans.Entities)
            {
                var clanMemberships = GetClanMemberships(clan.Id);

                foreach (var group in clanMemberships.Select(x => new { Member = x, Role = GetClanRole(x.ClanRoleId) }).GroupBy(x => x.Member.CharacterId))
                {
                    var duplicates = group.OrderByDescending(x => x.Role.Level).ToArray();
                    if (duplicates.Length == 1) continue;

                    // remove the rest of the memberships as they are lower or same level but with same character.
                    for (var i = 1; i < duplicates.Length; i++)
                    {
                        Remove(duplicates[i].Member);
                    }
                }
            }
        }

        private void MergeLoyaltyData(EntitySet<UserLoyalty> loyalty)
        {
            var toRemove = new List<UserLoyalty>();
            foreach (var u in users.Entities)
            {
                try
                {
                    var userLoyalties = loyalty[nameof(User), u.Id].GroupBy(x => x.StreamerUserId).ToList();
                    foreach (var l in userLoyalties)
                    {
                        if (l.Count() > 1)
                        {
                            var highestLevel = l.OrderByDescending(x => x.Level).FirstOrDefault();
                            if (highestLevel == null)
                            {
                                continue; // shouldnt happen..
                            }

                            if (!TimeSpan.TryParse(highestLevel.Playtime, out var highestPlayTime))
                            {
                                highestPlayTime = TimeSpan.MinValue;
                            }

                            foreach (var lu in l)
                            {
                                if (lu.Id == highestLevel.Id)
                                {
                                    continue;
                                }

                                if (TimeSpan.TryParse(lu.Playtime, out var playTime) && playTime > highestPlayTime)
                                {
                                    highestLevel.Playtime = lu.Playtime;
                                }

                                if (lu.Points > 0)
                                {
                                    highestLevel.Points += lu.Points;
                                }
                                if (lu.CheeredBits > 0)
                                {
                                    highestLevel.CheeredBits += lu.CheeredBits;
                                }
                                if (lu.GiftedSubs > 0)
                                {
                                    highestLevel.GiftedSubs += lu.GiftedSubs;
                                }
                                if (lu.IsSubscriber)
                                {
                                    highestLevel.IsSubscriber = true;
                                }

                                toRemove.Add(lu);
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    logger.LogError(exc.ToString());
                }
            }

            foreach (var tr in toRemove)
            {
                Remove(tr);
            }

            if (toRemove.Count > 0)
            {
                logger.LogError("(Not actual error) Merged " + toRemove.Count + " duplicated loyalty record(s).");
            }
        }

        private void ProcessInventoryItems(EntitySet<InventoryItem> inventoryItems)
        {
            var toRemove = new List<InventoryItem>();
            foreach (var ii in inventoryItems.Entities)
            {
                var c = GetCharacter(ii.CharacterId);
                if (ii.Amount <= 0 || c == null)
                {
                    toRemove.Add(ii);
                    continue;
                }
            }

            foreach (var bad in toRemove)
            {
                Remove(bad);
            }

            if (toRemove.Count > 0)
            {
                logger.LogError("(Not actual error) Remove " + toRemove.Count + " inventory items of characters that dont exist. Most likely item reached 0 in stack size but was never removed, this can happen if ID of the stack has changed.");
            }
        }

        /// <summary>
        ///     Remove a character and any of its assocciated entries such as Resources, Inventory Items, Clan Details, Statistics, Etc.
        /// </summary>
        /// <param name="c"></param>
        private void CascadeRemoveCharacter(Character c)
        {
            var s = GetCharacterSkills(c.SkillsId);
            if (s != null)
            {
                Remove(s);
            }

            var invItems = GetInventoryItems(c.Id);
            foreach (var invItem in invItems)
            {
                Remove(invItem);
            }

            var marketItems = GetMarketItems();
            foreach (var item in marketItems)
            {
                if (item.SellerCharacterId == c.Id)
                {
                    Remove(item);
                }
            }

            var records = GetCharacterSkillRecords(c.Id);
            foreach (var r in records)
            {
                Remove(r);
            }

            var statistics = GetStatistics(c.StatisticsId);
            if (statistics != null)
            {
                Remove(statistics);
            }

            var mem = GetClanMembership(c.Id);
            if (mem != null)
            {
                Remove(mem);
            }

            var invites = GetClanInvitesByCharacter(c.Id);
            if (invites != null && invites.Count > 0)
            {
                foreach (var i in invites)
                    Remove(i);
            }
        }

        private void RemoveCharacterIfEmpty(Character c)
        {
            var s = GetCharacterSkills(c.SkillsId);
            if (s == null || s.GetSkills().All(x => x.Level == 1 || (x.Name == "Health" && x.Level == 10)))
            {
                var items = GetInventoryItems(c.Id);
                if (items != null && items.Count > 0)
                {
                    return;
                }

                if (s != null)
                {
                    Remove(s);
                }

                var records = GetCharacterSkillRecords(c.Id);
                foreach (var r in records)
                {
                    Remove(r);
                }

                var statistics = GetStatistics(c.StatisticsId);
                if (statistics != null)
                {
                    Remove(statistics);
                }

                var mem = GetClanMembership(c.Id);
                if (mem != null)
                {
                    Remove(mem);
                }

                var invites = GetClanInvitesByCharacter(c.Id);
                if (invites != null && invites.Count > 0)
                {
                    foreach (var i in invites)
                        Remove(i);
                }
            }
        }

        private void RemoveEmptyPlayers()
        {
            var now = DateTime.UtcNow;
            List<Character> characterToRemove = new();
            List<CharacterSkillRecord> recordsToRemove = new();
            // check if we have character skill records that no longer has a character :o

            foreach (var s in this.characterSkillRecords.Entities)
            {
                if (!this.characters.Contains(s.CharacterId))
                {
                    recordsToRemove.Add(s);
                }
            }

            foreach (var c in this.characters.Entities)
            {
                if (c.LastUsed == null || (now - c.LastUsed) >= TimeSpan.FromDays(365 / 2))
                {
                    // check if the player has any data associated with it. Inventory Items
                    // Skills, etc.

                    var chars = GetCharactersByUserId(c.UserId);

                    // if chars == null, user does not exist anymore.
                    if (chars != null && chars.Count > 1)
                    {
                        continue;
                    }

                    var s = GetCharacterSkills(c.SkillsId);
                    if (s == null || s.GetSkills().All(x => x.Level == 1 || (x.Name == "Health" && x.Level == 10)))
                    {
                        var items = GetInventoryItems(c.Id);
                        if (items != null && items.Count > 0)
                        {
                            continue;
                        }

                        characterToRemove.Add(c);

                        if (s != null)
                        {
                            Remove(s);
                        }

                        var records = GetCharacterSkillRecords(c.Id);
                        foreach (var r in records)
                        {
                            Remove(r);
                        }

                        var statistics = GetStatistics(c.StatisticsId);
                        if (statistics != null)
                        {
                            Remove(statistics);
                        }

                        var mem = GetClanMembership(c.Id);
                        if (mem != null)
                        {
                            Remove(mem);
                        }

                        var invites = GetClanInvitesByCharacter(c.Id);
                        if (invites != null && invites.Count > 0)
                        {
                            foreach (var i in invites)
                                Remove(i);
                        }
                    }
                }
            }

            var str = new StringBuilder();
            var removedUserCount = 0;
            foreach (var c in characterToRemove)
            {
                Remove(c);

                str.AppendLine("c\t" + c.Id + "\t" + c.Name);

                var user = GetUser(c.UserId);
                // just in case, we will do another check.

                if (user != null)
                {
                    if (GetCharactersByUserId(c.UserId)?.Count > 1)
                    {
                        continue;
                    }

                    ++removedUserCount;

                    Remove(user);
                    str.AppendLine("u\t" + user.Id + "\t" + user.UserName);
                }
            }

            foreach (var r in recordsToRemove)
            {
                Remove(r);
            }

            if (characterToRemove.Count > 0)
            {
                logger.LogError("(Not actual error) Remove " + characterToRemove.Count + " characters and " + removedUserCount + " users removed as they have starting stats, no items and not played for over 6 months.");
                try
                {
                    var dt = DateTime.UtcNow;
                    string GeneratedFileStorage = "GeneratedData";
                    var fileName = $"removed__{dt:yyyy-MM-dd_hhMMss}.txt";

                    var fullPathToFile = System.IO.Path.Combine(GeneratedFileStorage, fileName);
                    System.IO.File.WriteAllText(fullPathToFile, str.ToString());
                }
                catch { }
            }
        }

        private void RemoveBadUsers(EntitySet<User> users)
        {
            var toRemove = new List<User>();
            foreach (var user in users.Entities)
            {
                if (string.IsNullOrEmpty(user.UserName) || (!string.IsNullOrEmpty(user.UserId) && Guid.TryParse(user.UserId, out _)))
                {
                    toRemove.Add(user);
                }
            }

            foreach (var badUser in toRemove)
            {
                Remove(badUser);
            }

            if (toRemove.Count > 0)
            {
                logger.LogError("Removed " + toRemove.Count + " users without username or users with twitch user id being a guid.");
            }
        }

        private bool InvokeIfSettingsMatch(string name, Func<ServerSettings, bool> check, Action invoke, Func<ServerSettings, string> onSuccess, Func<ServerSettings, string> onFail)
        {
            var settings = GetOrCreateServerSettings(name);
            if (check(settings))
            {
                try
                {
                    invoke();
                    onSuccess(settings);
                    return true;
                }
                catch
                {
                    onFail(settings);
                }
            }

            return false;
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ItemStatusEffect> GetItemStatusEffects(Guid itemId)
        {
            return itemStatusEffects[nameof(Item), itemId];
        }

        #region Add Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ItemStatusEffect statusEffect) => Update(() => itemStatusEffects.Add(statusEffect));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(CharacterStatusEffect statusEffect) => Update(() => characterStatusEffects.Add(statusEffect));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ItemRecipe item) => Update(() => itemRecipes.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ItemRecipeIngredient item) => Update(() => itemRecipeIngredients.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ServerSettings item) => Update(() => serverSettings.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(VendorItem item) => Update(() => vendorItems.Add(item));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(DailyAggregatedMarketplaceData item) => Update(() => dailyAggregatedMarketplaceData.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(DailyAggregatedEconomyReport item) => Update(() => dailyAggregatedEconomyReport.Add(item));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(VendorTransaction item) => Update(() => vendorTransaction.Add(item));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(UserAccess item) => Update(() => userAccess.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(CharacterClanSkillCooldown item) => Update(() => characterClanSkillCooldown.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ClanRolePermissions item) => Update(() => clanRolePermissions.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ResourceItemDrop item) => Update(() => resourceItemDrops.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Pet item) => Update(() => pets.Add(item));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(CharacterSkillRecord item) => Update(() => characterSkillRecords.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ItemAttribute item) => Update(() => itemAttributes.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Agreements item) => Update(() => agreements.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(UserBankItem item) => Update(() => userBankItems.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(RedeemableItem item) => Update(() => redeemableItems.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(UserNotification entity) => Update(() => notifications.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ClanSkill entity) => Update(() => clanSkills.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(MarketItemTransaction entity) => Update(() => marketTransactions.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(CharacterClanInvite entity) => Update(() => this.clanInvites.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Clan entity) => Update(() => this.clans.Add(entity));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ClanRole entity) => Update(() => this.clanRoles.Add(entity));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(CharacterClanMembership entity) => Update(() => this.clanMemberships.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(UserLoyalty loyalty) => Update(() => this.loyalty.Add(loyalty));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(UserLoyaltyRank loyaltyRank) => Update(() => loyaltyRanks.Add(loyaltyRank));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(UserLoyaltyReward loyaltyRankReward) => Update(() => loyaltyRewards.Add(loyaltyRankReward));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(UserPatreon ev) => Update(() => patreons.Add(ev));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ExpMultiplierEvent ev) => Update(() => expMultiplierEvents.Add(ev));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(CharacterSessionActivity ev) => Update(() => characterSessionActivities.Add(ev));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ItemCraftingRequirement entity) => Update(() => itemCraftingRequirements.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(VillageHouse house) => Update(() => villageHouses.Add(house));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Village entity) => Update(() => villages.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Item entity) => Update(() => items.Add(entity));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(ItemDrop entity)
        {
            if (entity == null) return AddEntityResult.Error;
            return Update(() => itemDrops.Add(entity));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(CharacterState entity) => Update(() => characterStates.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(SyntyAppearance entity) => Update(() => syntyAppearances.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Statistics entity) => Update(() => statistics.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Skills entity) => Update(() => characterSkills.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Resources entity) => Update(() => resources.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Character entity) => Update(() => characters.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(User entity) => Update(() => users.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(InventoryItem entity) => Update(() => inventoryItems.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(GameSession entity) => Update(() => gameSessions.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(MarketItem entity) => Update(() => marketItems.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(GameEvent entity) => gameEvents.Add(entity);//Update(() => gameEvents.Add(entity));

        public static GameSession CreateSession(Guid userId)
        {
            return new GameSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Revision = 0,
                Started = DateTime.UtcNow,
                Status = (int)SessionStatus.Active
            };
        }

        public GameEvent CreateSessionEvent<T>(RavenNest.Models.GameEventType type, GameSession session, T data)
        {
            session.Updated = DateTime.UtcNow;
            var ev = new GameEvent
            {
                Id = Guid.NewGuid(),
                GameSessionId = session.Id,
                UserId = session.UserId,
                Type = (int)type,
                Revision = GetNextGameEventRevision(session.Id),
                Data = MessagePackSerializer.Serialize(data, MessagePack.Resolvers.ContractlessStandardResolver.Options)//JSON.Stringify(data)
            };
            return ev;
        }
        #endregion

        #region Get Entities

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Village GetOrCreateVillageBySession(GameSession session)
        {
            var village = GetVillageBySession(session);
            if (village == null)
            {
                village = CreateVillage(session.UserId);
            }

            return village;
        }

        public Village CreateVillage(Guid userId)
        {
            var villageResources = new Resources()
            {
                Id = Guid.NewGuid()
            };

            Add(villageResources);

            var user = GetUser(userId);
            var minAdminVillageLevel = 30;
            var isAdmin = user.IsAdmin.GetValueOrDefault();
            var villageExp = isAdmin ? (long)GameMath.ExperienceForLevel(minAdminVillageLevel) : 0;
            var villageLevel = isAdmin ? GameMath.ExperienceForLevel(minAdminVillageLevel) : 1;
            var village = new Village()
            {
                Id = Guid.NewGuid(),
                ResourcesId = villageResources.Id,
                Level = (int)villageLevel,
                Experience = (long)villageExp,
                Name = "Village",
                UserId = userId
            };

            Add(village);

            return village;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<VillageHouse> GetVillageHouses(Village village)
        {
            return villageHouses[nameof(Village), village.Id];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<VillageHouse> GetOrCreateVillageHouses(Village village)
        {
            var houses = villageHouses[nameof(Village), village.Id];

            if (village.Level <= 1)
            {
                return Array.Empty<VillageHouse>();
            }

            var houseCount = System.Math.Min(village.Level / 10, GameMath.MaxVillageLevel / 10);

            if ((houses == null || houses.Count == 0) && houseCount > 0)
            {
                houses = Enumerable.Range(0, houseCount).Select(x => new VillageHouse
                {
                    Id = Guid.NewGuid(),
                    Created = DateTime.UtcNow,
                    Slot = x,
                    Type = 0,
                    VillageId = village.Id
                }).ToList();

                foreach (var house in houses)
                {
                    Add(house);
                }
            }

            if (houses.Count < houseCount)
            {
                var housesTemp = houses.ToList();

                for (var i = houses.Count; i < houseCount; ++i)
                {
                    var house = new VillageHouse
                    {
                        Id = Guid.NewGuid(),
                        Created = DateTime.UtcNow,
                        Slot = i,
                        Type = 0,
                        VillageId = village.Id
                    };

                    Add(house);

                    housesTemp.Add(house);
                }

                houses = housesTemp;
            }
            return houses;
        }

        public void ClearAllCharacterSessionStates(Guid userId)
        {
            foreach (var session in this.GetSessionsByUserId(userId))
            {
                ClearCharacterSessionStates(session.Id);
            }
        }

        public void ClearCharacterSessionStates(Guid sessionId)
        {
            this.characterSessionStates.TryRemove(sessionId, out _);
        }

        public void ResetCharacterSessionState(Guid sessionId, Guid characterId)
        {
            ConcurrentDictionary<Guid, CharacterSessionState> states;
            if (!characterSessionStates.TryGetValue(sessionId, out states))
            {
                characterSessionStates[sessionId] = states = new ConcurrentDictionary<Guid, CharacterSessionState>();
            }
            states[characterId] = new CharacterSessionState();
        }

        public CharacterSessionState GetCharacterSessionState(Guid sessionId, Guid characterId)
        {
            // We might need to know which platform the character is being used from so we know where to reply.
            if (!characterSessionStates.TryGetValue(sessionId, out var states))
            {
                states = new ConcurrentDictionary<Guid, CharacterSessionState>();
            }

            if (!states.TryGetValue(characterId, out var state))
            {
                state = new CharacterSessionState();
                state.LastTaskUpdate = DateTime.UtcNow;
                state.LastExpUpdate = DateTime.UtcNow;
                state.LastStateUpdate = DateTime.UtcNow;
                state.SailingRewardAttempted = DateTime.MinValue;
                states[characterId] = state;
                characterSessionStates[sessionId] = states;
            }

            return state;
        }

        public SessionState GetSessionState(Guid sessionId)
        {
            SessionState state;
            if (!sessionStates.TryGetValue(sessionId, out state))
            {
                state = new SessionState();
                sessionStates[sessionId] = state;
            }

            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ServerSettings GetServerSettings(string name)
        {
            return serverSettings.Entities.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ServerSettings GetOrCreateServerSettings(string name)
        {
            var settings = GetServerSettings(name);
            if (settings == null)
            {
                settings = new ServerSettings()
                {
                    Id = Guid.NewGuid(),
                    Name = name
                };
                Add(settings);
            }
            return settings;
        }

        // This is not code, it is a shrimp. Cant you see?
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character FindCharacter(Func<Character, bool> predicate) =>
            characters.Entities.FirstOrDefault(predicate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem FindPlayerItem(Guid id, Func<InventoryItem, bool> predicate) =>
            characters.TryGet(id, out var player)
                ? inventoryItems[nameof(Character), player.Id].FirstOrDefault(predicate)
                : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> FindPlayerItems(Guid id, Func<InventoryItem, bool> predicate) =>
            characters.TryGet(id, out var player)
                ? inventoryItems[nameof(Character), player.Id].Where(predicate).ToList()
                : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession FindSession(Func<GameSession, bool> predicate) =>
            gameSessions.Entities.FirstOrDefault(predicate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<DataModels.GameSession> FindSessions(Func<DataModels.GameSession, bool> predicate) =>
            gameSessions.Entities.Where(predicate).ToList();


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession GetSessionByCharacterId(Guid characterId, bool allowInactiveSessions = false)
        {
            var character = characters.Entities.FirstOrDefault(x => x.Id == characterId);
            if (character == null || character.UserIdLock == null) return null;
            var sessionOwner = GetUser(character.UserIdLock.Value);
            if (sessionOwner == null) return null;

            if (allowInactiveSessions)
            {
                return gameSessions[nameof(User), sessionOwner.Id].FirstOrDefault();
            }

            return GetActiveSessions().FirstOrDefault(x => x.UserId == sessionOwner.Id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession GetOwnedSessionByUserId(string userId, string platform)
        {
            var access = userAccess.Entities.FirstOrDefault(x =>
                x.PlatformId.Equals(userId, StringComparison.OrdinalIgnoreCase) &&
                x.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));

            if (access == null) return null;

            return GetActiveSessions().FirstOrDefault(x => x.UserId == access.UserId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession GetOwnedSessionByUserId(Guid userId)
        {
            return GetActiveSessions().FirstOrDefault(x => x.UserId == userId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete]
        public GameSession GetJoinedSessionByUserId(string userId, string platform)
        {
            var access = userAccess.Entities.FirstOrDefault(x =>
                x.PlatformId.Equals(userId, StringComparison.OrdinalIgnoreCase) &&
                x.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));

            if (access == null) return null;

            var character = characters.Entities.FirstOrDefault(x => x.UserId == access.UserId);
            if (character == null) return null;

            return gameSessions.Entities
                .OrderByDescending(x => x.Started)
                .FirstOrDefault(x =>
                    x.UserId == character.UserIdLock &&
                    (SessionStatus)x.Status == SessionStatus.Active);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User FindUser(Func<User, bool> predicate) =>
            users.Entities.FirstOrDefault(predicate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User FindUser(string userIdOrUsername)
        {
            if (string.IsNullOrWhiteSpace(userIdOrUsername))
                return null;

            foreach (var user in users.Entities)
            {
                if (user.UserName?.Equals(userIdOrUsername, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    return user;
                }

                foreach (var access in GetUserAccess(user.Id))
                {
                    if (access.PlatformId.Equals(userIdOrUsername, StringComparison.OrdinalIgnoreCase) ||
                        access.PlatformUsername.Equals(userIdOrUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        return user;
                    }
                }
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<UserLoyaltyReward> GetLoyaltyRewards()
            => loyaltyRewards.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Agreements> GetAllAgreements() => agreements.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetAllPlayerItems(Guid characterId) =>
            inventoryItems[nameof(Character), characterId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<UserBankItem> GetUserBankItems(Guid userId)
            => userBankItems[nameof(User), userId].ToList();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<UserBankItem> GetUserBankItemsByItemId(Guid itemId)
            => userBankItems.Entities.AsList(x => x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UserBankItem GetUserBankItem(Guid id) => userBankItems[id];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UserBankItem GetStashItem(Guid userId, Guid itemId) =>
            userBankItems[nameof(User), userId].FirstOrDefault(x => x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetInventoryItems(Guid characterId) =>
            inventoryItems[nameof(Character), characterId].Where(x => !x.Equipped).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character GetCharacter(Guid characterId) =>
            characters[characterId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character GetCharacterByName(string name, string identifier = "0")
        {
            var c = characters.Entities.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
            if (c == null) return null;
            return GetCharacterByUserId(c.UserId, identifier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character GetCharacterByUserId(Guid userId, string identifier = "0")
        {
            var chars = characters[nameof(User), userId];
            return GetCharacterByIdentifier(chars, identifier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character GetCharacterByUserId(string twitchUserId, string identifier)
        {
            var user = GetUserByTwitchId(twitchUserId);
            var chars = user == null ? null : characters[nameof(User), user.Id];
            return GetCharacterByIdentifier(chars, identifier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Character> GetCharactersByUserId(Guid userId)
        {
            var user = GetUser(userId);
            return user == null ? null : characters[nameof(User), user.Id];
        }

        [Obsolete("Use GetCharacterBySession(Guid, Guid, Bool) instead as it uses the Ravenfall UserId instead of a platform id")]
        public Character GetCharacterBySession(Guid sessionId, string userId, string platform, bool updateSession = true)
        {
            var session = GetSession(sessionId, updateSession);
            var characters = GetActiveSessionCharacters(session);

            foreach (var character in characters)
            {
                var uac = GetUserAccess(character.UserId);
                foreach (var a in uac)
                {
                    if (a.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                        a.PlatformId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                        return character;
                }
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character GetSessionCharacterByUserId(Guid sessionId, Guid userId, bool updateSession = true)
        {
            var session = GetSession(sessionId, updateSession);
            var characters = GetActiveSessionCharacters(session);
            return characters.FirstOrDefault(x => x.UserId == userId);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character GetSessionCharacter(Guid sessionId, Guid characterId, bool updateSession = true)
        {
            var session = GetSession(sessionId, updateSession);
            var characters = GetActiveSessionCharacters(session);
            return characters.FirstOrDefault(x => x.Id == characterId);
        }


        private Character GetCharacterByIdentifier(IReadOnlyList<Character> chars, string identifier)
        {
            if (identifier != null && identifier.Length > 0)
            {
                var allowedCharacters = "_=qwertyuiopåasdfghjklöäzxcvbnm1234567890".ToArray();
                identifier = string.Join("", identifier.ToArray().Where(x => allowedCharacters.Contains(Char.ToLower(x))));

                if (string.IsNullOrEmpty(identifier))
                {
                    identifier = "0";
                }
            }


            var hasIndex = int.TryParse(identifier, out var index);
            index = index > 0 ? index - 1 : 0;

            if (hasIndex)
            {
                var c1 = chars.FirstOrDefault(x => x.CharacterIndex == index);
                if (c1 != null)
                    return c1;
            }

            foreach (var c in chars.OrderBy(x => x.CharacterIndex))
            {
                if (!string.IsNullOrEmpty(c.Identifier)
                    && !string.IsNullOrEmpty(identifier)
                    && c.Identifier.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }

                if (string.IsNullOrEmpty(identifier) && string.IsNullOrEmpty(c.Identifier))
                {
                    return c;
                }
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ItemCraftingRequirement> GetCraftingRequirements(Guid itemId) => itemCraftingRequirements[nameof(Item), itemId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Character> GetCharacters(Func<Character, bool> predicate) =>
            characters.Entities.AsList(predicate);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Character> GetCharacters() => characters.Entities.AsReadOnlyList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<User> GetUsers() => users.Entities.AsReadOnlyList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Clan> GetClans() => clans.Entities.AsReadOnlyList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetEquippedItem(Guid characterId, Guid itemId) =>
            inventoryItems[nameof(Character), characterId].FirstOrDefault(x => x.Equipped && x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetInventoryItem(Guid inventoryItemId) => inventoryItems[inventoryItemId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetInventoryItem(Guid characterId, Guid itemId) =>
            inventoryItems[nameof(Character), characterId].FirstOrDefault(x => !x.Equipped && x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetEquippedItems(Guid characterId) =>
            inventoryItems[nameof(Character), characterId].AsList(x => x.Equipped);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetInventoryItems(Guid characterId, Guid itemId) =>
            inventoryItems[nameof(Character), characterId].AsList(x => !x.Equipped && x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetInventoryItemsByItemId(Guid itemId) =>
            inventoryItems.Entities.AsList(x => x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Item GetItem(Guid id) => items[id];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Item> GetItems() => items.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ItemAttribute> GetItemAttributes() => itemAttributes.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VendorItem GetVendorItemByItemId(Guid itemId)
            => vendorItems[nameof(Item), itemId].FirstOrDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<VendorItem> GetVendorItems() => vendorItems.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<VendorTransaction> GetVendorTransactions(DateTime start, DateTime end) => vendorTransaction.Entities.AsList(x => x.Created >= start && x.Created <= end);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions() => marketTransactions.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions(DateTime start, DateTime end) => marketTransactions.Entities.AsList(x => x.Created >= start && x.Created <= end);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions(Guid itemId, DateTime start, DateTime end) => marketTransactions[nameof(Item), itemId].AsList(x => x.Created >= start && x.Created <= end);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactionsBySeller(Guid seller, DateTime start, DateTime end) => marketTransactions[nameof(Character) + "Seller", seller].AsList(x => x.Created >= start && x.Created <= end);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactionsByBuyer(Guid buyer, DateTime start, DateTime end) => marketTransactions[nameof(Character) + "Buyer", buyer].AsList(x => x.Created >= start && x.Created <= end);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMarketItemCount() => marketItems.Entities.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMarketItemCount(ItemFilter filter) =>
            marketItems.Entities.Where(x => Filter(filter, x)).Count();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItem> GetMarketItems(Guid itemId, string tag = null)
        {
            if (string.IsNullOrEmpty(tag))
                return marketItems[nameof(Item), itemId];

            return marketItems[nameof(Item), itemId].AsList(x => x.Tag == tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DataModels.MarketItem GetMarketItem(Guid marketItemId)
        {
            return marketItems[marketItemId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItem> GetMarketItems(int skip, int take)
            => marketItems.Entities.Slice(skip, take);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<DailyAggregatedMarketplaceData> GetMarketplaceReports(DateTime startDateInclusive, DateTime endDateInclusive)
        {
            return dailyAggregatedMarketplaceData.Entities.AsList(x => x.Date >= startDateInclusive && x.Date <= endDateInclusive);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<DailyAggregatedEconomyReport> GetEconomyReports(DateTime startDateInclusive, DateTime endDateInclusive)
        {
            return dailyAggregatedEconomyReport.Entities.AsList(x => x.Date >= startDateInclusive && x.Date <= endDateInclusive);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItem> GetMarketItems() => marketItems.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItem> GetMarketItems(ItemFilter filter, int skip, int take)
            => marketItems.Entities.Where(x => Filter(filter, x)).Slice(skip, take);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNextGameEventRevision(Guid sessionId)
        {
            var events = GetSessionEvents(sessionId);
            if (events.Count == 0) return 1;
            return events.Max(x => x.Revision) + 1;
        }

        public int GetNextGameEventRevision(Guid sessionId, int minRevision)
        {
            var events = GetSessionEvents(sessionId);
            if (events.Count == 0) return minRevision + 1;
            var value = events.Max(x => x.Revision) + 1;
            if (value <= minRevision)
                value = minRevision + 1;
            return value;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession GetSession(Guid sessionId, bool updateSession = true)
        {
            var session = gameSessions[sessionId];
            if (updateSession && session != null) session.Updated = DateTime.UtcNow;
            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Character> GetSessionCharacters(GameSession currentSession)
        {
            if (currentSession == null) return null;
            return characters[nameof(GameSession), currentSession.UserId].OrderByDescending(x => x.LastUsed).AsList(x => GetUser(x.UserId) != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Character> GetActiveSessionCharacters(GameSession currentSession)
        {
            if (currentSession == null) return null;
            return characters[nameof(GameSession), currentSession.UserId]
                .OrderByDescending(x => x.LastUsed)
                .AsList(x => GetUser(x.UserId) != null && x.UserIdLock == currentSession.UserId && x.LastUsed >= currentSession.Started);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Character> GetCharactersByUserLock(System.Guid userIdLock)
        {
            return characters[nameof(GameSession), userIdLock].AsList(x => GetUser(x.UserId) != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUserProperty(Guid userId, string propertyKey, string propertyValue)
        {
            if (userId == Guid.Empty)
            {
                return;
            }

            Update(() =>
            {
                var prop = this.userProperties[nameof(User), userId].FirstOrDefault(x => x.PropertyKey.Equals(propertyKey, StringComparison.InvariantCultureIgnoreCase));
                if (prop == null)
                {
                    prop = new UserProperty();
                    prop.Id = Guid.NewGuid();
                    prop.UserId = userId;
                    prop.PropertyKey = propertyKey;
                    prop.Value = propertyValue;
                    prop.Created = DateTime.UtcNow;
                    prop.Updated = DateTime.UtcNow;
                    userProperties.Add(prop);
                    return;
                }

                prop.Value = propertyValue;
                prop.Updated = DateTime.UtcNow;
            });
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetUserProperty(Guid userId, string propertyKey, string defaultPropertyValue = null)
        {
            if (userId == Guid.Empty)
            {
                return defaultPropertyValue;
            }

            var prop = this.userProperties[nameof(User), userId].FirstOrDefault(x => x.PropertyKey.Equals(propertyKey, StringComparison.InvariantCultureIgnoreCase));
            if (prop != null)
            {
                return prop.Value;
            }
            return defaultPropertyValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<UserProperty> GetUserProperties(Guid userId)
        {
            return this.userProperties[nameof(User), userId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExpMultiplierEvent GetActiveExpMultiplierEvent() =>
            this.expMultiplierEvents?.Entities?
            .Where(x => x.StartTime <= DateTime.UtcNow && x.EndTime >= DateTime.UtcNow)
            .OrderByDescending(x => x.Multiplier)
            .FirstOrDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameEvent> GetSessionEvents(GameSession gameSession) => GetUserEvents(gameSession.UserId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameEvent> GetSessionEvents(Guid sessionId) => GetUserEvents(GetSession(sessionId).UserId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<DataModels.UserNotification> GetNotifications(Guid userId)
            => notifications[nameof(User), userId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameEvent> GetUserEvents(Guid userId) => gameEvents[nameof(User), userId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UserAccess GetUserAccess(Guid userId, string platform) =>
            userAccess[nameof(User), userId].FirstOrDefault(x => x.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<UserAccess> GetUserAccess(Guid userId) => userAccess[nameof(User), userId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User GetUser(Guid userId) => users[userId];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Pet GetActivePet(Guid characterId) => pets[nameof(Character), characterId].FirstOrDefault(x => x.Active);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Pet> GetPets(Guid characterId) => pets[nameof(Character), characterId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Pet GetPet(Guid id) => pets[id];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User GetUserByTwitchId(string userId)
        {
            return GetUser(userId, "twitch");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User GetUser(string platformId, string platform)
        {
            platformId = platformId?.ToLower()?.Trim();
            if (string.IsNullOrEmpty(platformId)) return null;

            var access = userAccess.Entities.FirstOrDefault(x =>
                x.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                x.PlatformId.Equals(platformId, StringComparison.OrdinalIgnoreCase));


            if (access != null)
            {
                return GetUser(access.UserId);
            }

            return null;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User GetUserByUsername(string username)
        {
            username = username?.ToLower()?.Trim();
            if (string.IsNullOrEmpty(username)) return null;
            return users.Entities.FirstOrDefault(x =>
                x != null && x.UserName != null && x.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        //.OrderBy(x => x.Created)
        //.FirstOrDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UserLoyalty GetUserLoyalty(Guid userId, Guid streamerUserId)
            => loyalty[nameof(User), userId].FirstOrDefault(x => x.StreamerUserId == streamerUserId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<UserLoyalty> GetUserLoyalties(Guid userId) => loyalty[nameof(User), userId];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<UserLoyalty> GetStreamerLoyalties(Guid userId) => loyalty["Streamer", userId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UserPatreon GetPatreonUser(Guid userId) =>
            patreons[nameof(User), userId].FirstOrDefault(x => x != null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UserPatreon GetPatreonUser(long patreonId) =>
            patreons.Entities.FirstOrDefault(x => x.PatreonId == patreonId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UserNotification GetNotification(Guid id) => notifications[id];


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameSession> GetSessionsByUserId(Guid userId)
        {
            return gameSessions[nameof(User), userId]
                    .OrderByDescending(x => x.Started).ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession GetSessionByUserId(Guid userId, bool updateSession = true, bool mustBeActive = true)
        {
            var session = gameSessions[nameof(User), userId]
                    .OrderByDescending(x => x.Started)
                    .FirstOrDefault(x => !mustBeActive || (mustBeActive && x.Stopped == null));
            if (updateSession && session != null) session.Updated = DateTime.UtcNow;
            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<RedeemableItem> GetRedeemableItems() => redeemableItems.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<ItemRecipe> GetItemRecipes()
        {
            return itemRecipes.Entities;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemRecipe GetItemRecipe(Guid recipeId)
        {
            return itemRecipes[recipeId];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ItemRecipeIngredient> GetItemRecipeIngredientsByItem(Guid itemId)
        {
            return itemRecipeIngredients[nameof(Item), itemId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemRecipe GetItemRecipeByItem(Guid itemId)
        {
            return itemRecipes[nameof(Item), itemId].FirstOrDefault();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceItemDrop GetResourceItemDrop(Guid itemId)
        {
            return resourceItemDrops[nameof(Item), itemId].FirstOrDefault();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ItemRecipeIngredient> GetRecipeIngredients(Guid recipeId)
        {
            return itemRecipeIngredients[nameof(ItemRecipe), recipeId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RedeemableItem GetRedeemableItemByItemId(Guid itemId) => redeemableItems[nameof(Item), itemId].FirstOrDefault(x => x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterSessionActivity GetSessionActivity(Guid sessionId, Guid characterId)
        {
            return characterSessionActivities[nameof(Character), characterId].FirstOrDefault(x => x.SessionId == sessionId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Resources GetResources(Guid resourcesId) => resources[resourcesId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Resources GetResources(Character character)
        {
            var user = GetUser(character.UserId);
            if (user == null)
            {
                // what?
            }
            return GetResources(user);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Resources GetResources(User user)
        {
            if (user.Resources == null) return null;
            if (resources.TryGet(user.Resources.Value, out var rsx))
                return rsx;
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Statistics GetStatistics(Guid statisticsId) => statistics[statisticsId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SyntyAppearance GetAppearance(Guid? syntyAppearanceId) =>
            syntyAppearanceId == null ? null : syntyAppearances[syntyAppearanceId.Value];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterSkillRecord GetCharacterSkillRecord(Guid id, int skillIndex)
        {
            return characterSkillRecords[nameof(Character), id].FirstOrDefault(x => x.SkillIndex == skillIndex);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterSkillRecord> GetCharacterSkillRecords(Guid characterId)
        {
            return characterSkillRecords[nameof(Character), characterId];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterSkillRecord> GetSkillRecords(int skillIndex)
        {
            return characterSkillRecords.Entities.AsList(x => x.SkillIndex == skillIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterSkillRecord> GetSkillRecords(int skillIndex, ICollection<Guid> characterIds)
        {
            var records = new List<CharacterSkillRecord>();
            foreach (var characterId in characterIds)
            {
                var sr = GetCharacterSkillRecord(characterId, skillIndex);
                if (sr == null)
                {
                    var character = GetCharacter(characterId);
                    var skills = GetCharacterSkills(character.SkillsId);
                    var skill = skills.GetSkill(skillIndex);
                    // slow. But add it.
                    sr = new CharacterSkillRecord
                    {
                        CharacterId = characterId,
                        DateReached = DateTime.UtcNow,
                        Id = Guid.NewGuid(),
                        SkillExperience = skill.Experience,
                        SkillLevel = skill.Level,
                        SkillIndex = skillIndex,
                        SkillName = skill.Name
                    };

                    Add(sr);
                }

                records.Add(sr);
            }
            return records;
            //return characterSkillRecords.Entities.AsList(x => characterIds.Contains(x.CharacterId) && x.SkillIndex == skillIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterSkillRecord> GetSkillRecords(int skillIndex, int level)
        {
            return characterSkillRecords.Entities.AsList(x => x.SkillIndex == skillIndex && x.SkillLevel == level);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Skills GetCharacterSkills(Guid skillsId) => characterSkills[skillsId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Skill> GetSkills() => skills.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Skill GetSkill(Guid skillId) => skills[skillId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterClanSkillCooldown> GetClanSkillCooldowns(Guid characterId)
            => characterClanSkillCooldown[nameof(Character), characterId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterClanSkillCooldown GetClanSkillCooldown(Guid characterId, Guid skillId)
        {
            var cd = characterClanSkillCooldown[nameof(Character), characterId].FirstOrDefault(x => x.SkillId == skillId);
            if (cd == null)
            {
                var now = DateTime.UtcNow;
                cd = new CharacterClanSkillCooldown
                {
                    Id = Guid.NewGuid(),
                    SkillId = skillId,
                    CharacterId = characterId,
                    CooldownStart = now,
                    CooldownEnd = now,
                };

                Add(cd);
            }
            return cd;
        }

        public CharacterClanSkillCooldown GetEnchantmentCooldown(Guid characterId)
        {
            var clanMembership = GetClanMembership(characterId);
            if (clanMembership == null)
                return null;

            var skills = GetClanSkills(clanMembership.ClanId);
            if (skills == null || skills.Count == 0)
                return null;

            var enchantingSkill = GetSkills().FirstOrDefault(x => x.Name == "Enchanting");
            //var clanSkill = skills.FirstOrDefault(x => x.SkillId == enchantingSkill.Id);
            return GetClanSkillCooldown(characterId, enchantingSkill.Id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ClanSkill> GetClanSkills(Guid clanId) => clanSkills[nameof(Clan), clanId];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ClanSkill> GetClanSkills() => clanSkills.Entities.ToList();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Clan GetClan(Guid clanId) => clans[clanId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterClanInvite GetClanInvite(Guid inviteId) => clanInvites[inviteId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterClanInvite> GetClanInvitesByCharacter(Guid characterId) => clanInvites[nameof(Character), characterId];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterClanInvite> GetClanInvitesSent(Guid userId) => clanInvites[nameof(User), userId];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterClanInvite> GetClanInvites(Guid clanId) => clanInvites[nameof(Clan), clanId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterClanMembership> GetClanMemberships(Guid clanId)
        {
            return clanMemberships[nameof(Clan), clanId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Clan GetClanByOwner(Guid userId)
        {
            return clans[nameof(User), userId].FirstOrDefault(x => x != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClanRolePermissions GetClanRolePermissions(Guid roleId)
        {
            return clanRolePermissions[nameof(ClanRole), roleId].FirstOrDefault(x => x != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterClanMembership GetClanMembership(Guid characterId) =>
            clanMemberships[nameof(Character), characterId].FirstOrDefault(x => x != null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClanRole GetClanRole(Guid roleId) =>
            clanRoles[roleId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ClanRole> GetClanRoles(Guid clanId) =>
            clanRoles[nameof(Clan), clanId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ItemDrop> GetItemDrops()
            => itemDrops.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ResourceItemDrop> GetResourceItemDrops()
            => resourceItemDrops.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterState GetCharacterState(Guid? stateId) => stateId == null ? null : characterStates[stateId.Value];


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<CharacterStatusEffect> GetCharacterStatusEffects(Guid characterId)
        {
            return characterStatusEffects[nameof(Character), characterId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameSession> GetActiveSessions() => gameSessions.Entities
                    .OrderByDescending(x => x.Started)
                    .AsList(x => x.Stopped == null && DateTime.UtcNow - x.Updated <= TimeSpan.FromSeconds(SessionTimeoutSeconds));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameSession> GetLatestSessions() => gameSessions.Entities
                .OrderByDescending(x => x.Started)
                .GroupBy(x => x.UserId)
                .Select(x => x.FirstOrDefault()).ToList();


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameSession> GetSessions() => gameSessions.Entities.OrderByDescending(x => x.Started).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetEquippedItem(Guid characterId, ItemCategory category)
        {
            foreach (var invItem in inventoryItems[nameof(Character), characterId].Where(x => x.Equipped))
            {
                var item = GetItem(invItem.ItemId);
                if (item.Category == (int)category) return invItem;
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Village GetVillageBySession(GameSession session) => villages[nameof(User), session.UserId].FirstOrDefault(x => x != null);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Village GetVillageByUserId(Guid userId) => villages[nameof(User), userId].FirstOrDefault(x => x != null);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Village GetVillage(Guid villageId) => villages[villageId];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Village> GetVillages() => villages.Entities.ToList();
        #endregion

        #region Remove Entities

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(ItemStatusEffect statusEffect) => itemStatusEffects.Remove(statusEffect);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(CharacterStatusEffect statusEffect) => characterStatusEffects.Remove(statusEffect);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(ItemRecipe item) => itemRecipes.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(ItemRecipeIngredient item) => itemRecipeIngredients.Remove(item);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(VendorItem item) => vendorItems.Remove(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(ItemDrop entity) => itemDrops.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(DailyAggregatedMarketplaceData item) => dailyAggregatedMarketplaceData.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(DailyAggregatedEconomyReport item) => dailyAggregatedEconomyReport.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(VendorTransaction item) => vendorTransaction.Remove(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(MarketItemTransaction item) => marketTransactions.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(UserPatreon item) => patreons.Remove(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(CharacterClanSkillCooldown item) => characterClanSkillCooldown.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(UserProperty item) => userProperties.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(ClanRolePermissions item) => clanRolePermissions.Remove(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(VillageHouse item) => villageHouses.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(CharacterSkillRecord item) => characterSkillRecords.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(ResourceItemDrop item) => resourceItemDrops.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(Pet item) => pets.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(UserBankItem item) => userBankItems.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(Agreements item) => agreements.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(RedeemableItem entity) => redeemableItems.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(UserNotification entity) => notifications.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(UserLoyalty entity) => loyalty.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(Clan entity) => this.clans.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(CharacterClanInvite entity) => this.clanInvites.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(ClanRole entity) => this.clanRoles.Remove(entity);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(CharacterClanMembership entity) => this.clanMemberships.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(CharacterSessionActivity ev) => characterSessionActivities.Remove(ev);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(GameEvent ev) => gameEvents.Remove(ev);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(ItemCraftingRequirement entity) => itemCraftingRequirements.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(UserAccess item) => userAccess.Remove(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(User user) => users.Remove(user);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(SyntyAppearance entity) => syntyAppearances.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(Skills skill) => characterSkills.Remove(skill);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(CharacterState state) => characterStates.Remove(state);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(Statistics stat) => statistics.Remove(stat);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(Character character) => characters.Remove(character);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(Resources res) => resources.Remove(res);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(Village res) => villages.Remove(res);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(MarketItem marketItem) => marketItems.Remove(marketItem);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoveEntityResult Remove(InventoryItem invItem) => inventoryItems.Remove(invItem);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(IReadOnlyList<InventoryItem> items) => items.ForEach(x => Remove(x));

        #endregion

        #region Persistence

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ScheduleNextSave()
        {
            if (scheduleHandler != null) return;
            scheduleHandler = kernel.SetTimeout(SaveChanges, SaveInterval);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ScheduleNextBackup()
        {
            if (backupHandler != null) return;
            backupHandler = kernel.SetTimeout(CreateBackup, BackupInterval);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T Update<T>(Func<T> update)
        {
            if (update == null) return default;
            var result = update.Invoke();
            ScheduleNextSave();
            return result;
        }
        private void Update(Action update)
        {
            if (update == null) return;
            update.Invoke();
            ScheduleNextSave();
        }

        public void Flush()
        {
            SaveChanges();
        }

        private void CreateBackup()
        {
            try
            {
                backupProvider.CreateBackup(entitySets);
            }
            catch (Exception exc)
            {
                logger.LogError("Failed to create data backup: " + exc);
            }
            finally
            {
                ScheduleNextBackup();
            }
        }

        public byte[] GetCompressedEntities()
        {
            try
            {
                return backupProvider.GetCompressedEntityStream(entitySets);
            }
            catch (Exception exc)
            {
                logger.LogError("Failed to get entity data stream: " + exc);
                return null;
            }
        }

        private void SaveChanges()
        {
            SimpleDropHandler.SaveDropTimes();
            kernel.ClearTimeout(scheduleHandler);
            scheduleHandler = null;
            var lastQuery = "";
            var entityType = "";
            var errorSaving = false;
            try
            {
                lock (SyncLock)
                {
                    logger.LogDebug("Saving all pending changes to the database.");

                    foreach (var entitySet in entitySets)
                    {
                        try
                        {
                            var queue = BuildSaveQueue(entitySet);
                            while (queue.TryPeek(out var saveData))
                            {
                                if (saveData.Entities.Count == 0)
                                {
                                    queue.Dequeue();
                                    continue;
                                }

                                using (var con = db.GetConnection())
                                {
                                    con.Open();

                                    var query = queryBuilder.Build(saveData);
                                    if (query == null || string.IsNullOrEmpty(query.Command))
                                    {
                                        queue.Dequeue();
                                        continue;
                                    }
                                    entityType = saveData.Entities[0]?.GetType().FullName;
                                    var command = con.CreateCommand();
                                    lastQuery = command.CommandText = query.Command;
                                    var result = command.ExecuteNonQuery();

                                    ClearChangeSetState(saveData);
                                    queue.Dequeue();
                                    con.Close();
                                }
                            }

                            backupProvider.ClearRestorePoint(entitySet);
                        }
                        catch (SqlException exc)
                        {
                            errorSaving = true;
                            backupProvider.CreateRestorePoint(new[] { entitySet });
                            logger.LogError("Failed to save " + entityType + " to DB! Restorepoint Created and query saved. Exception: " + exc);
                            File.WriteAllText(Path.Combine(FolderPaths.GeneratedData, entityType + "_error_query.sql"), lastQuery);
                        }
                    }

                    //var queue = BuildSaveQueue();
                    //while (queue.TryPeek(out var saveData))
                    //{
                    //    if (saveData.Entities.Count == 0)
                    //    {
                    //        queue.Dequeue();
                    //        continue;
                    //    }
                    //    using (var con = db.GetConnection())
                    //    {
                    //        con.Open();
                    //        var query = queryBuilder.Build(saveData);
                    //        if (query == null || string.IsNullOrEmpty(query.Command))
                    //        {
                    //            queue.Dequeue();
                    //            continue;
                    //        }
                    //        entityType = saveData.Entities[0]?.GetType().FullName;
                    //        var command = con.CreateCommand();
                    //        lastQuery = command.CommandText = query.Command;
                    //        var result = command.ExecuteNonQuery();
                    //        ClearChangeSetState(saveData);
                    //        queue.Dequeue();
                    //        con.Close();
                    //    }
                    //}
                    //backupProvider.ClearRestorePoint();
                }
            }
            //catch (SqlException exc)
            //{
            //    CreateBackup();
            //    backupProvider.CreateRestorePoint(entitySets);
            //    logger.LogError("ERROR SAVING DATA [Type: " + entityType + "](CREATING RESTORE POINT!!) " + exc);
            //    File.WriteAllText(Path.Combine(FolderPaths.GeneratedData, entityType + "_error_query.sql"), lastQuery);
            //}
            catch (Exception exc)
            {
                logger.LogError("ERROR SAVING DATA!! " + exc);
                // log this
            }
            finally
            {
                ScheduleNextSave();
            }

            if (errorSaving)
            {
                CreateBackup();
            }
        }

        private void HandleSqlError(DataSaveError saveError)
        {
        }

        private DataSaveError ParseSqlError(string error)
        {
            if (error.Contains("duplicate key"))
            {
                return ParseDuplicateKeyError(error);
            }

            if (error.Contains("insert the value NULL into"))
            {
                return ParseNullInsertError(error);
            }

            return null;
        }

        private DataSaveError ParseNullInsertError(string error)
        {
            return null;
            // TODO
        }

        private DataSaveError ParseDuplicateKeyError(string error)
        {
            var id = error.Split('(').Last().Split(')').First();
            var type = error.Split(new string[] { "'dbo." }, StringSplitOptions.None).Last().Split("'").First();
            return null;
            // TODO
        }

        private void ClearChangeSetState(EntityStoreItems items = null)
        {
            foreach (var set in entitySets)
            {
                if (items == null)
                    set.ClearChanges();
                else
                    set.Clear(items.Entities);
            }
        }

        private Queue<EntityStoreItems> BuildSaveQueue(IEntitySet set)
        {
            var queue = new Queue<EntityStoreItems>();
            var addedItems = JoinChangeSets(set.Added);
            foreach (var batch in CreateBatches(EntityState.Added, addedItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            var updateItems = JoinChangeSets(set.Updated);
            foreach (var batch in CreateBatches(EntityState.Modified, updateItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            var deletedItems = JoinChangeSets(set.Removed);
            foreach (var batch in CreateBatches(EntityState.Deleted, deletedItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            return queue;
        }


        private Queue<EntityStoreItems> BuildSaveQueue()
        {
            var queue = new Queue<EntityStoreItems>();
            var addedItems = JoinChangeSets(entitySets.SelectAsArray(x => x.Added));
            foreach (var batch in CreateBatches(EntityState.Added, addedItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            var updateItems = JoinChangeSets(entitySets.SelectAsArray(x => x.Updated));
            foreach (var batch in CreateBatches(EntityState.Modified, updateItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            var deletedItems = JoinChangeSets(entitySets.SelectAsArray(x => x.Removed));
            foreach (var batch in CreateBatches(EntityState.Deleted, deletedItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            return queue;
        }

        private ICollection<EntityStoreItems> CreateBatches(EntityState state, IReadOnlyList<EntityChangeSet> items, int batchSize)
        {
            if (items == null || items.Count == 0) return new List<EntityStoreItems>();
            var batches = (int)Math.Floor(items.Count / (float)batchSize) + 1;
            var batchList = new List<EntityStoreItems>(batches);
            for (var i = 0; i < batches; ++i)
            {
                batchList.Add(new EntityStoreItems(state, items.SliceAs(i * batchSize, batchSize, x => x.Entity)));
            }
            return batchList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IReadOnlyList<EntityChangeSet> JoinChangeSets(params IEnumerable<EntityChangeSet>[] changesets) =>
            changesets.SelectMany(x => x).OrderBy(x => x.LastModified).AsReadOnlyList();

        public bool RemoveFromStash(UserBankItem bankItemScroll, int amount)
        {
            if (bankItemScroll == null || bankItemScroll.Amount < amount)
                return false;

            var left = bankItemScroll.Amount - amount;
            if (left == 0)
            {
                Remove(bankItemScroll);
            }
            else
            {
                bankItemScroll.Amount -= amount;
            }

            return true;
        }

        public Skills GenerateSkills()
        {
            return new Skills
            {
                Id = Guid.NewGuid(),
                HealthLevel = 10,
                AttackLevel = 1,
                CraftingLevel = 1,
                CookingLevel = 1,
                DefenseLevel = 1,
                FarmingLevel = 1,
                FishingLevel = 1,
                MagicLevel = 1,
                MiningLevel = 1,
                RangedLevel = 1,
                SailingLevel = 1,
                SlayerLevel = 1,
                StrengthLevel = 1,
                WoodcuttingLevel = 1,
                HealingLevel = 1,
                AlchemyLevel = 1,
                GatheringLevel = 1,
            };
        }

        public void EnqueueGameEvent(GameEvent entity)
        {
            if (entity == null)
            {
                return;
            }

            // is it possible that there are multiple tcp connections from the client or same streamer?
            // are we using the wrong connection if so? or wrong session?
            if (tcpConnectionProvider.TryGet(entity.GameSessionId, out var connection) && connection.Connected)
            {
                connection.Enqueue(entity);
                return;
            }

            Add(entity);
        }


        private bool Filter(ItemFilter itemFilter, MarketItem item)
        {
            if (itemFilter == ItemFilter.All)
                return true;

            return GetItemFilter(item.ItemId) == itemFilter;
        }

        private ItemFilter GetItemFilter(Guid itemId)
        {
            var item = GetItem(itemId);
            var itemType = (ItemType)item.Type;
            var itemCategory = (ItemCategory)item.Category;

            if (itemCategory == ItemCategory.Resource ||
                itemType == ItemType.Coins ||
                itemType == ItemType.Mining ||
                itemType == ItemType.Woodcutting ||
                itemType == ItemType.Fishing ||
                itemType == ItemType.Crafting ||
                itemType == ItemType.Cooking ||
                itemType == ItemType.Alchemy)
                return ItemFilter.Resources;

            if (itemType == ItemType.OneHandedSword || itemType == ItemType.TwoHandedSword)
                return ItemFilter.Swords;
            if (itemType == ItemType.TwoHandedBow) return ItemFilter.Bows;
            if (itemType == ItemType.TwoHandedStaff) return ItemFilter.Staves;
            if (itemType == ItemType.Ring || itemType == ItemType.Amulet) return ItemFilter.Accessories;
            if (itemType == ItemType.Shield) return ItemFilter.Shields;
            if (itemType == ItemType.Pet) return ItemFilter.Pets;
            if (itemType == ItemType.Scroll) return ItemFilter.Scrolls;

            if (itemCategory == ItemCategory.Armor)
                return ItemFilter.Armors;

            return ItemFilter.All;
        }

        public void Dispose()
        {
            this.Flush();
        }

        internal void SetNetworkStats(int threadId, long receiveMessageCount, long receiveRateKBps, long outMessageCount, long outRateKBps)
        {
            NetworkStats.ThreadId = threadId;
            NetworkStats.InMessageCount = receiveMessageCount;
            NetworkStats.InTrafficKBps = receiveRateKBps;
            NetworkStats.OutMessageCount = outMessageCount;
            NetworkStats.OutTrafficKBps = outRateKBps;
            NetworkStats.InSampleDateTime = DateTime.UtcNow;
        }

        public readonly NetworkStats NetworkStats = new NetworkStats();
    }

    public class NetworkStats
    {
        public int ThreadId { get; internal set; }
        public long InMessageCount { get; internal set; }
        public long InTrafficKBps { get; internal set; }
        public long OutMessageCount { get; internal set; }
        public long OutTrafficKBps { get; internal set; }
        public DateTime InSampleDateTime { get; internal set; }
    }

    public class DataSaveError
    {
    }

    #endregion


    public struct Ingredient
    {
        public Item Item;
        public int Amount;

        public static explicit operator Ingredient(Item item)
        {
            return new Ingredient
            {
                Item = item,
                Amount = 1
            };
        }

        public static Ingredient[] FromArray(Item[] i)
        {
            var ingr = new List<Ingredient>();
            var req = i.GroupBy(x => x.Id);
            foreach (var r in req)
            {
                ingr.Add(new Ingredient
                {
                    Amount = r.Count(),
                    Item = r.FirstOrDefault()
                });
            }
            return ingr.ToArray();
        }
    }
    public class ObsoleteItems
    {
        [Obsolete("Use Lionite Ore instead")] public Item Lionite;
        [Obsolete("Use Ethereum Ore instead")] public Item Ethereum;
        [Obsolete("Use Bronze Bar or Iron Bar instead")] public Item OreIngot;
        [Obsolete("Use Dragon Ore instead")] public Item DragonScale;
        [Obsolete("Use Iron Bar instead")] internal Item IronNugget;
        [Obsolete("Use Steel Bar instead")] internal Item SteelNugget;
        [Obsolete("Use Mithril Bar instead")] internal Item MithrilNugget;
        [Obsolete("Use Adamantite Bar instead")] internal Item AdamantiteNugget;
        [Obsolete("Use Rune Bar instead")] internal Item RuneNugget;
        [Obsolete("Use Phantom Bar instead")] internal Item PhantomCore;
        [Obsolete("Use Abraxas Bar instead")] internal Item AbraxasSpirit;
        [Obsolete("Use Ancient Bar instead")] internal Item AncientHeart;
        [Obsolete("Use Atlarus Bar instead")] internal Item AtlarusLight;

        [Obsolete] internal Item WoodPlank;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsObsoleteItem(Item item)
        {
            return IsObsoleteItem(item.Id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsObsoleteItem(Guid itemId)
        {
            return itemId == AbraxasSpirit.Id ||
                   itemId == AdamantiteNugget.Id ||
                   itemId == AncientHeart.Id ||
                   itemId == AtlarusLight.Id ||
                   itemId == DragonScale.Id ||
                   itemId == Ethereum.Id ||
                   itemId == IronNugget.Id ||
                   itemId == Lionite.Id ||
                   itemId == MithrilNugget.Id ||
                   itemId == OreIngot.Id ||
                   itemId == PhantomCore.Id ||
                   itemId == RuneNugget.Id ||
                   itemId == SteelNugget.Id;
        }
    }
    public class TypedItems
    {
        // Special Items
        public Item SantaHat;

        // Woodcutting
        public Item Logs;
        public Item BristleLogs;
        public Item GlowbarkLogs;
        public Item MystwoodLogs;
        public Item SandriftLogs;
        public Item PineheartLogs;
        public Item EbonshadeLogs;
        public Item IronbarkLogs;
        public Item FrostbiteLogs;
        public Item DragonwoodLogs;
        public Item GoldwillowLogs;
        public Item ShadowoakLogs;

        // New drops for alchemy
        public Item Hearthstone;
        public Item WanderersGem;
        public Item IronEmblem;
        public Item KyoCrystal;
        public Item HeimRune;
        public Item AtriasFeather;
        public Item EldarasMark;
        public Item Realmstone;

        // tokens
        public Item ChristmasToken;
        public Item HalloweenToken;
        public Item AbraxasToken;
        public Item RuneToken;

        // Gathering - Cooking
        public Item Water;
        public Item Mushroom;
        public Item BlackPepper;
        public Item Salt;
        public Item Yeast;

        // Gathering - Alchemy
        public Item Yarrow;
        public Item Hemp;
        public Item Sand;
        public Item Resin;


        public Item RedClover;
        public Item Comfrey;
        public Item Sage;
        public Item Mugwort;
        public Item Lavender;
        public Item Goldenrod;
        public Item Elderflower;
        public Item Wormwood;
        public Item Valerian;
        public Item Skullcap;
        public Item Chamomile;
        public Item LemonBalm;

        // Farming - Cooking
        public Item Wheat;
        public Item Tomato;
        public Item Milk;
        public Item Eggs;
        public Item Cabbage;
        public Item RawChicken;
        public Item RawChickenLeg;
        public Item RawPork;
        public Item RawBeef;
        public Item Potato;
        public Item Onion;
        public Item Cumin;
        public Item Coriander;
        public Item Paprika;
        public Item Turmeric;
        public Item Apple;
        public Item Carrots;
        public Item Garlic;
        public Item Grapes;
        public Item Truffle;

        // Fishing
        public Item RawSprat;
        public Item RawShrimp;
        public Item RawRedSeaBass;
        public Item RawBass;
        public Item RawPerch;
        public Item RawSalmon;
        public Item RawCrab;
        public Item RawLobster;
        public Item RawBlueLobster;
        public Item RawSwordfish;
        public Item RawPufferFish;
        public Item RawOctopus;
        public Item RawMantaRay;
        public Item RawKraken;
        public Item RawLeviathan;
        public Item RawPoseidonsGuardian;

        // Cooking
        public Item Flour;
        public Item Butter;
        public Item Sugar;
        public Item Cinnamon;
        public Item Cheese;
        public Item SpiceMix;
        public Item Ham;
        public Item Cacao;
        public Item GoldenLeaf;
        public Item Chocolate;

        // Cooking Recipes
        public Item RedWine;
        public Item HamSandwich;
        public Item RoastedChicken;
        public Item RoastBeef;
        public Item RoastedPork;
        public Item CookedChickenLeg;
        public Item Steak;
        public Item GrilledCheese;
        public Item ApplePie;
        public Item Bread;
        public Item Skewers;
        public Item ChocolateChipCookies;
        public Item BurnedGrilledCheese;

        // Fish
        public Item Sprat;
        public Item Shrimp;
        public Item RedSeaBass;
        public Item Bass;
        public Item Perch;
        public Item Salmon;
        public Item Crab;
        public Item Lobster;
        public Item BlueLobster;
        public Item Swordfish;
        public Item PufferFish;
        public Item Octopus;
        public Item MantaRay;
        public Item Kraken;
        public Item LeviathansRoyalStew;
        public Item PoseidonsGuardianFeast;

        // Misc
        public Item BurnedChicken;
        public Item BurnedBeef;
        public Item BurnedPork;
        public Item BurnedChickenLeg;
        public Item BurnedSteak;
        public Item BurnedApplePie;
        public Item BurnedBread;
        public Item BurnedSkewers;
        public Item BurnedChocolateChipCookies;

        // Failed Fish Dishes
        public Item BurnedSprat;
        public Item BurnedShrimp;
        public Item BurnedRedSeaBass;
        public Item BurnedBass;
        public Item BurnedPerch;
        public Item BurnedSalmon;
        public Item BurnedCrab;
        public Item BurnedLobster;
        public Item BurnedBlueLobster;
        public Item BurnedSwordfish;
        public Item BurnedPufferFish;
        public Item BurnedOctopus;
        public Item BurnedMantaRay;
        public Item BurnedKraken;
        public Item MuddledLeviathanBroth;
        public Item RuinedGuardianDelight;

        // Alchemy Recipes
        public Item Paper;
        // public Item DragonwoodPaper; // maybe?

        public Item Vial;
        public Item String;
        public Item WoodPulp;

        public Item HealthPotion;
        public Item GreatHealthPotion;
        public Item RegenPotion;
        public Item DefensePotion;
        public Item GreatDefensePotion;
        public Item StrengthPotion;
        public Item GreatStrengthPotion;
        public Item MagicPotion;
        public Item GreatMagicPotion;
        public Item RangedPotion;
        public Item GreatRangedPotion;
        public Item HealingPotion;
        public Item GreatHealingPotion;


        public Item TomeOfHome;
        public Item TomeOfAway;
        public Item TomeOfIronhill;
        public Item TomeOfKyo;
        public Item TomeOfHeim;
        public Item TomeOfAtria;
        public Item TomeOfEldara;
        public Item TomeOfTeleportation;

        // Mining
        public Item Emerald;
        public Item Ruby;
        public Item Sapphire;
        public Item Topaz;
        public Item PhilosophersStone;

        public Item CopperOre;
        public Item TinOre;
        public Item IronOre;
        public Item Coal;
        public Item Silver;
        public Item Gold;
        public Item MithrilOre;
        public Item AdamantiteOre;
        public Item RuneOre;
        public Item DragonOre;
        public Item AbraxasOre;
        public Item PhantomOre;
        public Item LioniteOre;
        public Item EthereumOre;
        public Item AncientOre;
        public Item AtlarusOre;
        public Item Eldrium;

        // Crafting
        public Item BronzeBar;
        public Item IronBar;
        public Item SteelBar;
        public Item MithrilBar;
        public Item AdamantiteBar;
        public Item RuneBar;
        public Item DragonBar;
        public Item AbraxasBar;
        public Item PhantomBar;
        public Item LioniteBar;
        public Item EthereumBar;
        public Item AncientBar;
        public Item AtlarusBar;

        public Item ElderBronzeBar;
        public Item ElderIronBar;
        public Item ElderSteelBar;
        public Item ElderMithrilBar;
        public Item ElderAdamantiteBar;
        public Item ElderRuneBar;
        public Item ElderDragonBar;
        public Item ElderAbraxasBar;
        public Item ElderPhantomBar;
        public Item ElderLioniteBar;
        public Item ElderEthereumBar;
        public Item ElderAncientBar;
        public Item ElderAtlarusBar;

        public Item SilverBar;
        public Item GoldBar;

        // equipment

        public Item GoldRing;
        public Item EmeraldRing;
        public Item RubyRing;
        public Item DragonRing;
        public Item PhantomRing;

        public Item GoldAmulet;
        public Item EmeraldAmulet;
        public Item RubyAmulet;
        public Item DragonAmulet;
        public Item PhantomAmulet;

        public Item ArchersRing;
        public Item ArchersRingII;
        public Item ArchersRingIII;
        public Item ArchersRingIV;
        public Item MagesRing;
        public Item MagesRingII;
        public Item MagesRingIII;
        public Item MagesRingIV;
        public Item ArchmagesPendant;
        public Item KnightsEmblem;
        public Item OwlsEyeRing;
        public Item RingOfTheCelestial;
        public Item WarriorsMightRing;
        public Item WindcallersAmulet;
        public ItemPets Pets;
        public ItemSet
            Bronze, Iron, Steel, Black, Mithril, Adamantite, Rune, Dragon, Abraxas, Phantom, Ether, Lionsbane, Ethereum, Ancient, Atlarus;
        //,ElderBronze, ElderIron, ElderSteel, ElderMithril, ElderAdamantite, ElderRune, ElderDragon,
        //ElderAbraxas, ElderPhantom, ElderEther, ElderLionite, ElderAncient, ElderAtlarus;

    }
    public class ItemPets
    {
        // base pets
        public Item FoxPet;
        public Item DeerPet;
        public Item BearPet;
        public Item BlueOrbPet;
        public Item WolfPet;
        public Item GreenOrbPet;
        public Item PolarBearPet;
        public Item RedOrbPet;
        public Item RedPandaPet;

        // redeemable (Loyalty points)
        public Item DiscoRavenPet;
        public Item BaconRavenPet;
        public Item DiamondRavenPet;
        public Item TurdRavenPet;
        public Item RavenPet;
        public Item Rajah;

        // redeemable (halloween)
        public Item GhostPet;
        public Item BatPet;
        public Item PumpkinPet;
        public Item SpiderPet;
        public Item WerewolfPet;
        public Item YetiPet;

        // redeemable (christmas)
        public Item GreenSantaMetalon;
        public Item BlackSantaRaven;
        public Item MagicSantaRaven;
        public Item PurpleSantaMetalon;
        public Item RedSantaMetalon;
        public Item SantaRaven;
    }

    public class ItemSet
    {
        public Item Boots;
        public Item Gloves;
        public Item Sword;
        public Item Helmet;
        public Item Bow;
        public Item Staff;
        public Item TwoHandedSword;
        public Item TwoHandedAxe;
        public Item Spear;
        public Item Axe;
        public Item Katana;
        public Item Leggings;
        public Item Chest;
        public Item Shield;
    }
}
