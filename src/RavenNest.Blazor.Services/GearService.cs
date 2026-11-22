using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Game;
using RavenNest.Combat;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EquipmentSlot = RavenNest.BusinessLogic.Game.EquipmentSlot;
using InventoryItem = RavenNest.Models.InventoryItem;
using Item = RavenNest.Models.Item;
using ItemCategory = RavenNest.Models.ItemCategory;
using ItemType = RavenNest.Models.ItemType;
using Skill = RavenNest.Models.Skill;
using UserBankItem = RavenNest.Models.UserBankItem;

namespace RavenNest.Blazor.Services
{
    /// <summary>
    ///     What the Equipment and Inventory tabs of a character ask the server (Components/PlayerGear.razor).
    ///     <see cref="GearService"/> is the real one. The signed-out preview at /dev/gear-preview has its own, which
    ///     changes a made up character and saves nothing, so the same page can be clicked through without an account.
    /// </summary>
    /// <remarks>
    ///     Every action answers with the character as the server has it afterwards (<see cref="GearResult.Player"/>),
    ///     and the page shows that: it never changes its own copy to match what it asked for.
    /// </remarks>
    public interface IGearBackend
    {
        /// <summary>The character is the signed-in user's own (gems are changed by the owner only).</summary>
        bool IsOwner(WebsitePlayer player);

        /// <summary>The viewer is an administrator (add items, set coins).</summary>
        bool IsAdministrator { get; }

        /// <summary>The character as the server has it now; null when it is gone.</summary>
        WebsitePlayer GetPlayer(Guid characterId);

        /// <summary>The coins of the character's account.</summary>
        long GetCoins(WebsitePlayer player);

        /// <summary>The character's skill tree build as text (its passives count as gear stats); null for none.</summary>
        string GetSkillBuild(Guid characterId);

        /// <summary>The viewer's own characters, for "send to".</summary>
        Task<IReadOnlyList<WebsitePlayer>> GetMyCharactersAsync();

        /// <summary>The viewer's stash, for picking a skin from it.</summary>
        IReadOnlyList<UserBankItem> GetStash();

        double GetGemKeepChance(Guid characterId);

        GearResult Equip(Guid characterId, InventoryItem item);
        GearResult Unequip(Guid characterId, InventoryItem item);

        /// <summary>
        ///     The server's "equip best". <paramref name="skill"/> is null for what the character trains now, or the
        ///     name of a combat skill to dress for (the word of "!equip healing").
        /// </summary>
        GearResult EquipBest(Guid characterId, string skill);

        /// <summary>Whole stacks to the account's stash.</summary>
        GearResult SendToStash(Guid characterId, IReadOnlyList<InventoryItem> items);

        GearResult SendToCharacter(Guid characterId, Guid toCharacterId, InventoryItem item, long amount);

        /// <summary>One stack, or part of it, to the vendor.</summary>
        GearResult Sell(Guid characterId, InventoryItem item, long amount);

        /// <summary>Whole stacks to the vendor.</summary>
        GearResult Sell(Guid characterId, IReadOnlyList<InventoryItem> items);

        /// <summary>The skin comes out of the stash first when <paramref name="fromStash"/> is given.</summary>
        GearResult AddTransmogrification(Guid characterId, InventoryItem item, Guid skinItemId, UserBankItem fromStash);
        GearResult RemoveTransmogrification(Guid characterId, InventoryItem item);

        GemChange SocketGem(Guid characterId, Guid inventoryItemId, int kind, int tier, int socket, Guid? confirmedGem);
        GemChange UnsocketGem(Guid characterId, Guid inventoryItemId, int socket, Guid? confirmedGem);

        /// <summary>Administrators: puts an item in the inventory.</summary>
        GearResult AddItem(Guid characterId, Item item, int amount);

        /// <summary>Administrators: sets the account's coins; null when refused.</summary>
        long? SetCoins(Guid userId, long coins);
    }

    /// <summary>What an action did, as the server sees it.</summary>
    public sealed class GearResult
    {
        /// <summary>The character afterwards, changed or not; null only when it could not be read.</summary>
        public WebsitePlayer Player { get; set; }

        /// <summary>How many stacks the action was done to.</summary>
        public int Done { get; set; }

        /// <summary>How many it was asked for.</summary>
        public int Asked { get; set; }

        /// <summary>Why nothing was done (not signed in, not your character); null otherwise.</summary>
        public string Refused { get; set; }

        /// <summary>The account's coins afterwards.</summary>
        public long Coins { get; set; }

        /// <summary>Coins gained by the action (a sale).</summary>
        public long Gained { get; set; }

        public bool Ok => Refused == null && Done > 0;
    }

    /// <summary>The real thing: every call is checked here (signed in, and the owner or an administrator).</summary>
    public class GearService : RavenNestService, IGearBackend
    {
        private readonly GameData gameData;
        private readonly PlayerManager playerManager;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly PlayerService playerService;
        private readonly UserService userService;

        public GearService(
            GameData gameData,
            PlayerManager playerManager,
            PlayerInventoryProvider inventoryProvider,
            PlayerService playerService,
            UserService userService,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.playerManager = playerManager;
            this.inventoryProvider = inventoryProvider;
            this.playerService = playerService;
            this.userService = userService;
        }

