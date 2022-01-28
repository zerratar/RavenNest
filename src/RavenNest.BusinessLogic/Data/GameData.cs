using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public class GameData : IGameData
    {
        #region Settings
        private const int BackupInterval = 60 * 60 * 1000; // once per hour
        private const int SaveInterval = 10000;
        private const int SaveMaxBatchSize = 50;
        public const float SessionTimeoutSeconds = 1f;
        #endregion

        #region Private members

        private readonly IRavenfallDbContextProvider db;
        private readonly ILogger logger;
        private readonly IKernel kernel;
        private readonly IQueryBuilder queryBuilder;

        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, CharacterSessionState>> characterSessionStates
            = new ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, CharacterSessionState>>();

        private readonly ConcurrentDictionary<Guid, SessionState> sessionStates
            = new ConcurrentDictionary<Guid, SessionState>();

        private readonly EntitySet<Agreements, Guid> agreements;

        private readonly EntitySet<UserLoyalty, Guid> loyalty;
        private readonly EntitySet<UserProperty, Guid> userProperties;
        private readonly EntitySet<UserLoyaltyRank, Guid> loyaltyRanks;
        private readonly EntitySet<UserLoyaltyReward, Guid> loyaltyRewards;
        private readonly EntitySet<UserClaimedLoyaltyReward, Guid> claimedLoyaltyRewards;
        private readonly EntitySet<UserNotification, Guid> notifications;

        private readonly EntitySet<CharacterClanInvite, Guid> clanInvites;
        private readonly EntitySet<Clan, Guid> clans;
        private readonly EntitySet<ClanRole, Guid> clanRoles;
        private readonly EntitySet<ClanSkill, Guid> clanSkills;
        private readonly EntitySet<MarketItemTransaction, Guid> marketTransactions;
        private readonly EntitySet<VendorTransaction, Guid> vendorTransaction;

        private readonly EntitySet<CharacterClanMembership, Guid> clanMemberships;

        private readonly EntitySet<UserPatreon, Guid> patreons;
        private readonly EntitySet<CharacterSessionActivity, Guid> characterSessionActivities;
        private readonly EntitySet<Appearance, Guid> appearances;
        private readonly EntitySet<SyntyAppearance, Guid> syntyAppearances;
        private readonly EntitySet<Character, Guid> characters;
        private readonly EntitySet<CharacterState, Guid> characterStates;
        private readonly EntitySet<GameSession, Guid> gameSessions;
        private readonly EntitySet<ExpMultiplierEvent, Guid> expMultiplierEvents;
        private readonly EntitySet<GameEvent, Guid> gameEvents;

        private readonly EntitySet<UserBankItem, Guid> userBankItems;
        private readonly EntitySet<InventoryItem, Guid> inventoryItems;


        private readonly EntitySet<ItemAttribute, Guid> itemAttributes;

        private readonly EntitySet<RedeemableItem, Guid> redeemableItems;

        private readonly EntitySet<MarketItem, Guid> marketItems;
        private readonly EntitySet<Item, Guid> items;
        private readonly EntitySet<NPC, Guid> npcs;
        private readonly EntitySet<NPCItemDrop, Guid> npcItemDrops;
        private readonly EntitySet<NPCSpawn, Guid> npcSpawns;
        private readonly EntitySet<ItemCraftingRequirement, Guid> itemCraftingRequirements;
        private readonly EntitySet<Resources, Guid> resources;
        private readonly EntitySet<Statistics, Guid> statistics;
        private readonly EntitySet<Skills, Guid> characterSkills;
        private readonly EntitySet<Skill, Guid> skills;

        private readonly EntitySet<User, Guid> users;
        private readonly EntitySet<GameClient, Guid> gameClients;
        private readonly EntitySet<Village, Guid> villages;
        private readonly EntitySet<VillageHouse, Guid> villageHouses;


        private readonly IEntitySet[] entitySets;
        private readonly IGameDataBackupProvider backupProvider;
        private ITimeoutHandle scheduleHandler;
        private ITimeoutHandle backupHandler;

        #endregion

        #region Public members
        public GameClient Client { get; private set; }
        public object SyncLock { get; } = new object();
        public bool InitializedSuccessful { get; } = false;

        public BotStats Bot { get; set; } = new BotStats();
        #endregion

        #region Game Data Construction
        public GameData(
            IGameDataBackupProvider backupProvider,
            IGameDataMigration dataMigration,
            IRavenfallDbContextProvider db,
            ILogger<GameData> logger,
            IKernel kernel,
            IQueryBuilder queryBuilder)
        {
            try
            {
                this.db = db;
                this.logger = logger;
                this.kernel = kernel;
                this.queryBuilder = queryBuilder;
                this.backupProvider = backupProvider;

                var stopWatch = new Stopwatch();
                stopWatch.Start();

                #region Data Restoration
                IEntityRestorePoint restorePoint = backupProvider.GetRestorePoint(new[] {
                        typeof(UserLoyalty),
                        typeof(ClanRole),
                        //typeof(CharacterClanInvite),
                        typeof(CharacterClanMembership),
                        typeof(ClanSkill),
                        typeof(UserClaimedLoyaltyReward),
                        typeof(UserPatreon),
                        typeof(UserProperty),
                        typeof(Appearance),
                        typeof(SyntyAppearance),
                        typeof(Character),
                        typeof(CharacterState),
                        typeof(InventoryItem),
                        typeof(Item),
                        typeof(User),
                        typeof(ItemAttribute),
                        typeof(RedeemableItem),
                        //typeof(UserNotification),
                        typeof(MarketItemTransaction),
                        typeof(VendorTransaction),
                        typeof(GameSession),
                        typeof(Village),
                        typeof(VillageHouse),
                        typeof(Resources),
                        typeof(Skills),
                        typeof(Statistics),
                        typeof(MarketItem),
                        typeof(ItemCraftingRequirement),
                        typeof(CharacterSessionActivity),
                        typeof(Agreements),
                        typeof(UserBankItem)
                });

                if (restorePoint != null)
                {
                    dataMigration.Migrate(this.db, restorePoint);
                    backupProvider.ClearRestorePoint();
                }
                #endregion

                #region Data Load
                using (var ctx = this.db.Get())
                {
                    agreements = new EntitySet<Agreements, Guid>(restorePoint?.Get<Agreements>() ?? ctx.Agreements.ToList(), i => i.Id);

                    loyalty = new EntitySet<UserLoyalty, Guid>(restorePoint?.Get<UserLoyalty>() ?? ctx.UserLoyalty.ToList(), i => i.Id);
                    loyalty.RegisterLookupGroup(nameof(User), x => x.UserId);
                    loyalty.RegisterLookupGroup("Streamer", x => x.StreamerUserId);

                    redeemableItems = new EntitySet<RedeemableItem, Guid>(restorePoint?.Get<RedeemableItem>() ?? ctx.RedeemableItem.ToList(), i => i.Id);
                    redeemableItems.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    userProperties = new EntitySet<UserProperty, Guid>(restorePoint?.Get<UserProperty>() ?? ctx.UserProperty.ToList(), i => i.Id);
                    userProperties.RegisterLookupGroup(nameof(User), x => x.UserId);

                    loyaltyRanks = new EntitySet<UserLoyaltyRank, Guid>(ctx.UserLoyaltyRank.ToList(), i => i.Id);

                    loyaltyRewards = new EntitySet<UserLoyaltyReward, Guid>(ctx.UserLoyaltyReward.ToList(), i => i.Id);
                    //loyaltyRewards.RegisterLookupGroup(nameof(UserLoyaltyRank), x => x.RankId);

                    claimedLoyaltyRewards = new EntitySet<UserClaimedLoyaltyReward, Guid>(
                        restorePoint?.Get<UserClaimedLoyaltyReward>() ?? ctx.UserClaimedLoyaltyReward.ToList(), i => i.Id);
                    claimedLoyaltyRewards.RegisterLookupGroup(nameof(User), x => x.UserId);
                    claimedLoyaltyRewards.RegisterLookupGroup(nameof(UserLoyaltyReward), x => x.RewardId);
                    claimedLoyaltyRewards.RegisterLookupGroup(nameof(Character), x => x.CharacterId.GetValueOrDefault());

                    characterSessionActivities = new EntitySet<CharacterSessionActivity, Guid>(restorePoint?.Get<CharacterSessionActivity>() ?? ctx.CharacterSessionActivity.ToList(), i => i.Id);
                    characterSessionActivities.RegisterLookupGroup(nameof(GameSession), x => x.SessionId);
                    characterSessionActivities.RegisterLookupGroup(nameof(Character), x => x.CharacterId);
                    characterSessionActivities.RegisterLookupGroup(nameof(User), x => x.UserId);

                    clanInvites = new EntitySet<CharacterClanInvite, Guid>(
                        restorePoint?.Get<CharacterClanInvite>() ?? ctx.CharacterClanInvite.ToList(), i => i.Id);
                    clanInvites.RegisterLookupGroup(nameof(Clan), x => x.ClanId);
                    clanInvites.RegisterLookupGroup(nameof(Character), x => x.CharacterId);
                    clanInvites.RegisterLookupGroup(nameof(User), x => x.InviterUserId.GetValueOrDefault());

                    patreons = new EntitySet<UserPatreon, Guid>(
                        restorePoint?.Get<UserPatreon>() ??
                        ctx.UserPatreon.ToList(), i => i.Id);
                    patreons.RegisterLookupGroup(nameof(User), x => x.UserId.GetValueOrDefault());

                    notifications = new EntitySet<UserNotification, Guid>(
                        restorePoint?.Get<UserNotification>() ??
                        ctx.UserNotification.ToList(), i => i.Id);
                    notifications.RegisterLookupGroup(nameof(User), x => x.UserId);

                    expMultiplierEvents = new EntitySet<ExpMultiplierEvent, Guid>(
                        ctx.ExpMultiplierEvent.ToList(), i => i.Id);

                    appearances = new EntitySet<Appearance, Guid>(
                        restorePoint?.Get<Appearance>() ??
                        ctx.Appearance.ToList(), i => i.Id);

                    syntyAppearances = new EntitySet<SyntyAppearance, Guid>(
                        restorePoint?.Get<SyntyAppearance>() ??
                        ctx.SyntyAppearance.ToList(), i => i.Id);

                    characters = new EntitySet<Character, Guid>(
                        restorePoint?.Get<Character>() ??
                        ctx.Character.ToList(), i => i.Id);

                    characters.RegisterLookupGroup(nameof(User), x => x.UserId);
                    characters.RegisterLookupGroup(nameof(GameSession), x => x.UserIdLock.GetValueOrDefault());

                    characterStates = new EntitySet<CharacterState, Guid>(restorePoint?.Get<CharacterState>() ?? ctx.CharacterState.ToList(), i => i.Id);
                    gameSessions = new EntitySet<GameSession, Guid>(restorePoint?.Get<GameSession>() ?? ctx.GameSession.ToList(), i => i.Id);
                    gameSessions.RegisterLookupGroup(nameof(User), x => x.UserId);

                    // we can still store the game events, but no need to load them on startup as the DB will quickly be filled.
                    // and take a long time to load
                    gameEvents = new EntitySet<GameEvent, Guid>(new List<GameEvent>() /*ctx.GameEvent.ToList()*/, i => i.Id);
                    gameEvents.RegisterLookupGroup(nameof(GameSession), x => x.GameSessionId);
                    gameEvents.RegisterLookupGroup(nameof(User), x => x.UserId);

                    userBankItems = new EntitySet<UserBankItem, Guid>(restorePoint?.Get<UserBankItem>() ?? ctx.UserBankItem.ToList(), i => i.Id);
                    userBankItems.RegisterLookupGroup(nameof(User), x => x.UserId);

                    inventoryItems = new EntitySet<InventoryItem, Guid>(restorePoint?.Get<InventoryItem>() ?? ctx.InventoryItem.ToList(), i => i.Id);
                    inventoryItems.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

                    itemAttributes = new EntitySet<ItemAttribute, Guid>(restorePoint?.Get<ItemAttribute>() ?? ctx.ItemAttribute.ToList(), i => i.Id);

                    marketItems = new EntitySet<MarketItem, Guid>(restorePoint?.Get<MarketItem>() ?? ctx.MarketItem.ToList(), i => i.Id);
                    marketItems.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    items = new EntitySet<Item, Guid>(restorePoint?.Get<Item>() ?? ctx.Item.ToList(), i => i.Id);

                    npcs = new EntitySet<NPC, Guid>(ctx.NPC.ToList(), i => i.Id);
                    npcSpawns = new EntitySet<NPCSpawn, Guid>(ctx.NPCSpawn.ToList(), i => i.Id);
                    npcSpawns.RegisterLookupGroup(nameof(NPC), x => x.NpcId);

                    npcItemDrops = new EntitySet<NPCItemDrop, Guid>(ctx.NPCItemDrop.ToList(), i => i.Id);
                    npcItemDrops.RegisterLookupGroup(nameof(NPC), x => x.NpcId);

                    itemCraftingRequirements = new EntitySet<ItemCraftingRequirement, Guid>(
                        restorePoint?.Get<ItemCraftingRequirement>() ??
                        ctx.ItemCraftingRequirement.ToList(), i => i.Id);
                    itemCraftingRequirements.RegisterLookupGroup(nameof(Item), x => x.ItemId);

                    clans = new EntitySet<Clan, Guid>(
                        restorePoint?.Get<Clan>() ?? ctx.Clan.ToList(), i => i.Id);
                    clans.RegisterLookupGroup(nameof(User), x => x.UserId);

                    clanRoles = new EntitySet<ClanRole, Guid>(
                        restorePoint?.Get<ClanRole>() ?? ctx.ClanRole.ToList(), i => i.Id);
                    clanRoles.RegisterLookupGroup(nameof(Clan), x => x.ClanId);

                    clanMemberships = new EntitySet<CharacterClanMembership, Guid>(
                        restorePoint?.Get<CharacterClanMembership>() ?? ctx.CharacterClanMembership.ToList(), i => i.Id);
                    clanMemberships.RegisterLookupGroup(nameof(Clan), x => x.ClanId);
                    clanMemberships.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

                    villages = new EntitySet<Village, Guid>(
                        restorePoint?.Get<Village>() ??
                        ctx.Village.ToList(), i => i.Id);
                    villages.RegisterLookupGroup(nameof(User), x => x.UserId);

                    villageHouses = new EntitySet<VillageHouse, Guid>(
                        restorePoint?.Get<VillageHouse>() ??
                        ctx.VillageHouse.ToList(), i => i.Id);

                    villageHouses.RegisterLookupGroup(nameof(Village), x => x.VillageId);

                    resources = new EntitySet<Resources, Guid>(
                        restorePoint?.Get<Resources>() ??
                        ctx.Resources.ToList(), i => i.Id);

                    statistics = new EntitySet<Statistics, Guid>(
                        restorePoint?.Get<Statistics>() ??
                        ctx.Statistics.ToList(), i => i.Id);

                    characterSkills = new EntitySet<Skills, Guid>(
                        restorePoint?.Get<Skills>() ??
                        ctx.Skills.ToList(), i => i.Id);

                    clanSkills = new EntitySet<ClanSkill, Guid>(
                        restorePoint?.Get<ClanSkill>() ??
                        ctx.ClanSkill.ToList(), i => i.Id);
                    clanSkills.RegisterLookupGroup(nameof(Clan), x => x.ClanId);



                    vendorTransaction = new EntitySet<VendorTransaction, Guid>(
                        restorePoint?.Get<VendorTransaction>() ??
                        ctx.VendorTransaction.ToList(), i => i.Id);
                    vendorTransaction.RegisterLookupGroup(nameof(Item), x => x.ItemId);
                    vendorTransaction.RegisterLookupGroup(nameof(Character) + "Seller", x => x.SellerCharacterId);

                    marketTransactions = new EntitySet<MarketItemTransaction, Guid>(
                        restorePoint?.Get<MarketItemTransaction>() ??
                        ctx.MarketItemTransaction.ToList(), i => i.Id);
                    marketTransactions.RegisterLookupGroup(nameof(Item), x => x.ItemId);
                    marketTransactions.RegisterLookupGroup(nameof(Character) + "Seller", x => x.SellerCharacterId);
                    marketTransactions.RegisterLookupGroup(nameof(Character) + "Buyer", x => x.BuyerCharacterId);


                    skills = new EntitySet<Skill, Guid>(
                        ctx.Skill.ToList(), i => i.Id);

                    users = new EntitySet<User, Guid>(restorePoint?.Get<User>() ?? ctx.User.ToList(), i => i.Id);

                    gameClients = new EntitySet<GameClient, Guid>(ctx.GameClient.ToList(), i => i.Id);

                    Client = gameClients.Entities.First();

                    entitySets = new IEntitySet[]
                    {
                        redeemableItems,
                        itemAttributes,
                        patreons, loyalty, loyaltyRewards, loyaltyRanks, claimedLoyaltyRewards,
                        expMultiplierEvents, notifications,
                        appearances, syntyAppearances, characters, characterStates,
                        userProperties, vendorTransaction,
                        userBankItems,
                        items, // so we can update items
                        gameSessions, /*gameEvents, */ inventoryItems, marketItems, marketTransactions,
                        resources, statistics, characterSkills, clanSkills, users, villages, villageHouses,
                        clans, clanRoles, clanMemberships, clanInvites, agreements,
                        npcs, npcSpawns, npcItemDrops, itemCraftingRequirements, characterSessionActivities
                    };
                }
                #endregion

                #region Post Data Load - Transformations
                EnsureMagicAttributes();
                EnsureResources();

                //UpgradeSkillLevels(characterSkills);

                RemoveBadUsers(users);
                RemoveBadInventoryItems(inventoryItems);
                EnsureClanLevels(clans);
                EnsureExpMultipliersWithinBounds(expMultiplierEvents);
                EnsureCraftingRequirements(items);
                MergeLoyaltyData(loyalty);
                #endregion

                stopWatch.Stop();
                logger.LogDebug($"All database entries loaded in {stopWatch.Elapsed.TotalSeconds} seconds.");
                logger.LogDebug("GameData initialized... Starting kernel...");
                kernel.Start();
                InitializedSuccessful = true;
                CreateBackup();
                ScheduleNextSave();
            }
            catch (Exception exc)
            {
                InitializedSuccessful = false;
                System.IO.File.WriteAllText("ravenfall-error.log", exc.ToString());
            }

        }

        private void EnsureMagicAttributes()
        {
            if (this.itemAttributes.Entities.Count > 0)
            {
                return;
            }

            for (var i = 0; i < DataModels.Skills.SkillNames.Length; ++i)
            {
                var sn = DataModels.Skills.SkillNames[i];

                Add(new ItemAttribute
                {
                    Id = Guid.NewGuid(),
                    Description = "Increases " + sn + " by 25%",
                    Name = sn.ToUpper(),
                    AttributeIndex = i,
                    DefaultValue = "5%",
                    MaxValue = "25%",
                    MinValue = "1%",
                    Type = 1
                });
            }


            Add(new ItemAttribute
            {
                Id = Guid.NewGuid(),
                Description = "Increases Aim by 20%",
                Name = "AIM",
                AttributeIndex = DataModels.Skills.SkillNames.Length + 1,
                DefaultValue = "5%",
                MaxValue = "20%",
                MinValue = "1%",
                Type = 1
            });

            Add(new ItemAttribute
            {
                Id = Guid.NewGuid(),
                Description = "Increases Power by 20%",
                Name = "POWER",
                AttributeIndex = DataModels.Skills.SkillNames.Length + 2,
                DefaultValue = "5%",
                MaxValue = "20%",
                MinValue = "1%",
                Type = 1
            });

            Add(new ItemAttribute
            {
                Id = Guid.NewGuid(),
                Description = "Increases Armor by 20%",
                Name = "ARMOR",
                AttributeIndex = DataModels.Skills.SkillNames.Length + 3,
                DefaultValue = "5%",
                MaxValue = "20%",
                MinValue = "1%",
                Type = 1
            });

        }

        private void EnsureResources()
        {
            foreach (var character in this.characters.Entities)
            {
                var resources = this.GetResourcesByCharacterId(character.Id);
                if (resources == null)
                {
                    resources = new DataModels.Resources
                    {
                        Id = Guid.NewGuid(),
                    };
                    Add(resources);
                    character.ResourcesId = resources.Id;
                }
            }
        }
        #endregion

        #region Data Transformations

        private void EnsureCraftingRequirements(EntitySet<Item, Guid> items)
        {

            Item GetItemByCategory(ItemCategory category, string containsName)
            {
                return items.Entities.FirstOrDefault(x => (ItemCategory)x.Category == ItemCategory.Resource && x.Name.Contains(containsName, StringComparison.OrdinalIgnoreCase));
            }

            var etherCraftingLevel = 280;
            var lionCraftingLevel = 240;
            var phantomCraftingLevel = 200; // change to 210 ?


            var ingot = GetItemByCategory(ItemCategory.Resource, "ore ingot");
            var wood = GetItemByCategory(ItemCategory.Resource, "wood plank");
            var gold = GetItemByCategory(ItemCategory.Resource, "gold");
            foreach (var item in items.Entities)
            {

                // Make lionsbane craftable

                var nl = item.Name.ToLower();
                if (item.RequiredCraftingLevel >= 1000)
                {
                    item.RequiredCraftingLevel = 1000;
                    item.Craftable = false;
                }

                if (item.RequiredCraftingLevel < 1000)
                {
                    item.Craftable = true;
                    var requirements = GetCraftingRequirements(item.Id) ?? new List<ItemCraftingRequirement>();
                    if (requirements != null && requirements.Count > 0 || item.WoodCost > 0 || item.OreCost > 0)
                    {
                        if (requirements != null && requirements.Count > 0)
                        {
                            foreach (var req in requirements)
                            {
                                if (req.Amount == 0)
                                {
                                    req.Amount = 3;
                                }
                            }
                        }

                        //continue;
                    }

                    Item resType = null;
                    var type = (ItemType)item.Type;
                    var ingotCount = 0;
                    var woodCount = (type == ItemType.TwoHandedStaff || type == ItemType.TwoHandedBow || type == ItemType.TwoHandedSword || type == ItemType.Shield) ? 5 : 0;
                    var goldCount = 0;
                    var resCount = 0;

                    if (nl.Contains("emerald"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "emerald");
                        ingotCount = 5;
                    }
                    if (nl.Contains("ruby"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "ruby");
                        ingotCount = 5;
                    }
                    if (nl.Contains("bronze"))
                    {
                        resType = ingot;
                    }
                    if (nl.Contains("iron"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "iron nugget");
                        ingotCount = 5;
                    }
                    if (nl.Contains("steel"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "steel nugget");
                        ingotCount = 5;
                    }
                    if (nl.Contains("gold "))
                    {
                        resType = gold;
                        ingotCount = 5;
                    }
                    if (nl.Contains("mithril"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "mithril nugget");
                        ingotCount = 10;
                        woodCount = woodCount * 2;
                    }
                    if (nl.Contains("adamantite"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "adamantite nugget");
                        ingotCount = 15;
                        woodCount = woodCount * 3;
                    }
                    if (nl.Contains("rune"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "rune nugget");
                        ingotCount = 25;
                        woodCount = woodCount * 5;
                    }
                    if (nl.Contains("dragon"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "dragon scale");
                        ingotCount = 35;
                        woodCount = woodCount * 7;
                    }
                    if (nl.Contains("abraxas"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "abraxas spirit");
                        ingotCount = 60;
                        woodCount = woodCount * 12;
                    }

                    if (nl.Contains("phantom"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "phantom core");
                        ingotCount = 75;
                        woodCount = woodCount * 15;
                        item.RequiredCraftingLevel = phantomCraftingLevel;
                    }

                    if (nl.Contains("lionsbane"))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "lionite");
                        ingotCount = 90;
                        woodCount = woodCount * 18;
                        resCount = 3;
                        item.RequiredCraftingLevel = lionCraftingLevel;
                    }

                    if (nl.StartsWith("ether "))
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "ethereum");
                        ingotCount = 120;
                        woodCount = woodCount * 24;
                        resCount = 5;
                        item.RequiredCraftingLevel = etherCraftingLevel;
                    }
                    switch (type)
                    {
                        case ItemType.Amulet:
                            resCount += 1;
                            goldCount = 10;
                            break;

                        case ItemType.Ring:
                            resCount += 1;
                            goldCount = 5;
                            break;

                        case ItemType.OneHandedSword:
                            resCount += 3;
                            break;

                        case ItemType.TwoHandedAxe:
                            resCount += 4;
                            break;
                        case ItemType.TwoHandedSword:
                            resCount += 5;
                            break;

                        case ItemType.TwoHandedBow:
                            resCount += 4;
                            break;

                        case ItemType.TwoHandedStaff:
                            resCount += 4;
                            break;

                        case ItemType.Helmet:
                            resCount += 3;
                            break;
                        case ItemType.Chest:
                            resCount += 5;
                            break;
                        case ItemType.Leggings:
                            resCount += 4;
                            break;
                        case ItemType.Gloves:
                        case ItemType.Boots:
                            resCount += 3;
                            break;
                        case ItemType.Shield:
                            resCount += 4;
                            break;
                    }


                    //if (resType == null || ingot == null) continue;

                    if (ingotCount > 0 && ingot != null)
                    {

                        AddOrReplace(requirements, new ItemCraftingRequirement()
                        {
                            Id = Guid.NewGuid(),
                            Amount = ingotCount,
                            ItemId = item.Id,
                            ResourceItemId = ingot.Id
                        });
                    }

                    if (woodCount > 0 && wood != null)
                    {
                        AddOrReplace(requirements, new ItemCraftingRequirement()
                        {
                            Id = Guid.NewGuid(),
                            Amount = woodCount,
                            ItemId = item.Id,
                            ResourceItemId = wood.Id
                        });
                    }

                    if (goldCount > 0 && gold != null)
                    {
                        AddOrReplace(requirements, new ItemCraftingRequirement()
                        {
                            Id = Guid.NewGuid(),
                            Amount = goldCount,
                            ItemId = item.Id,
                            ResourceItemId = gold.Id
                        });
                    }

                    if (resCount > 0 && resType != null)
                    {
                        AddOrReplace(requirements, new ItemCraftingRequirement()
                        {
                            Id = Guid.NewGuid(),
                            Amount = resCount,
                            ItemId = item.Id,
                            ResourceItemId = resType.Id
                        });
                    }
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

        private void EnsureExpMultipliersWithinBounds(EntitySet<ExpMultiplierEvent, Guid> expMultiplierEvents)
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

        private void EnsureClanLevels(EntitySet<Clan, Guid> clans)
        {
            foreach (var clan in clans.Entities)
            {
                if (clan.Level == 0)
                    clan.Level = 1;
            }
        }


        private void MergeLoyaltyData(EntitySet<UserLoyalty, Guid> loyalty)
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

        private void RemoveBadInventoryItems(EntitySet<InventoryItem, Guid> inventoryItems)
        {
            var toRemove = new List<InventoryItem>();
            foreach (var ii in inventoryItems.Entities)
            {
                if (ii.Amount <= 0) toRemove.Add(ii);
            }

            foreach (var bad in toRemove)
            {
                Remove(bad);
            }
        }

        private void RemoveBadUsers(EntitySet<User, Guid> users)
        {
            var toRemove = new List<User>();
            foreach (var user in users.Entities)
            {
                if (string.IsNullOrEmpty(user.UserName) || Guid.TryParse(user.UserId, out var guid))
                {
                    toRemove.Add(user);
                }
            }

            foreach (var badUser in toRemove)
            {
                Remove(badUser);
            }
        }

        private void UpgradeSkillLevels(EntitySet<Skills, Guid> skills)
        {
            // total exp 170: 0
            // 170 + overflow

            // (total) exp required for current level
            // 170: 170totalexp - total exp for current level
            // 

            var skillsChanged = false;
            foreach (var skill in skills.Entities)
            {
                var data = skill.GetSkills();
                foreach (var s in data)
                {
                    var lv = s.Level;

                    if (lv > GameMath.MaxLevel)
                    {
                        s.Level = GameMath.MaxLevel;
                        //s.Experience = 0;
                        skillsChanged = true;
                        continue;
                    }

                    if (lv > 0)
                        continue;

                    lv = 1;
                    var xp = s.Experience;
                    var expForNextLevel = GameMath.ExperienceForLevel(lv + 1);
                    while (xp >= expForNextLevel)
                    {
                        xp -= expForNextLevel;
                        ++lv;
                        expForNextLevel = GameMath.ExperienceForLevel(lv + 1);
                    }

                    s.Experience = xp;
                    s.Level = lv;
                    skillsChanged = true;
                }
            }

            if (skillsChanged)
            {
                ScheduleNextSave();
            }
        }

        #endregion

        #region Add Methods


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(ItemAttribute item) => Update(() => itemAttributes.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Agreements item) => Update(() => agreements.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(UserBankItem item) => Update(() => userBankItems.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(RedeemableItem item) => Update(() => redeemableItems.Add(item));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(UserNotification entity) => Update(() => notifications.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(ClanSkill entity) => Update(() => clanSkills.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(MarketItemTransaction entity) => Update(() => marketTransactions.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(VendorTransaction entity) => Update(() => vendorTransaction.Add(entity));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(CharacterClanInvite entity) => Update(() => this.clanInvites.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Clan entity) => Update(() => this.clans.Add(entity));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(ClanRole entity) => Update(() => this.clanRoles.Add(entity));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(CharacterClanMembership entity) => Update(() => this.clanMemberships.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(UserLoyalty loyalty) => Update(() => this.loyalty.Add(loyalty));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(UserLoyaltyRank loyaltyRank) => Update(() => loyaltyRanks.Add(loyaltyRank));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(UserLoyaltyReward loyaltyRankReward) => Update(() => loyaltyRewards.Add(loyaltyRankReward));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(UserPatreon ev) => Update(() => patreons.Add(ev));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(ExpMultiplierEvent ev) => Update(() => expMultiplierEvents.Add(ev));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(CharacterSessionActivity ev) => Update(() => characterSessionActivities.Add(ev));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(ItemCraftingRequirement entity) => Update(() => itemCraftingRequirements.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(VillageHouse house) => Update(() => villageHouses.Add(house));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Village entity) => Update(() => villages.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Item entity) => Update(() => items.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(CharacterState entity) => Update(() => characterStates.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(SyntyAppearance entity) => Update(() => syntyAppearances.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Statistics entity) => Update(() => statistics.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Skills entity) => Update(() => characterSkills.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Appearance entity) => Update(() => appearances.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Resources entity) => Update(() => resources.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Character entity) => Update(() => characters.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(User entity) => Update(() => users.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(InventoryItem entity) => Update(() => inventoryItems.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(GameSession entity) => Update(() => gameSessions.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(MarketItem entity) => Update(() => marketItems.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(GameEvent entity) => Update(() => gameEvents.Add(entity));

        public GameSession CreateSession(Guid userId)
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

        public GameEvent CreateSessionEvent<T>(GameEventType type, GameSession session, T data)
        {
            session.Updated = DateTime.UtcNow;
            var ev = new GameEvent
            {
                Id = Guid.NewGuid(),
                GameSessionId = session.Id,
                UserId = session.UserId,
                Type = (int)type,
                Revision = GetNextGameEventRevision(session.Id),
                Data = JSON.Stringify(data)
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
                var villageResources = new Resources()
                {
                    Id = Guid.NewGuid()
                };

                Add(villageResources);


                var user = GetUser(session.UserId);
                var villageExp = user.IsAdmin.GetValueOrDefault()
                    ? GameMath.OLD_LevelToExperience(30)
                    : 0;
                var villageLevel = GameMath.OLD_ExperienceToLevel(villageExp);

                village = new Village()
                {
                    Id = Guid.NewGuid(),
                    ResourcesId = villageResources.Id,
                    Level = villageLevel,
                    Experience = (long)villageExp,
                    Name = "Village",
                    UserId = session.UserId
                };

                Add(village);
            }

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
                return new VillageHouse[0];
            }

            var houseCount = village.Level / 10;

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
            ConcurrentDictionary<Guid, CharacterSessionState> states;

            if (!characterSessionStates.TryGetValue(sessionId, out states))
            {
                states = new ConcurrentDictionary<Guid, CharacterSessionState>();
            }

            CharacterSessionState state;
            if (!states.TryGetValue(characterId, out state))
            {
                state = new CharacterSessionState();
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
        public GameSession GetSessionByCharacterId(Guid characterId)
        {
            var character = characters.Entities.FirstOrDefault(x => x.Id == characterId);
            if (character == null || character.UserIdLock == null) return null;
            var sessionOwner = GetUser(character.UserIdLock.Value);
            if (sessionOwner == null) return null;
            return GetActiveSessions().FirstOrDefault(x => x.UserId == sessionOwner.Id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession GetOwnedSessionByUserId(string userId)
        {
            var user = users.Entities.FirstOrDefault(x => x.UserId == userId);
            if (user == null) return null;

            return GetActiveSessions().FirstOrDefault(x => x.UserId == user.Id);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameSession GetJoinedSessionByUserId(string userId)
        {
            var user = users.Entities.FirstOrDefault(x => x.UserId == userId);
            if (user == null) return null;

            var character = characters.Entities.FirstOrDefault(x => x.UserId == user.Id);
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

            return users.Entities.FirstOrDefault(x =>
                    x != null &&
                    (x.UserId == userIdOrUsername ||
                    (x.UserName?.Equals(userIdOrUsername, StringComparison.OrdinalIgnoreCase) ?? false)));
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
        public IReadOnlyList<UserBankItem> GetUserBankItems(Guid id)
            => userBankItems[nameof(User), id].ToList();


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
        public Character GetCharacterByName(string name, string identifier)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Character GetCharacterBySession(Guid sessionId, string userId, bool updateSession = true)
        {
            var session = GetSession(sessionId, updateSession);
            var characters = GetSessionCharacters(session);
            return characters.FirstOrDefault(x => GetUser(x.UserId)?.UserId == userId);
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
            characters.Entities.Where(predicate).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Character> GetCharacters() => characters.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<User> GetUsers() => users.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Clan> GetClans() => clans.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetEquippedItem(Guid characterId, Guid itemId) =>
            inventoryItems[nameof(Character), characterId]
                .FirstOrDefault(x => x.Equipped && x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetInventoryItem(Guid inventoryItemId) => inventoryItems[inventoryItemId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetInventoryItem(Guid characterId, Guid itemId) =>
            inventoryItems[nameof(Character), characterId]
               .FirstOrDefault(x => !x.Equipped && x.ItemId == itemId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetEquippedItems(Guid characterId) =>
            inventoryItems[nameof(Character), characterId]
                    .Where(x => x.Equipped)
                    .ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<InventoryItem> GetInventoryItems(Guid characterId, Guid itemId) =>
            inventoryItems[nameof(Character), characterId]
                    .Where(x => !x.Equipped && x.ItemId == itemId)
                    .ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Item GetItem(Guid id) => items[id];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Item> GetItems() => items.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ItemAttribute> GetItemAttributes() => itemAttributes.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions() => marketTransactions.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions(DateTime start, DateTime end) => marketTransactions.Entities.Where(x => x.Created >= start && x.Created <= end).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions(Guid itemId, DateTime start, DateTime end) => marketTransactions[nameof(Item), itemId].Where(x => x.Created >= start && x.Created <= end).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactionsBySeller(Guid seller, DateTime start, DateTime end) => marketTransactions[nameof(Character) + "Seller", seller].Where(x => x.Created >= start && x.Created <= end).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactionsByBuyer(Guid buyer, DateTime start, DateTime end) => marketTransactions[nameof(Character) + "Buyer", buyer].Where(x => x.Created >= start && x.Created <= end).ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMarketItemCount() => marketItems.Entities.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItem> GetMarketItems(Guid itemId, string tag = null)
        {
            if (string.IsNullOrEmpty(tag))
                return marketItems[nameof(Item), itemId];

            return marketItems[nameof(Item), itemId].Where(x => x.Tag == tag).ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DataModels.MarketItem GetMarketItem(Guid marketItemId)
        {
            return marketItems[marketItemId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<MarketItem> GetMarketItems(int skip, int take) =>
            marketItems.Entities.Skip(skip).Take(take).ToList();

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
        public IReadOnlyList<Character> GetSessionCharacters(GameSession currentSession, bool activeSessionOnly = true)
        {
            if (currentSession == null) return null;
            if (activeSessionOnly)
                return characters[nameof(GameSession), currentSession.UserId]
                    .Where(x => GetUser(x.UserId) != null && x.UserIdLock == currentSession.UserId && x.LastUsed >= currentSession.Started)
                    .OrderByDescending(x => x.LastUsed)
                    .ToList();

            // in case we need to know all characters that has been locked to this user (based on sessionId).
            // so we can clear those users out if necessary.
            // note(zerratar): should be a separate method. Not part of this As we want to ensure we only get the real active players.
            return characters[nameof(GameSession), currentSession.UserId].OrderByDescending(x => x.LastUsed).Where(x => GetUser(x.UserId) != null).ToList();
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
        public ExpMultiplierEvent GetActiveExpMultiplierEvent() =>
            this.expMultiplierEvents?.Entities?
            .Where(x => x.StartTime <= DateTime.UtcNow && x.EndTime >= DateTime.UtcNow)
            .OrderByDescending(x => x.Multiplier)
            .FirstOrDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameEvent> GetSessionEvents(GameSession gameSession) =>
            GetSessionEvents(gameSession.Id);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameEvent> GetSessionEvents(Guid sessionId) =>
            gameEvents[nameof(GameSession), sessionId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<DataModels.UserNotification> GetNotifications(Guid userId)
            => notifications[nameof(User), userId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameEvent> GetUserEvents(Guid userId) =>
            gameEvents[nameof(User), userId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User GetUser(Guid userId) => users[userId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User GetUserByTwitchId(string twitchUserId)
        {
            twitchUserId = twitchUserId?.ToLower()?.Trim();
            if (string.IsNullOrEmpty(twitchUserId)) return null;
            return users.Entities.FirstOrDefault(x => x.UserId != null
                && x.UserId.Equals(twitchUserId, StringComparison.OrdinalIgnoreCase));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public User GetUserByUsername(string username)
        {
            username = username?.ToLower()?.Trim();
            if (string.IsNullOrEmpty(username)) return null;
            return users.Entities.FirstOrDefault(x => x.UserName != null && x.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));
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
            patreons[nameof(User), userId].FirstOrDefault();

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
        public GameSession GetSessionByUserId(Guid userId, bool updateSession = true)
        {
            var session = gameSessions[nameof(User), userId]
                    .OrderByDescending(x => x.Started)
                    .FirstOrDefault(x => x.Stopped == null);
            if (updateSession && session != null) session.Updated = DateTime.UtcNow;
            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ICollection<RedeemableItem> GetRedeemableItems() => redeemableItems.Entities;

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
        public Resources GetResourcesByCharacterId(Guid sellerCharacterId) =>
            GetResources(GetCharacter(sellerCharacterId).ResourcesId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Statistics GetStatistics(Guid statisticsId) => statistics[statisticsId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SyntyAppearance GetAppearance(Guid? syntyAppearanceId) =>
            syntyAppearanceId == null ? null : syntyAppearances[syntyAppearanceId.Value];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Skills GetCharacterSkills(Guid skillsId) => characterSkills[skillsId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Skill> GetSkills() => skills.Entities.ToList();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Skill GetSkill(Guid skillId) => skills[skillId];
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
        public Clan GetClanByUser(Guid userId)
        {
            return clans[nameof(User), userId].FirstOrDefault();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterClanMembership GetClanMembership(Guid characterId) =>
            clanMemberships[nameof(Character), characterId].FirstOrDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClanRole GetClanRole(Guid roleId) =>
            clanRoles[roleId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ClanRole> GetClanRoles(Guid clanId) =>
            clanRoles[nameof(Clan), clanId];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterState GetCharacterState(Guid? stateId) => stateId == null ? null : characterStates[stateId.Value];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameSession> GetActiveSessions() => gameSessions.Entities
                    .OrderByDescending(x => x.Started)
                    .Where(x => x.Stopped == null && DateTime.UtcNow - x.Updated <= TimeSpan.FromSeconds(SessionTimeoutSeconds)).ToList();

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
        public Village GetVillageBySession(GameSession session) => villages[nameof(User), session.UserId].FirstOrDefault();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Village GetVillageByUserId(Guid userId) => villages[nameof(User), userId].FirstOrDefault();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Village GetVillage(Guid villageId) => villages[villageId];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Village> GetVillages() => villages.Entities.ToList();
        #endregion

        #region Remove Entities

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(UserBankItem item) => userBankItems.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(Agreements item) => agreements.Remove(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(RedeemableItem entity) => redeemableItems.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(UserNotification entity) => notifications.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(UserLoyalty entity) => loyalty.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(Clan entity) => this.clans.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(CharacterClanInvite entity) => this.clanInvites.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(ClanRole entity) => this.clanRoles.Remove(entity);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(CharacterClanMembership entity) => this.clanMemberships.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(CharacterSessionActivity ev) => characterSessionActivities.Remove(ev);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(GameEvent ev) => gameEvents.Remove(ev);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(ItemCraftingRequirement entity) => itemCraftingRequirements.Remove(entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(User user) => users.Remove(user);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(Skills skill) => characterSkills.Remove(skill);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(Statistics stat) => statistics.Remove(stat);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(Character character) => characters.Remove(character);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(Resources res) => resources.Remove(res);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(MarketItem marketItem) => marketItems.Remove(marketItem);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(InventoryItem invItem) => inventoryItems.Remove(invItem);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(IReadOnlyList<InventoryItem> items) => items.ForEach(Remove);

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
            kernel.ClearTimeout(scheduleHandler);
            scheduleHandler = null;
            try
            {
                lock (SyncLock)
                {
                    logger.LogDebug("Saving all pending changes to the database.");

                    var queue = BuildSaveQueue();

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

                            var command = con.CreateCommand();
                            command.CommandText = query.Command;
                            var result = command.ExecuteNonQuery();
                            //if (result < saveData.Entities.Count)
                            //{
                            //    logger.LogError($"Unable to save all data in batch: {result} / {saveData.Entities.Count}. Creating restore point.");
                            //    CreateBackup();
                            //    backupProvider.CreateRestorePoint(entitySets);
                            //    return;
                            //}

                            ClearChangeSetState(saveData);
                            queue.Dequeue();
                            con.Close();
                        }
                    }

                    backupProvider.ClearRestorePoint();
                }
            }
            catch (SqlException exc)
            {
                CreateBackup();
                backupProvider.CreateRestorePoint(entitySets);
                logger.LogError("ERROR SAVING DATA (CREATING RESTORE POINT!!) " + exc);
            }
            catch (Exception exc)
            {
                logger.LogError("ERROR SAVING DATA!! " + exc);
                // log this
            }
            finally
            {
                ScheduleNextSave();
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

        private Queue<EntityStoreItems> BuildSaveQueue()
        {
            var queue = new Queue<EntityStoreItems>();
            var addedItems = JoinChangeSets(entitySets.Select(x => x.Added).ToArray());
            foreach (var batch in CreateBatches(EntityState.Added, addedItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            var updateItems = JoinChangeSets(entitySets.Select(x => x.Updated).ToArray());
            foreach (var batch in CreateBatches(EntityState.Modified, updateItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            var deletedItems = JoinChangeSets(entitySets.Select(x => x.Removed).ToArray());
            foreach (var batch in CreateBatches(EntityState.Deleted, deletedItems, SaveMaxBatchSize))
            {
                queue.Enqueue(batch);
            }

            return queue;
        }

        private ICollection<EntityStoreItems> CreateBatches(RavenNest.DataModels.EntityState state, ICollection<EntityChangeSet> items, int batchSize)
        {
            if (items == null || items.Count == 0) return new List<EntityStoreItems>();
            var batches = (int)Math.Floor(items.Count / (float)batchSize) + 1;
            var batchList = new List<EntityStoreItems>(batches);
            for (var i = 0; i < batches; ++i)
            {
                batchList.Add(new EntityStoreItems(state, items.Skip(i * batchSize).Take(batchSize).Select(x => x.Entity).ToList()));
            }
            return batchList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ICollection<EntityChangeSet> JoinChangeSets(params ICollection<EntityChangeSet>[] changesets) =>
            changesets.SelectMany(x => x).OrderBy(x => x.LastModified).ToList();




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
    }

    public class DataSaveError
    {
    }

    #endregion

    public class BotStats
    {
        public UInt32 CommandsPerSecondsMax;
        public UInt32 JoinedChannelsCount;
        public UInt32 UserCount;
        public UInt32 ConnectionCount;
        public UInt32 SessionCount;

        public UInt64 TotalCommandCount;
        public double CommandsPerSecondsDelta;

        public TimeSpan Uptime;
        public DateTime LastSessionStarted;
        public DateTime LastSessionEnded;
        public DateTime Started;
        public DateTime LastUpdated;
        public TimeSpan TimeSinceUpdate => DateTime.UtcNow - LastUpdated;
    }
}
