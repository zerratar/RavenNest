using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Game.Processors.Tasks;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;


namespace RavenNest.BusinessLogic.Data
{
    public class GameData
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

        private readonly EntitySet<Agreements> agreements;

        private readonly EntitySet<UserLoyalty> loyalty;
        private readonly EntitySet<UserProperty> userProperties;
        private readonly EntitySet<UserLoyaltyRank> loyaltyRanks;
        private readonly EntitySet<UserLoyaltyReward> loyaltyRewards;
        private readonly EntitySet<UserClaimedLoyaltyReward> claimedLoyaltyRewards;
        private readonly EntitySet<UserNotification> notifications;

        private readonly EntitySet<CharacterClanInvite> clanInvites;

        private readonly EntitySet<Clan> clans;
        private readonly EntitySet<ClanRole> clanRoles;
        private readonly EntitySet<ClanSkill> clanSkills;
        private readonly EntitySet<ClanRolePermissions> clanRolePermissions;
        private readonly EntitySet<CharacterClanSkillCooldown> characterClanSkillCooldown;

        private readonly EntitySet<MarketItemTransaction> marketTransactions;
        //private readonly EntitySet<VendorTransaction> vendorTransaction;

        private readonly EntitySet<CharacterClanMembership> clanMemberships;

        private readonly EntitySet<UserPatreon> patreons;
        private readonly EntitySet<UserAccess> userAccess;
        private readonly EntitySet<CharacterSessionActivity> characterSessionActivities;
        private readonly EntitySet<Appearance> appearances;
        private readonly EntitySet<SyntyAppearance> syntyAppearances;
        private readonly EntitySet<Character> characters;
        private readonly EntitySet<CharacterState> characterStates;
        private readonly EntitySet<GameSession> gameSessions;
        private readonly EntitySet<ExpMultiplierEvent> expMultiplierEvents;
        private readonly EntitySet<GameEvent> gameEvents;


        private readonly EntitySet<Pet> pets;

        private readonly EntitySet<UserBankItem> userBankItems;
        private readonly EntitySet<InventoryItem> inventoryItems;
        private readonly EntitySet<ResourceItemDrop> resourceItemDrops;

        private readonly EntitySet<ItemAttribute> itemAttributes;