        public bool IsOwner(WebsitePlayer player)
        {
            var session = GetSession();
            return player != null && session != null && session.Authenticated && session.UserId != Guid.Empty && player.UserId == session.UserId;
        }

        public bool IsAdministrator
        {
            get
            {
                var session = GetSession();
                return session != null && session.Authenticated && session.Administrator;
            }
        }

        public WebsitePlayer GetPlayer(Guid characterId) => playerManager.GetWebsitePlayer(characterId);

        public long GetCoins(WebsitePlayer player) => player == null ? 0 : playerService.GetCoins(player.UserId);

        public string GetSkillBuild(Guid characterId) => gameData.GetCharacterSkillBuild(characterId)?.Build;

        public async Task<IReadOnlyList<WebsitePlayer>> GetMyCharactersAsync()
        {
            var session = GetSession();
            if (session == null || !session.Authenticated) return Array.Empty<WebsitePlayer>();
            return await playerService.GetMyPlayersAsync();
        }

        public IReadOnlyList<UserBankItem> GetStash()
        {
            var session = GetSession();
            if (session == null || !session.Authenticated) return Array.Empty<UserBankItem>();
            return userService.GetCurrentUser()?.Stash ?? (IReadOnlyList<UserBankItem>)Array.Empty<UserBankItem>();
        }

        public double GetGemKeepChance(Guid characterId) => playerService.GetGemKeepChance(characterId);

        public GearResult Equip(Guid characterId, InventoryItem item)
        {
            return Act(characterId, 1, () => item != null && playerManager.EquipItem(characterId, item) ? 1 : 0);
        }

        public GearResult Unequip(Guid characterId, InventoryItem item)
        {
            return Act(characterId, 1, () => item != null && playerManager.UnequipItem(characterId, item) ? 1 : 0);
        }

        public GearResult EquipBest(Guid characterId, string skill)
        {
            return Act(characterId, 1, () =>
            {
                // the same call the game makes for "!equip all" and "!equip healing": the inventory feed tells a
                // running game what changed, as with every change made on this site
                inventoryProvider.Get(characterId).EquipBestItems(GearOutfit.Parse(skill));
                return 1;
            });
        }

        public GearResult SendToStash(Guid characterId, IReadOnlyList<InventoryItem> items)
        {
            return Act(characterId, items?.Count ?? 0, () =>
            {
                var done = 0;
                foreach (var item in items)
                {
                    // one stack at a time, so a game still on the old events hears of each one
                    if (item != null && !item.Equipped && playerManager.SendToStash(characterId, item, item.Amount)) done++;
                }
                return done;
            });
        }

        public GearResult SendToCharacter(Guid characterId, Guid toCharacterId, InventoryItem item, long amount)
        {
            return Act(characterId, 1, () =>
            {
                // only between the characters of one account, also for an administrator
                var from = gameData.GetCharacter(characterId);
                var to = gameData.GetCharacter(toCharacterId);
                if (from == null || to == null || from.UserId != to.UserId || item == null) return 0;
                return playerManager.SendToCharacter(characterId, toCharacterId, item, Math.Clamp(amount, 1, Math.Max(1, item.Amount))) ? 1 : 0;
            });
        }

        public GearResult Sell(Guid characterId, InventoryItem item, long amount)
        {
            return Act(characterId, 1, () =>
                item != null && !item.Equipped && playerManager.SellItemToVendor(characterId, item, Math.Clamp(amount, 1, Math.Max(1, item.Amount))) ? 1 : 0);
        }

        public GearResult Sell(Guid characterId, IReadOnlyList<InventoryItem> items)
        {
            return Act(characterId, items?.Count ?? 0, () =>
            {
                var done = 0;
                foreach (var item in items)
                {
                    if (item != null && !item.Equipped && playerManager.SellItemToVendor(characterId, item, item.Amount)) done++;
                }
                return done;
            });
        }

        public GearResult AddTransmogrification(Guid characterId, InventoryItem item, Guid skinItemId, UserBankItem fromStash)
        {
            return Act(characterId, 1, () =>
            {
                // the skin is looked for in the character's own inventory, so one from the stash moves there first;
                // when that fails nothing is changed
                if (fromStash != null && !playerManager.SendToCharacter(characterId, fromStash, 1)) return 0;
                return playerManager.AddTransmogrification(characterId, item, skinItemId) ? 1 : 0;
            });
        }

        public GearResult RemoveTransmogrification(Guid characterId, InventoryItem item)
        {
            return Act(characterId, 1, () => playerManager.RemoveTransmogrification(characterId, item) ? 1 : 0);
        }

        public GemChange SocketGem(Guid characterId, Guid inventoryItemId, int kind, int tier, int socket, Guid? confirmedGem)
        {
            return playerService.SocketGem(characterId, inventoryItemId, kind, tier, socket, confirmedGem);
        }

        public GemChange UnsocketGem(Guid characterId, Guid inventoryItemId, int socket, Guid? confirmedGem)
        {
            return playerService.UnsocketGem(characterId, inventoryItemId, socket, confirmedGem);
        }

        public GearResult AddItem(Guid characterId, Item item, int amount)
        {
            if (!IsAdministrator) return new GearResult { Refused = "Only administrators add items.", Asked = 1, Player = GetPlayer(characterId) };
            var player = item == null ? null : playerService.AddItem(characterId, item, amount);
            return new GearResult { Player = player ?? GetPlayer(characterId), Asked = 1, Done = player != null ? 1 : 0, Coins = CoinsOf(characterId) };
        }

        public long? SetCoins(Guid userId, long coins) => playerService.SetCoins(userId, coins);

        /// <summary>
        ///     Does one action for a character of the signed-in user (or for an administrator), and reads the
        ///     character and its coins back from the server afterwards, whatever happened.
        /// </summary>
        private GearResult Act(Guid characterId, int asked, Func<int> action)
        {
            var result = new GearResult { Asked = asked };
            var refused = MayChange(characterId);
            if (refused != null)
            {
                result.Refused = refused;
                result.Player = GetPlayer(characterId);
                result.Coins = CoinsOf(characterId);
                return result;
            }

            var before = CoinsOf(characterId);
            if (asked > 0) result.Done = action();
            result.Player = GetPlayer(characterId);
            result.Coins = CoinsOf(characterId);
            result.Gained = Math.Max(0, result.Coins - before);
            return result;
        }

        // Null when the signed-in user may change this character's gear, otherwise why not. The character id
        // arrives from the browser, so the page hiding a button is not the rule: this is.
        private string MayChange(Guid characterId)
        {
            var session = GetSession();
            if (session == null || !session.Authenticated || session.UserId == Guid.Empty)
            {
                return "You need to be signed in to do that.";
            }

            var character = gameData.GetCharacter(characterId);
            if (character == null)
            {
                return "That character is gone.";
            }

            if (character.UserId != session.UserId && !session.Administrator)
            {
                return "That character does not belong to you.";
            }

            return null;
        }

        private long CoinsOf(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            return character == null ? 0 : playerService.GetCoins(character.UserId);
        }
    }

    /// <summary>One place gear is worn, as the Equipment tab draws it.</summary>
    /// <param name="Key">For markup: "head", "melee".</param>
    /// <param name="Label">"Head", "Melee weapon".</param>
    /// <param name="Picture">The game's own glyph for an empty slot (wwwroot/imgs/icons), or null.</param>
    /// <param name="Icon">A Font Awesome class for where the game has no glyph.</param>
    public sealed record GearSlot(EquipmentSlot Slot, string Key, string Label, string Picture, string Icon)
    {
        /// <summary>The label's first word, for where there is no room for more: "Melee".</summary>
        public string Short => Label.Split(' ')[0];
    }

    /// <summary>The kinds the bag is filtered by.</summary>
    public enum GearKind
    {
        All,
        Weapons,
        Armor,
        Jewelry,
        Pets,
        Boats,
        Gems,
        Food,
        Potions,
        Scrolls,
        Resources,
        Other
    }

    /// <summary>
    ///     The rules the two tabs share and that need no server: which slot a piece goes in, what kind of thing an
    ///     item is, what a piece is worth when the best is picked, and whether it beats what is worn.
    /// </summary>
    public static class GearRules
    {
        private const string Icons = "/imgs/icons/inventory/";

        // The slots the game really has (EquipmentSlot without Belt and Cape, which nothing uses), in the order they
        // stand around the character: armor down the left, what is held and worn as jewelry down the right.
        public static readonly GearSlot[] Left =
        {
            new GearSlot(EquipmentSlot.Head, "head", "Head", Icons + "helmet.png", null),
            new GearSlot(EquipmentSlot.Amulet, "amulet", "Amulet", null, "fa-solid fa-gem"),
            new GearSlot(EquipmentSlot.Chest, "chest", "Chest", Icons + "custume_top.png", null),
            new GearSlot(EquipmentSlot.Gloves, "gloves", "Gloves", Icons + "glove.png", null),
            new GearSlot(EquipmentSlot.Leggings, "leggings", "Leggings", null, "fa-solid fa-socks"),
            new GearSlot(EquipmentSlot.Boots, "boots", "Boots", Icons + "custume_boots.png", null),
        };

        public static readonly GearSlot[] Right =
        {
            new GearSlot(EquipmentSlot.MeleeWeapon, "melee", "Melee weapon", Icons + "sword.png", null),
            new GearSlot(EquipmentSlot.RangedWeapon, "ranged", "Ranged weapon", Icons + "archer.png", null),
            new GearSlot(EquipmentSlot.MagicWeapon, "magic", "Magic weapon", Icons + "magic weapon.png", null),
            new GearSlot(EquipmentSlot.Shield, "shield", "Shield", "/imgs/icons/skills/Defense.png", null),
            new GearSlot(EquipmentSlot.Ring, "ring", "Ring", Icons + "ring.png", null),
            new GearSlot(EquipmentSlot.Pet, "pet", "Pet", Icons + "Pet.png", null),
        };

        public static readonly GearSlot Boat = new GearSlot(EquipmentSlot.Boat, "boat", "Boat", "/imgs/icons/skills/sailing.png", null);

        public static readonly GearSlot[] All = Left.Concat(Right).Append(Boat).ToArray();

        public static GearSlot Get(EquipmentSlot slot) => All.FirstOrDefault(x => x.Slot == slot);