        private readonly EntitySet<RedeemableItem> redeemableItems;

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
                        typeof(Appearance),
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
                        //typeof(UserNotification),
                        typeof(MarketItemTransaction),
                        //typeof(VendorTransaction),
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
                        typeof(UserBankItem),
                        typeof(ExpMultiplierEvent)
                });


                logger.LogInformation($"Checking for restore points.");
                if (restorePoint != null)
                {
                    dataMigration.Migrate(this.db, restorePoint);
                    backupProvider.ClearRestorePoint();
                }
                #endregion

                logger.LogInformation($"Loading dataset from database.");
                #region Data Load
                using (var ctx = this.db.Get())
                {
                    agreements = new EntitySet<Agreements>(restorePoint?.Get<Agreements>() ?? ctx.Agreements.ToList());

                    resourceItemDrops = new EntitySet<ResourceItemDrop>(restorePoint?.Get<ResourceItemDrop>() ?? ctx.ResourceItemDrop.ToList());

                    patreonSettings = new EntitySet<PatreonSettings>(restorePoint?.Get<PatreonSettings>() ?? ctx.PatreonSettings.ToList());

                    loyalty = new EntitySet<UserLoyalty>(restorePoint?.Get<UserLoyalty>() ?? ctx.UserLoyalty.ToList());
                    loyalty.RegisterLookupGroup(nameof(User), x => x.UserId);
                    loyalty.RegisterLookupGroup("Streamer", x => x.StreamerUserId);

                    pets = new EntitySet<Pet>(restorePoint?.Get<Pet>() ?? ctx.Pet.ToList());
                    pets.RegisterLookupGroup(nameof(Character), x => x.CharacterId);

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

                    appearances = new EntitySet<Appearance>(
                        restorePoint?.Get<Appearance>() ??
                        ctx.Appearance.ToList());

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


                    //vendorTransaction = new EntitySet<VendorTransaction>(
                    //    restorePoint?.Get<VendorTransaction>() ??
                    //    ctx.VendorTransaction.ToList());
                    //vendorTransaction.RegisterLookupGroup(nameof(Item), x => x.ItemId);
                    //vendorTransaction.RegisterLookupGroup(nameof(Character) + "Seller", x => x.SellerCharacterId);

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
                        itemAttributes,
                        pets,
                        patreons, loyalty, loyaltyRewards, loyaltyRanks, claimedLoyaltyRewards,
                        expMultiplierEvents, notifications,
                        appearances, syntyAppearances, characters, characterStates,
                        userProperties, /*vendorTransaction,*/
                        userBankItems,
                        characterSkillRecords,
                        clanRolePermissions,
                        characterClanSkillCooldown,
                        patreonSettings,
                        resourceItemDrops,
                        gameClients,
                        userAccess,
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
                EnsureCharacterSkillRecords();
                EnsureMagicAttributes();
                EnsureResources();

                //UpgradeSkillLevels(characterSkills);
                //RemoveBadUsers(users);

                RemoveBadInventoryItems(inventoryItems);

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
                //ApplyVendorPrices();

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

        public List<List<User>> GetDuplicateUsers()
        {
            return users.Entities.GroupBy(u => u.UserName)
                .Where(group => group.Count() > 1)
                .Select(group => group.ToList())
                .ToList();
        }

        public void MergeAccounts()
        {
            var duplicates = GetDuplicateUsers();

            foreach (var group in duplicates)
            {
                // Merge all the users in the group, this will put all characters, user bank items, etc into one user account
                var mergedUser = MergeUser(group);

                // with all merged characters, which may just as well be beyond the maximum of 3.
                // start removing them
                var characters = GetCharactersByUserId(mergedUser.Id);
                foreach (var c in characters)
                {
                    RemoveCharacterIfEmpty(c);
                }

                // now with removed characters in first step, get the updated list of characters
                characters = GetCharactersByUserId(mergedUser.Id);

                // if we have more than 3 characters, we have to merge characters if possible.
                if (characters.Count > 3)
                {
                    // do not merge right now, its going to be too risky.
                }

                // finally, with one last step, we will assign new character indices
                characters = GetCharactersByUserId(mergedUser.Id);
                var index = 0;
                foreach (var c in characters.OrderBy(x => x.Created))
                {
                    c.CharacterIndex = index++;
                }
            }
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

        private void RemoveUser(User user)
        {
            var props = GetUserProperties(user.Id);
            foreach (var prop in props)
            {
                Remove(prop);
            }

            var uac = GetUserAccess(user.Id);
            foreach (var a in uac)
            {
                Remove(a);
            }
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

        private void RewardRollbackPlayers()
        {
            string[] data = null;
            if (System.IO.File.Exists("changes.txt"))
            {
                // rename this shit.
                var dt = DateTime.UtcNow;
                data = System.IO.File.ReadAllLines("changes.txt");
                System.IO.File.Move("changes.txt", $"restored-changes_{dt:yyyy-MM-dd_hhMMss}.txt", true);
            }

            if (data == null)
                return;


            Guid[] rewardItems = new Guid[] {
                Guid.Parse("AD431ADB-9001-4BAF-8125-1D0D0525A247"),
                Guid.Parse("9C364DAF-7985-42DB-A116-2D58139D14A3"),
                Guid.Parse("1E9D818D-6EEE-46D5-B47F-2E2ECDC2FEDE"),
                Guid.Parse("D28DC33E-E256-4DAF-B0A7-3487E7E1532A"),
                Guid.Parse("704D9F4D-7CF5-4491-8950-7383F81B5F30"),
                Guid.Parse("F927197A-5DDF-4CA4-95F1-871FA3A49BE6"),
                Guid.Parse("BF4F3CD5-B126-4588-9F35-885B94C240A0"),
                Guid.Parse("9ABF2A4B-9D6F-4893-8FEC-8F17C28CD4CD"),
                Guid.Parse("C35032FB-79E4-4DEB-ADD3-9E3AAEA27060"),
                Guid.Parse("79C1E673-93D3-4DA3-A48E-A58E4A2FD1AD"),
                Guid.Parse("AC8DBEC9-3D49-4294-88D2-B820BA7393D5"),
                Guid.Parse("35EC855D-EEC7-4737-BF04-B8AFD6051E41"),
                Guid.Parse("B18A15B1-629F-4B8C-920E-BAB3DDBE877B"),
                Guid.Parse("41A8E82D-3DE3-45D9-967B-BC93C447406D"),
                Guid.Parse("B8DE84CE-1C1F-4509-A505-D3A893398138"),
                Guid.Parse("EFCE022C-54E3-42ED-8F7B-E34C46F0187E"),
                Guid.Parse("612A99C9-8AE6-4422-AFDC-EA58056F7FAA"),
                Guid.Parse("E40AFF21-A40C-4013-99A4-F4DB5AB6296A"),
                Guid.Parse("B2DD7C43-9F9F-4A77-A702-F9DD23CBDF2D")
            };
            var random = new Random();

            var rewardCount = 0;

            for (var i = 0; i < data.Length; ++i)
            {
                // go through each line, check which player affected. Reward random item out of latest hats.
                var line = data[i];
                if (line.IndexOf('#') > 0)
                {
                    var d = line.Split('#');
                    var name = d[0];
                    var index = d[1];
                    var c = GetCharacterByName(name, index);
                    if (c != null)
                    {
                        var itemId = rewardItems[random.Next(0, rewardItems.Length)];

                        var ii = new InventoryItem
                        {
                            Amount = 1,
                            CharacterId = c.Id,
                            Id = Guid.NewGuid(),
                            ItemId = itemId,
                        };

                        rewardCount++;

                        Add(ii);
                    }
                }
            }

            if (rewardCount > 0)
            {
                logger.LogError("(Not actual error) " + rewardCount + " items was rewarded to various players.");
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

                var appearance = GetAppearance(c.AppearanceId);
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

        private void EnsureMagicAttributes()
        {
            if (this.itemAttributes.Entities.Count > 0)
            {
                return;
            }

            logger.LogInformation($"Restoring magic attributes.");

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

            if (resourceItemDrops.Entities.Count == 0)
            {
                foreach (var drop in ResourceTaskProcessor.DefaultDroppableResources)
                {
                    Add(new ResourceItemDrop
                    {
                        Id = Guid.NewGuid(),
                        DropChance = drop.DropChance,
                        ItemId = drop.Id,
                        ItemName = drop.Name,
                        LevelRequirement = drop.SkillLevel,

                    });
                }
            }

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

        private long GetResourceCost(ItemMaterial material)
        {
            switch (material)
            {
                case ItemMaterial.Bronze: return 10;
                case ItemMaterial.Iron: return 150;
                case ItemMaterial.Steel: return 500;
                case ItemMaterial.Black: return 1000;
                case ItemMaterial.Mithril: return 2000;
                case ItemMaterial.Adamantite: return 3500;
                case ItemMaterial.Rune: return 6000;
                case ItemMaterial.Dragon: return 10000;
                case ItemMaterial.Ultima: return 20000;
                case ItemMaterial.Phantom: return 35000;
                case ItemMaterial.Lionsbane: return 50000;
                case ItemMaterial.Ether: return 60000;
                case ItemMaterial.Ancient: return 75000;
                case ItemMaterial.Atlarus: return 100_000;
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
                    return ItemMaterial.Ultima;
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

                var material = GetMaterial(item);
                var resourceCost = GetResourceCost(material);
                if (resourceCost > 1)
                {
                    item.ShopSellPrice = resourceCost;
                }
            }

            foreach (var item in items.Entities)
            {
                // if items has crafting materials.
                // calculate vendor price based that.

                if (item.ShopSellPrice == 0)
                {
                    item.ShopSellPrice = 1; // minimum price.
                }

                if (item.Category == (int)ItemCategory.Resource)
                {
                    continue;
                }

                var itemMaterial = GetMaterial(item);
                var lower = item.Name.ToLower();
                var craftable = item.Craftable.GetValueOrDefault();
                if (item.ShopSellPrice == 1 || craftable) // phantom is special case, as we want to adjust the price on this one.
                {
                    var requirements = craftable ? GetCraftingRequirements(item.Id) : null;

                    // if craftable, use crafting resources to determine cost.
                    if (requirements != null && requirements.Count > 0)
                    {
                        var newPrice = 0L;
                        foreach (var r in requirements)
                        {
                            var targetItem = GetItem(r.ResourceItemId);
                            if (targetItem != null)
                            {
                                newPrice += (targetItem.ShopSellPrice * r.Amount);
                            }
                        }
                        item.ShopSellPrice = RoundTo1000((long)(newPrice * resourceMargins));
                        continue;
                    }
                    else if (craftable && item.OreCost > 0)
                    {
                        // only using wood or ore.
                        item.ShopSellPrice = RoundTo1000((long)(item.OreCost * GetResourceCost(itemMaterial) * resourceMargins));
                    }

                    // this one can only be redeemed
                    if (lower.Contains("ancient"))
                    {
                        var redeemable = GetRedeemableItemByItemId(item.Id);
                        if (redeemable != null)
                        {
                            item.ShopSellPrice = RoundTo1000((long)(redeemable.Cost * (GetItem(redeemable.CurrencyItemId)?.ShopSellPrice ?? 0) * resourceMargins * redeemableReduction));
                        }
                    }


                    //var craftable = item.Craftable.GetValueOrDefault() || item.RequiredCraftingLevel > GameMath.MaxLevel;
                    //switch ((ItemCategory)item.Category)
                    //{
                    //    case ItemCategory.Resource:
                    //        // atlarus lights, etc.
                    //        break;
                    //    case ItemCategory.Pet:
                    //    case ItemCategory.Scroll:
                    //        break;
                    //    default:
                    //        // armor, weapons, etc.
                    //        break;
                    //}
                    //if (item.RequiredCraftingLevel > GameMath.MaxLevel)
                    //{
                    //    // we can't craft this one
                    //    // we would have to calculate vendor price
                    //    // based on tier
                    //}
                }
            }
        }

        private void EnsureCraftingRequirements(EntitySet<Item> items)
        {
            Item GetItemByCategory(ItemCategory category, string containsName)
            {
                return items.Entities.FirstOrDefault(x => (ItemCategory)x.Category == ItemCategory.Resource && x.Name.Contains(containsName, StringComparison.OrdinalIgnoreCase));
            }

            var phantomCraftingLevel = 200; // change to 210 ?
            var lionCraftingLevel = 240;
            var etherCraftingLevel = 280;
            var atlarusCraftingLevel = 420;//etherCraftingLevel * 1.75;

            var ingot = GetItemByCategory(ItemCategory.Resource, "ore ingot");
            var wood = GetItemByCategory(ItemCategory.Resource, "wood plank");
            var gold = GetItemByCategory(ItemCategory.Resource, "gold");
            foreach (var item in items.Entities)
            {
                var isResource = item.Category == (int)ItemCategory.Resource || item.Type == (int)ItemType.Ore;
                if (item.Category == (int)ItemCategory.Resource)
                    continue;

                // Make lionsbane craftable
                var nl = item.Name.ToLower();
                if (item.RequiredCraftingLevel > GameMath.MaxLevel)
                {
                    item.RequiredCraftingLevel = GameMath.MaxLevel + 1;
                    item.Craftable = false;
                }

                var isAtlarus = nl.StartsWith("atlarus");

                if (item.RequiredCraftingLevel < GameMath.MaxLevel || isAtlarus)
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

                    if (isAtlarus)
                    {
                        resType = GetItemByCategory(ItemCategory.Resource, "atlarus light");
                        ingotCount = 210;
                        woodCount = woodCount * 42;
                        //resCount = 2;
                        item.RequiredCraftingLevel = atlarusCraftingLevel;
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

        private void RemoveBadInventoryItems(EntitySet<InventoryItem> inventoryItems)
        {
            var toRemove = new List<InventoryItem>();
            foreach (var ii in inventoryItems.Entities)
            {
                if (ii.Amount <= 0 || GetCharacter(ii.CharacterId) == null)
                {
                    toRemove.Add(ii);
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

                var resx = GetResources(c.ResourcesId);
                if (resx != null && (resx.Wheat != 0 || resx.Coins != 0 || resx.Fish != 0 || resx.Wood != 0 || resx.Ore != 0))
                {
                    return;
                }

                if (resx != null)
                {
                    Remove(resx);
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

                var appearance = GetAppearance(c.AppearanceId);
                if (appearance != null)
                {
                    Remove(appearance);
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

                        var appearance = GetAppearance(c.AppearanceId);
                        if (appearance != null)
                        {
                            Remove(appearance);
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

        private void MergeVillages()
        {
            // slow process.
            foreach (var user in users.Entities)
            {
                var userId = user.Id;
                var userVillages = villages[nameof(User), userId];
                if (userVillages.Count > 1)
                {
                    var toKeep = userVillages.OrderByDescending(x => x.Level).FirstOrDefault();
                    var resourceToKeep = GetResources(toKeep.ResourcesId);

                    foreach (var village in userVillages)
                    {
                        if (village == toKeep)
                        {
                            continue;
                        }

                        toKeep.Experience += village.Experience;

                        for (var i = 1; i < village.Level; ++i)
                        {
                            toKeep.Experience += (long)GameMath.ExperienceForLevel(i);
                        }

                        var resources0 = GetResources(village.ResourcesId);
                        if (resourceToKeep != null && resources0 != null)
                        {
                            resourceToKeep.Wheat += resources0.Wheat;
                            resourceToKeep.Arrows += resources0.Arrows;
                            resourceToKeep.Ore += resources0.Ore;
                            resourceToKeep.Coins += resources0.Coins;
                            resourceToKeep.Fish += resources0.Fish;
                            resourceToKeep.Wood += resources0.Wood;
                        }

                        var houses = GetVillageHouses(village);
                        foreach (var house in houses)
                        {
                            Remove(house);
                        }

                        Remove(resources0);
                        Remove(village);
                    }

                    var expForNext = GameMath.ExperienceForLevel(toKeep.Level + 1);
                    while (toKeep.Experience > expForNext)
                    {
                        toKeep.Experience -= (long)expForNext;
                        toKeep.Level++;

                        expForNext = GameMath.ExperienceForLevel(toKeep.Level + 1);
                    }
                }
            }
        }

        private void UpgradeVillageLevels()
        {
            var updated = false;
            foreach (var village in villages.Entities)
            {
                var targetLevel = 170;
                if (village.Level != targetLevel)
                {
                    continue;
                }

                if (village.Level >= GameMath.MaxVillageLevel)
                {
                    village.Level = GameMath.MaxVillageLevel;
                    village.Experience = (long)GameMath.ExperienceForLevel(village.Level + 1) - 1;
                    continue;
                }

                var nextLevel = village.Level + 1;
                var expForLevel = GameMath.ExperienceForLevel(nextLevel);
                while (village.Experience >= expForLevel)
                {
                    var expLeft = (long)(village.Experience - expForLevel);

                    village.Experience = expLeft;
                    village.Level++;
                    updated = true;
                    expForLevel = GameMath.ExperienceForLevel(village.Level + 1);
                }

                // ensure village houses are within limits
                var houses = GetOrCreateVillageHouses(village);
                var maxHouses = GameMath.MaxVillageLevel / 10;
                var houseCount = houses.Count;
                while (houseCount > maxHouses)
                {
                    Remove(houses[houseCount - 1]);
                    houseCount--;
                }
            }
            if (updated)
            {
                ScheduleNextSave();
            }
        }

        private void UpgradeSkillLevels(EntitySet<Skills> skills)
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
        //public AddEntityResult Add(VendorTransaction entity) => Update(() => vendorTransaction.Add(entity));
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        public AddEntityResult Add(CharacterState entity) => Update(() => characterStates.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(SyntyAppearance entity) => Update(() => syntyAppearances.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Statistics entity) => Update(() => statistics.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Skills entity) => Update(() => characterSkills.Add(entity));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AddEntityResult Add(Appearance entity) => Update(() => appearances.Add(entity));

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
            if (!characterSessionStates.TryGetValue(sessionId, out var states))
            {
                states = new ConcurrentDictionary<Guid, CharacterSessionState>();
            }

            if (!states.TryGetValue(characterId, out var state))
            {
                state = new CharacterSessionState();
                state.LastTaskUpdate = DateTime.UtcNow;
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
        public Item GetItem(Guid id) => items[id];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<Item> GetItems() => items.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<ItemAttribute> GetItemAttributes() => itemAttributes.Entities;

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
        public GameSession GetSessionByUserId(Guid userId, bool updateSession = true)
        {
            var session = gameSessions[nameof(User), userId]
                    .OrderByDescending(x => x.Started)
                    .FirstOrDefault(x => x.Stopped == null);
            if (updateSession && session != null) session.Updated = DateTime.UtcNow;
            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<RedeemableItem> GetRedeemableItems() => redeemableItems.Entities;

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
                cd = new CharacterClanSkillCooldown
                {
                    Id = Guid.NewGuid(),
                    SkillId = skillId,
                    CharacterId = characterId
                };

                Add(cd);
            }
            return cd;
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
        public IReadOnlyList<ResourceItemDrop> GetResourceItemDrops()
            => resourceItemDrops.Entities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CharacterState GetCharacterState(Guid? stateId) => stateId == null ? null : characterStates[stateId.Value];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<GameSession> GetActiveSessions() => gameSessions.Entities
                    .OrderByDescending(x => x.Started)
                    .AsList(x => x.Stopped == null && DateTime.UtcNow - x.Updated <= TimeSpan.FromSeconds(SessionTimeoutSeconds));

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

                //exc.

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
            };
        }

        public void EnqueueGameEvent(GameEvent entity)
        {
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

            if (itemType == ItemType.Coins || itemType == ItemType.Ore || itemType == ItemType.Wood || itemType == ItemType.Fish)
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

    }

    public class DataSaveError
    {
    }

    #endregion
}