        /// <summary>Where a piece is worn; None for what cannot be worn.</summary>
        public static EquipmentSlot SlotOf(Item item)
        {
            return item == null ? EquipmentSlot.None : ReadOnlyInventoryItem.GetEquipmentSlot((RavenNest.DataModels.ItemType)(int)item.Type);
        }

        public static GearKind KindOf(Item item)
        {
            if (item == null) return GearKind.Other;
            if (Gems.IsGem(item.Id)) return GearKind.Gems;
            switch (item.Category)
            {
                case ItemCategory.Weapon: return GearKind.Weapons;
                case ItemCategory.Armor: return GearKind.Armor;
                case ItemCategory.Ring:
                case ItemCategory.Amulet: return GearKind.Jewelry;
                case ItemCategory.Pet: return GearKind.Pets;
                case ItemCategory.Vehicle: return GearKind.Boats;
                case ItemCategory.Food: return GearKind.Food;
                case ItemCategory.Potion: return GearKind.Potions;
                case ItemCategory.Scroll: return GearKind.Scrolls;
                case ItemCategory.Resource: return GearKind.Resources;
            }

            // looks (hats, masks, scarves) go with what is worn in the same place
            switch (SlotOf(item))
            {
                case EquipmentSlot.None: return GearKind.Other;
                case EquipmentSlot.Amulet:
                case EquipmentSlot.Ring: return GearKind.Jewelry;
                case EquipmentSlot.Pet: return GearKind.Pets;
                case EquipmentSlot.Boat: return GearKind.Boats;
                case EquipmentSlot.MeleeWeapon:
                case EquipmentSlot.RangedWeapon:
                case EquipmentSlot.MagicWeapon: return GearKind.Weapons;
                default: return GearKind.Armor;
            }
        }

        public static string KindLabel(GearKind kind) => kind == GearKind.All ? "All" : kind.ToString();

        public static string KindIcon(GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Weapons: return "fa-sharp fa-solid fa-sword";
                case GearKind.Armor: return "fa-sharp fa-solid fa-helmet-battle";
                case GearKind.Jewelry: return "fa-sharp fa-solid fa-ring";
                case GearKind.Pets: return "fa-sharp fa-solid fa-paw";
                case GearKind.Boats: return "fa-sharp fa-solid fa-sailboat";
                case GearKind.Gems: return "fa-sharp fa-solid fa-gem";
                case GearKind.Food: return "fa-sharp fa-solid fa-drumstick-bite";
                case GearKind.Potions: return "fa-sharp fa-solid fa-flask";
                case GearKind.Scrolls: return "fa-sharp fa-solid fa-scroll";
                case GearKind.Resources: return "fa-sharp fa-solid fa-cubes";
                case GearKind.Other: return "fa-sharp fa-solid fa-ellipsis";
                default: return "fa-sharp fa-solid fa-rectangles-mixed";
            }
        }

        /// <summary>
        ///     The narrower group inside a kind ("Swords" in Weapons, "Mining" in Resources), from the same categories
        ///     the stash page filters by; null where a kind has none.
        /// </summary>
        public static string GroupOf(Item item)
        {
            if (item == null) return null;
            switch (KindOf(item))
            {
                case GearKind.Weapons:
                case GearKind.Resources:
                    var filter = ItemFilterExtensions.GetItemFilter(item);
                    var option = ItemFilterOptions.All.FirstOrDefault(x => x.Filter == filter);
                    return option == null || filter == ItemFilter.All ? "Other" : option.Label;

                case GearKind.Armor:
                    var slot = SlotOf(item);
                    return slot == EquipmentSlot.Shield ? "Shields" : Get(slot)?.Label;

                case GearKind.Jewelry:
                    return SlotOf(item) == EquipmentSlot.Ring ? "Rings" : "Amulets";
            }

            return null;
        }

        /// <summary>
        ///     What a piece is worth by the rule every "equip best" starts from: its seven classic columns, with what
        ///     a Power, Aim or Armor enchantment adds to them (the server's PlayerInventory.GetItemValue).
        /// </summary>
        public static int Classic(Item item, IReadOnlyList<ItemEnchantment> enchantments)
        {
            if (item == null) return 0;
            var value = GearScore.Classic(item);
            if (enchantments == null) return value;
            foreach (var e in enchantments)
            {
                var key = (e.Name ?? string.Empty).ToUpperInvariant();
                var percent = e.ValueType == AttributeValueType.Percent;
                var share = percent && e.Value >= 1 ? e.Value / 100d : e.Value;
                if (key == "POWER")
                {
                    value += percent
                        ? (int)(item.WeaponPower * share) + (int)(item.MagicPower * share) + (int)(item.RangedPower * share)
                        : (int)e.Value;
                }
                else if (key == "AIM")
                {
                    value += percent
                        ? (int)(item.WeaponAim * share) + (int)(item.MagicAim * share) + (int)(item.RangedAim * share)
                        : (int)e.Value;
                }
                else if (key == "ARMOUR" || key == "ARMOR")
                {
                    value += percent ? (int)(item.ArmorPower * share) : (int)e.Value;
                }
            }

            return value;
        }

        private static readonly (GearStat Stat, Func<Item, int> Column)[] ClassicColumns =
        {
            (GearStat.MeleeAccuracy, i => i.WeaponAim),
            (GearStat.MeleePower, i => i.WeaponPower),
            (GearStat.RangedAccuracy, i => i.RangedAim),
            (GearStat.RangedPower, i => i.RangedPower),
            (GearStat.MagicAccuracy, i => i.MagicAim),
            (GearStat.MagicPower, i => i.MagicPower),
            (GearStat.Armor, i => i.ArmorPower),
        };

        /// <summary>
        ///     What a piece gives, stat by stat: the classic columns with what a Power, Aim or Armor enchantment adds
        ///     to them, then the stats beyond the columns (element damage, resistances, crits, the shares class gear
        ///     carries) with their decimals. The shares of class gear are small numbers: rounded to whole percents,
        ///     the 1.5% of a robe and the 0.4% of a hood would read as "1%" and "0%".
        /// </summary>
        public static List<GearStatLine> StatsOf(Item item, IReadOnlyList<ItemEnchantment> enchantments)
        {
            var lines = new List<GearStatLine>();
            if (item == null) return lines;

            bool power = false, aim = false, armor = false, powerShare = false, aimShare = false, armorShare = false;
            double powerValue = 0, aimValue = 0, armorValue = 0;
            foreach (var e in enchantments ?? Array.Empty<ItemEnchantment>())
            {
                var key = (e.Name ?? string.Empty).ToUpperInvariant();
                var percent = e.ValueType == AttributeValueType.Percent;
                var share = percent && e.Value >= 1 ? e.Value / 100d : e.Value;
                if (key == "POWER") { power = true; powerShare = percent; powerValue = share; }
                else if (key == "AIM") { aim = true; aimShare = percent; aimValue = share; }
                else if (key == "ARMOR" || key == "ARMOUR") { armor = true; armorShare = percent; armorValue = share; }
            }

            foreach (var (stat, column) in ClassicColumns)
            {
                var value = column(item);
                if (value <= 0) continue;
                var isArmor = stat == GearStat.Armor;
                var isPower = stat == GearStat.MeleePower || stat == GearStat.RangedPower || stat == GearStat.MagicPower;
                var has = isArmor ? armor : isPower ? power : aim;
                var isShare = isArmor ? armorShare : isPower ? powerShare : aimShare;
                var by = isArmor ? armorValue : isPower ? powerValue : aimValue;
                var bonus = !has ? 0 : isShare ? (int)(value * by) : (int)by;
                lines.Add(new GearStatLine(stat, GearStats.Label(stat), value, bonus, false));
            }

            if (string.IsNullOrEmpty(item.Stats)) return lines;
            var all = GearStats.Parse(item.Stats);
            for (var s = 0; s < GearStats.Count; ++s)
            {
                var stat = (GearStat)s;
                if (Array.Exists(ClassicColumns, x => x.Stat == stat)) continue;
                var value = all[stat];
                if (value == 0) continue;
                lines.Add(new GearStatLine(stat, ShareLabel(stat), value, 0, GearStats.IsShare(stat)));
            }

            return lines;
        }

        // "1.5% Healing Power" says it: "Healing Power Bonus" is the stat's name beside the flat Healing Power, and
        // the percent sign already tells the two apart (the game's item tooltips and chat name them the same way).
        private static string ShareLabel(GearStat stat)
        {
            switch (stat)
            {
                case GearStat.MeleePowerBonus: return "Melee Power";
                case GearStat.RangedPowerBonus: return "Ranged Power";
                case GearStat.MagicPowerBonus: return "Magic Power";
                case GearStat.HealingPowerBonus: return "Healing Power";
                case GearStat.AccuracyBonus: return "Accuracy";
                case GearStat.ArmorBonus: return "Armor";
                case GearStat.MaxHealthBonus: return "Max Health";
                default: return GearStats.Label(stat);
            }
        }

        /// <summary>
        ///     Would "equip best" put this piece on in place of the worn one: the game's own comparison
        ///     (RavenNest.Models.GearScore), for a character dressing for <paramref name="dressFor"/>. False where the
        ///     rule cannot tell (two pieces it counts as worth nothing, like most jewelry and every pet).
        /// </summary>
        public static bool Beats(Item candidate, int candidateClassic, Item worn, int wornClassic, Skill dressFor)
        {
            if (candidate == null || candidateClassic <= 0) return false;
            var wornMaterial = worn?.Material ?? ItemMaterial.None;
            var fits = GearScore.FitsFor(dressFor, wornMaterial);
            return GearScore.Beats(candidate.Material, candidateClassic, wornMaterial, worn == null ? 0 : wornClassic, fits);
        }

        /// <summary>The combat skills a character can be dressed for, in the game's order.</summary>
        public static readonly Skill[] DressFor = { Skill.Attack, Skill.Defense, Skill.Strength, Skill.Ranged, Skill.Magic, Skill.Healing };

        public static string SkillPicture(Skill skill)
        {
            switch (skill)
            {
                case Skill.Attack: return "/imgs/icons/skills/Attack.png";
                case Skill.Defense: return "/imgs/icons/skills/Defense.png";
                case Skill.Strength: return "/imgs/icons/skills/Strength.png";
                case Skill.Ranged: return "/imgs/icons/skills/ranged.png";
                case Skill.Magic: return "/imgs/icons/skills/Magic.png";
                case Skill.Healing: return "/imgs/icons/skills/healing.png";
                default: return "/imgs/icons/skills/CombatLevel.png";
            }
        }

        /// <summary>The way of fighting a skill's numbers are shown for.</summary>
        public static AttackStyle StyleOf(Skill skill)
        {
            switch (skill)
            {
                case Skill.Ranged: return AttackStyle.Ranged;
                case Skill.Magic: return AttackStyle.Magic;
                case Skill.Healing: return AttackStyle.Healing;
                default: return AttackStyle.Melee;
            }
        }

        /// <summary>"Tank gear, tier 4" for class gear; null for everything else.</summary>
        public static string LineOf(Item item)
        {
            if (item == null || !ItemLines.IsClassGear(item.Material)) return null;
            var name = GearOutfit.NameOf(ItemLines.Of(item.Material));
            return char.ToUpperInvariant(name[0]) + name.Substring(1) + " gear, tier " + ItemLines.TierOf(item.Material);
        }
    }

    /// <summary>One stat of a piece: its own value and what an enchantment adds; shares are fractions (0.015 is 1.5%).</summary>
    public sealed record GearStatLine(GearStat Stat, string Label, double Value, double Bonus, bool Share)
    {
        public double Total => Value + Bonus;

        /// <summary>A value of this stat as it is read: "1,204" or "1.5%".</summary>
        public string Text(double value) => Share
            ? Math.Round(value * 100, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%"
            : Math.Round(value).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>One row of the stats list: a label, its value, and whether it reads as a gain or a loss.</summary>
    public sealed record GearStatRow(string Label, string Value, bool Good = false, bool Bad = false);

    /// <summary>
    ///     A character's combat numbers as worn: the same sheet a fight in the game reads (the game's CombatRules.Sheet
    ///     and PlayerController.UpdateEquipmentEffect), built from the skills, the worn gear with its enchantments and
    ///     gems, and the skill trees' passives. Food, potions and a battle pet's bonuses come and go in play and are
    ///     not counted.
    /// </summary>
    public sealed class GearSheet
    {
        public CombatSheet Sheet { get; private set; }
        public AttackStyle Style { get; private set; }

        public int MaxHealth { get; private set; }
        public int VitalityHealth { get; private set; }
        public int ArmorHealth { get; private set; }
        public int FlatGearHealth { get; private set; }
        public int BonusHealth { get; private set; }

        /// <summary>The shield was left out: the game does not count it while training Ranged, Magic or Healing.</summary>
        public bool ShieldLeftOut { get; private set; }

        public int Armor => Sheet.EffectiveArmor;
        public double ArmorCut => CombatMath.ArmorReduction(Armor);
        public StyleStats StyleStats => Sheet.Style(Style);
        public int Power => StyleStats.EffectivePower;
        public int Accuracy => StyleStats.EffectiveAccuracy;
        public int MaxHit => (int)CombatMath.MaxHit(StyleStats.DamageLevel, StyleStats.EffectivePower);

        public string StyleName => Style == AttackStyle.Ranged ? "Ranged" : Style == AttackStyle.Magic ? "Magic" : Style == AttackStyle.Healing ? "Healing" : "Melee";

        /// <param name="enchantments">Reads an item's enchantments (ItemService.GetItemEnchantments).</param>
        /// <param name="getItem">Reads an item by id.</param>
        public static GearSheet Build(
            WebsitePlayer player,
            AttackStyle style,
            string skillBuild,
            Func<Guid, Item> getItem,
            Func<InventoryItem, IReadOnlyList<ItemEnchantment>> enchantments)
        {
            var skills = player.Skills;
            var levels = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["ATTACK"] = skills.AttackLevel,
                ["DEFENSE"] = skills.DefenseLevel,
                ["STRENGTH"] = skills.StrengthLevel,
                ["HEALTH"] = skills.HealthLevel,
                ["RANGED"] = skills.RangedLevel,
                ["MAGIC"] = skills.MagicLevel,
                ["HEALING"] = skills.HealingLevel,
            };
            var bonus = levels.Keys.ToDictionary(x => x, x => 0d, StringComparer.OrdinalIgnoreCase);

            var extras = new GearStats();
            GearStats[] weaponExtras = null;
            int armor = 0, armorHealth = 0;
            int meleeAim = 0, meleePower = 0, rangedAim = 0, rangedPower = 0, magicAim = 0, magicPower = 0;
            var shieldLeftOut = false;
            var noShield = style != AttackStyle.Melee;

            foreach (var worn in (player.InventoryItems ?? Array.Empty<InventoryItem>()).Where(x => x.Equipped))
            {
                var item = getItem(worn.ItemId);
                if (item == null) continue;
                if (item.Type == ItemType.Shield && noShield)
                {
                    shieldLeftOut = true;
                    continue;
                }

                if (CombatMath.IsArmorPiece((int)item.Type)) armorHealth += CombatMath.ArmorHealth(item.ArmorPower);

                var gear = string.IsNullOrEmpty(item.Stats)
                    ? GearStats.FromLegacy(item.WeaponAim, item.WeaponPower, item.RangedAim, item.RangedPower, item.MagicAim, item.MagicPower, item.ArmorPower)
                    : GearStats.Parse(item.Stats);
                var gearStyle = item.Category == ItemCategory.Weapon ? gear.WeaponStyle() : null;

                int powerBonus = 0, aimBonus = 0, armorBonus = 0;
                bool powerShare = false, aimShare = false;
                double powerValue = 0, aimValue = 0;
                foreach (var e in enchantments(worn) ?? Array.Empty<ItemEnchantment>())
                {
                    var key = (e.Name ?? string.Empty).ToUpperInvariant();
                    var percent = e.ValueType == AttributeValueType.Percent;
                    var share = percent && e.Value >= 1 ? e.Value / 100d : e.Value;

                    if (key == "POWER") { powerShare = percent; powerValue = share; powerBonus = 1; continue; }
                    if (key == "AIM") { aimShare = percent; aimValue = share; aimBonus = 1; continue; }
                    if (key == "ARMOR" || key == "ARMOUR") { armorBonus = percent ? (int)(item.ArmorPower * share) : (int)e.Value; continue; }

                    // an enchantment that raises a skill: a share of the level, or levels
                    if (levels.TryGetValue(key, out var level))
                    {
                        bonus[key] += percent ? level * share : e.Value;
                        continue;
                    }

                    // the magical ones are named like gear stats (crit chance, life steal, thorns, ...)
                    if (!GearStats.TryParseKey(e.Name, out var stat)) continue;
                    if (stat <= GearStat.Armor && stat != GearStat.HealingPower) continue;
                    if (!GearStats.IsShare(stat) && percent) continue;
                    extras[stat] += share;
                }

                // a Power or Aim enchantment adds to the columns the piece has
                int Raised(int column, bool has, bool isShare, double value) => !has || column <= 0 ? column : column + (isShare ? (int)(column * value) : (int)value);
                meleePower += Raised(item.WeaponPower, powerBonus > 0, powerShare, powerValue);
                rangedPower += Raised(item.RangedPower, powerBonus > 0, powerShare, powerValue);
                magicPower += Raised(item.MagicPower, powerBonus > 0, powerShare, powerValue);
                meleeAim += Raised(item.WeaponAim, aimBonus > 0, aimShare, aimValue);
                rangedAim += Raised(item.RangedAim, aimBonus > 0, aimShare, aimValue);
                magicAim += Raised(item.MagicAim, aimBonus > 0, aimShare, aimValue);
                armor += item.ArmorPower + armorBonus;

                // gems go with the piece: a weapon's with its own style
                if (GemManager.TryGetSlot(item, out var gemSlot)) gear.Add(Gems.SocketBonus(worn.Sockets, gemSlot));
                if (gearStyle.HasValue)
                {
                    weaponExtras ??= new GearStats[4];
                    (weaponExtras[(int)gearStyle.Value] ??= new GearStats()).Add(gear);
                }
                else
                {
                    extras.Add(gear);
                }
            }

            // the skill trees' passives count like gear (those that need a battle pet out are left out)
            if (!string.IsNullOrEmpty(skillBuild)) extras.Add(SkillBuild.Parse(skillBuild).Passives(false));

            int Level(string key) => (int)Math.Floor(levels[key] + bonus[key]);

            var sheet = new CombatSheet
            {
                CombatLevel = player.CombatLevel,
                DefenseLevel = Level("DEFENSE"),
                Armor = armor,
                IsPlayer = true,
            };
            sheet.Melee.DamageLevel = Level("STRENGTH");
            sheet.Melee.AimLevel = Level("ATTACK");
            sheet.Melee.Power = meleePower;
            sheet.Melee.Accuracy = meleeAim;
            sheet.Ranged.DamageLevel = Level("RANGED");
            sheet.Ranged.AimLevel = Level("RANGED");
            sheet.Ranged.Power = rangedPower;
            sheet.Ranged.Accuracy = rangedAim;
            sheet.Magic.DamageLevel = Level("MAGIC");
            sheet.Magic.AimLevel = Level("MAGIC");
            sheet.Magic.Power = magicPower;
            sheet.Magic.Accuracy = magicAim;
            // healing weapons carry magic power
            sheet.Healing.DamageLevel = Level("HEALING");
            sheet.Healing.AimLevel = Level("HEALING");
            sheet.Healing.Power = magicPower;

            extras.ApplyExtras(sheet, null);
            if (weaponExtras != null)
            {
                for (var s = 0; s < weaponExtras.Length; ++s) weaponExtras[s]?.ApplyExtras(sheet, (AttackStyle)s);
            }

            // health is worked out the game's way: Vitality's share, the armor pieces, flat health on gear, then the
            // "more health" shares of all of that
            double Gear(GearStat stat)
            {
                var total = extras[stat];
                if (weaponExtras != null) foreach (var w in weaponExtras) if (w != null) total += w[stat];
                return total;
            }

            var vitality = CombatMath.PlayerMaxHealth(Level("HEALTH"));
            var flat = Gear(GearStat.MaxHealth) + armorHealth;
            var more = Gear(GearStat.MaxHealthBonus);
            var gearHealth = flat + (more != 0 ? (vitality + flat) * more : 0);
            sheet.MaxHealth = Math.Max(1, vitality + (int)gearHealth);

            return new GearSheet
            {
                Sheet = sheet,
                Style = style,
                MaxHealth = sheet.MaxHealth,
                VitalityHealth = vitality,
                ArmorHealth = armorHealth,
                FlatGearHealth = (int)(flat - armorHealth),
                BonusHealth = (int)(gearHealth - flat),
                ShieldLeftOut = shieldLeftOut,
            };
        }

        /// <summary>Every stat in the groups and words of the game's own stats view (the details window).</summary>
        public List<(string Heading, List<GearStatRow> Rows)> Groups()
        {
            var sheet = Sheet;
            var groups = new List<(string, List<GearStatRow>)>();

            var health = new List<GearStatRow>
            {
                new GearStatRow("Max health", Number(MaxHealth)),
                new GearStatRow("From Vitality", Number(VitalityHealth)),
            };
            if (ArmorHealth > 0) health.Add(new GearStatRow("From armor", "+" + Number(ArmorHealth), true));
            if (FlatGearHealth != 0) health.Add(new GearStatRow("From gear", Signed(FlatGearHealth), FlatGearHealth > 0, FlatGearHealth < 0));
            if (BonusHealth != 0) health.Add(new GearStatRow("Health bonus", Signed(BonusHealth), BonusHealth > 0, BonusHealth < 0));
            if (sheet.LifeSteal > 0) health.Add(new GearStatRow(GearStats.Label(GearStat.LifeSteal), Percent(sheet.LifeSteal), true));
            if (sheet.HealingReceived != 0) health.Add(new GearStatRow(GearStats.Label(GearStat.HealingReceived), SignedPercent(sheet.HealingReceived), sheet.HealingReceived > 0, sheet.HealingReceived < 0));
            if (sheet.CombatRegen > 0) health.Add(new GearStatRow(GearStats.Label(GearStat.CombatRegen), Percent(sheet.CombatRegen), true));
            groups.Add(("Health", health));

            var defense = new List<GearStatRow>
            {
                new GearStatRow("Armor", Number(Armor)),
                new GearStatRow("Physical damage cut", Percent(ArmorCut)),
            };
            if (sheet.GearEvasion > 0) defense.Add(new GearStatRow(GearStats.Label(GearStat.Evasion), Number(sheet.GearEvasion)));
            if (sheet.BlockChance > 0) defense.Add(new GearStatRow(GearStats.Label(GearStat.BlockChance), Percent(sheet.BlockChance)));
            if (sheet.DamageTakenMultiplier < 1) defense.Add(new GearStatRow("Damage reduction", Percent(1 - sheet.DamageTakenMultiplier), true));
            if (sheet.Thorns > 0) defense.Add(new GearStatRow(GearStats.Label(GearStat.Thorns), Percent(sheet.Thorns)));
            if (sheet.LowHealthReduction > 0) defense.Add(new GearStatRow(GearStats.Label(GearStat.LowHealthReduction), Percent(sheet.LowHealthReduction)));
            groups.Add(("Defense", defense));

            var s = StyleStats;
            var healing = Style == AttackStyle.Healing;
            var offense = new List<GearStatRow> { new GearStatRow("Power", Number(s.EffectivePower)) };
            if (!healing) offense.Add(new GearStatRow("Accuracy", Number(s.EffectiveAccuracy)));
            offense.Add(new GearStatRow(healing ? "Max heal" : "Max hit", Number(MaxHit)));
            offense.Add(new GearStatRow("Crit chance", Percent(sheet.CritChance)));
            offense.Add(new GearStatRow("Crit damage", Percent(sheet.CritDamage)));
            if (sheet.AttackSpeed != 1) offense.Add(new GearStatRow(GearStats.Label(GearStat.AttackSpeed), SignedPercent(sheet.AttackSpeed - 1), sheet.AttackSpeed > 1, sheet.AttackSpeed < 1));
            if (sheet.ArmorPenetration > 0) offense.Add(new GearStatRow(GearStats.Label(GearStat.ArmorPenetration), Percent(sheet.ArmorPenetration)));
            if (sheet.BossDamage > 0) offense.Add(new GearStatRow(GearStats.Label(GearStat.BossDamage), SignedPercent(sheet.BossDamage), true));
            if (sheet.ExecuteDamage > 0) offense.Add(new GearStatRow(GearStats.Label(GearStat.ExecuteDamage), SignedPercent(sheet.ExecuteDamage), true));
            if (sheet.ThreatMultiplier != 1) offense.Add(new GearStatRow(GearStats.Label(GearStat.Threat), SignedPercent(sheet.ThreatMultiplier - 1)));
            groups.Add(("Offense: " + StyleName, offense));

            var elements = new List<GearStatRow>();
            var all = new[] { Element.Fire, Element.Ice, Element.Lightning, Element.Poison };
            foreach (var e in all)
            {
                var damage = s.Conversion[e] + sheet.ElementBonus[e];
                if (damage > 0) elements.Add(new GearStatRow(e + " damage", Percent(damage), true));
            }
            foreach (var e in all)
            {
                var resist = sheet.Resistance[e];
                if (resist != 0) elements.Add(new GearStatRow(e + " resistance", SignedPercent(Math.Min(resist, CombatMath.PlayerResistanceCap)), resist > 0, resist < 0));
            }
            if (elements.Count > 0) groups.Add(("Elements", elements));

            return groups;
        }

        public static string Number(long value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        public static string Signed(long value) => (value > 0 ? "+" : "") + Number(value);
        public static string Percent(double share) => (share * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
        public static string SignedPercent(double share) => (share > 0 ? "+" : "") + Percent(share);
    }
}
