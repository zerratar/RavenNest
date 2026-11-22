using RavenNest.Blazor.Services;
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
using Skill = RavenNest.Models.Skill;

namespace RavenNest.Blazor.Pages.Dev
{
    /// <summary>
    ///     The gear tabs' backend for /dev/gear-preview: a made up character carrying real items of the game, kept in
    ///     memory for as long as the page is open. Every action changes that character only; nothing is saved or
    ///     sent anywhere. It answers as the server does (the character afterwards), so the page under it is the
    ///     real one.
    /// </summary>
    public sealed class GearPreviewBackend : IGearBackend
    {
        private readonly ItemService items;
        private readonly Dictionary<string, Item> byName;
        private readonly List<InventoryItem> bag = new List<InventoryItem>();
        private readonly List<UserBankItem> stash = new List<UserBankItem>();
        private readonly Random random = new Random(7);
        private readonly bool owner;
        private readonly bool administrator;
        private long coins = 1_284_650;

        public Guid CharacterId { get; } = Guid.NewGuid();
        private readonly Guid userId = Guid.NewGuid();
        private readonly Guid otherCharacterId = Guid.NewGuid();

        private readonly IReadOnlyDictionary<string, string> options;

        private string Option(string name) => options != null && options.TryGetValue(name, out var value) ? value : null;

        /// <param name="state">"owner" (the default), "visitor", "admin" or "empty".</param>
        /// <param name="options">
        ///     For looking at the dressed figure: wear (ItemMaterial names, comma separated: the armor of those
        ///     materials is worn instead of the usual set), headpiece (a word of the head piece's name to prefer),
        ///     bare (nothing else is worn), gender, hair, head, beard, cape (0 to 4), capehidden, helmet (0 hides it),
        ///     outfits (0: the character has no outfits yet).
        /// </param>
        public GearPreviewBackend(ItemService items, string state, IReadOnlyDictionary<string, string> options = null)
        {
            this.items = items;
            this.options = options;
            byName = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.GetItems())
            {
                if (!string.IsNullOrEmpty(item.Name)) byName[item.Name] = item;
            }

            owner = state != "visitor";
            administrator = state == "admin";
            if (state != "empty")
            {
                Fill();
                if (Option("outfits") != "0") FillOutfits();
            }
        }

        /// <summary>The stack of an item by name, for opening the page on it (&amp;pick=).</summary>
        public Guid? StackOf(string itemName, bool worn)
        {
            return bag.FirstOrDefault(x => x.Equipped == worn && byName.TryGetValue(itemName ?? string.Empty, out var item) && x.ItemId == item.Id)?.Id;
        }

        // The armor of the named materials, worn: one piece a slot, the first material that has one.
        private void WearMaterials(string names)
        {
            var taken = new HashSet<EquipmentSlot>();
            var prefer = Option("headpiece");
            foreach (var name in names.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Enum.TryParse<ItemMaterial>(name.Trim(), true, out var material)) continue;
                var pieces = byName.Values
                    .Where(x => x.Material == material && x.Category == ItemCategory.Armor)
                    .OrderByDescending(x => prefer != null && x.Name.Contains(prefer, StringComparison.OrdinalIgnoreCase))
                    .ThenBy(x => x.Name, StringComparer.Ordinal);
                foreach (var piece in pieces)
                {
                    var slot = GearRules.SlotOf(piece);
                    if (slot == EquipmentSlot.None || !taken.Add(slot)) continue;
                    bag.Add(new InventoryItem { Id = Guid.NewGuid(), ItemId = piece.Id, Amount = 1, Equipped = true });
                }
            }
        }

        private void Fill()
        {
            var wear = Option("wear");
            if (wear != null)
            {
                WearMaterials(wear);
                if (Option("bare") == null)
                {
                    Add("Dragon Katana", worn: true);
                    Add("Dragon Bow", worn: true);
                    Add("Phantom Staff", worn: true);
                    Add("Dragon Shield", worn: true);
                    Add("Archmages Pendant", worn: true);
                }

                FillBag();
                return;
            }

            // what a healer around level 160 wears: a class gear set with one plate piece left to replace, a weapon
            // for each way of fighting, and an empty shield slot
            Add("Priest Cowl", worn: true, sockets: Sockets((GemKind.Diamond, GemTier.Polished)));
            Add("Archmages Pendant", worn: true);
            Add("Priest Vestment", worn: true, enchantment: "ARMOR:12%;HEALING:4%");
            Add("Rune Gloves", worn: true);
            Add("Priest Tabard", worn: true);
            Add("Dragon Boots", worn: true, sockets: Sockets((GemKind.Ruby, GemTier.Flawed)));
            Add("Dragon Katana", worn: true, sockets: Sockets((GemKind.Ruby, GemTier.Polished)));
            Add("Dragon Bow", worn: true);
            Add("Phantom Staff", worn: true, enchantment: "POWER:9%;CRIT_CHANCE:2%", sockets: Sockets((GemKind.Sapphire, GemTier.Flawed), null));
            Add("Owls Eye Ring", worn: true);
            Add("Raven Pet", worn: true);
            Add("Sloop", worn: true);

            FillBag();
        }

        private void FillBag()
        {
            // pieces to wear: an upgrade, pieces of other lines, ones the character cannot wear yet, looks
            Add("Priest Gloves");
            Add("Priest Boots");
            Add("Priest Cap");
            Add("Hierophant Vestment");
            Add("Abraxas Helmet", enchantment: "ARMOR:8%");
            Add("Dragon Helmet", 2);
            Add("Phantom Chest", sockets: Sockets(null, (GemKind.Emerald, GemTier.Chipped)));
            Add("Dragon Chest");
            Add("Dragon Leggings");
            Add("Rune Boots", 3);
            Add("Dragon Shield");
            Add("Rune Shield");
            Add("Rune Sword");
            Add("Dragon 2H Axe");
            Add("Rune Bow");
            Add("Dragon Staff");
            Add("Elder Atlarus Staff");
            Add("Warlock Robe");
            Add("Conjurer Hood");
            Add("Crusader Helm");
            Add("Tracker Hood");
            Add("Witchfinder Buckle Hat");
            Add("Ring Of The Celestial");
            Add("Ruby Ring");
            Add("Dragon Ring", 2);
            Add("Phantom Amulet");
            Add("Emerald Amulet");
            Add("Santa Hat");
            // the Beastmaster line needs Taming: one piece within reach, one far above it
            Add("Fledgling Feathered Hat");
            Add("Wyrmkin Hide Vest");
            Add("Fox Pet");
            Add("Black Cat");
            Add("Fishing Boat");

            // everything else a bag fills up with
            Add("Polished Ruby", 3);
            Add("Flawless Sapphire");
            Add("Flawed Emerald", 2);
            Add("Chipped Diamond", 7);
            Add("Chipped Topaz", 4);
            Add("Salmon", 19);
            Add("Bass", 12);
            Add("Cooked Chicken Leg", 36);
            Add("Bread", 29);
            Add("Mango Tart", 6);
            Add("Health Potion", 18);
            Add("Great Healing Potion", 4);
            Add("Tome of Home", 2);
            Add("Raid Scroll", 5);
            Add("Dungeon Scroll", 3);
            Add("Exp Multiplier Scroll", 2);
            Add("Tome of Forgetting");
            Add("Halloween Token", 4);
            Add("Rune Ore", 128);
            Add("Dragon Ore", 56);
            Add("Ruby", 14);
            Add("Dragon Scale", 6);
            Add("Rune Bar", 37);
            Add("Dragonwood Logs", 183);
            Add("Raw Bass", 64);
            Add("Raw Tuna", 21);
            Add("Wheat", 1450);
            Add("Tomato", 212);
            Add("Grapes", 48);
            Add("Mushroom", 95);
            Add("Red Clover", 33);
            Add("Hide", 77);
            Add("Vial", 40);
            Add("String", 16);
        }

        private static string Sockets(params (GemKind Kind, GemTier Tier)?[] gems)
        {
            return Gems.FormatSockets(gems.Select(g => g.HasValue ? Gems.ItemId(g.Value.Kind, g.Value.Tier) : (Guid?)null).ToList());
        }

        private void Add(string name, long amount = 1, bool worn = false, string enchantment = null, string sockets = null)
        {
            // an item this server's data does not have is left out, so the preview never breaks on a renamed item
            if (!byName.TryGetValue(name, out var item)) return;
            bag.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                ItemId = item.Id,
                Amount = amount,
                Equipped = worn,
                Enchantment = enchantment,
                Sockets = sockets,
                Soulbound = item.Soulbound,
            });
        }

        // ---- What the page asks ---------------------------------------------------------------------------------------

        public bool IsOwner(WebsitePlayer player) => owner;

        public bool IsAdministrator => administrator;

        public long GetCoins(WebsitePlayer player) => coins;

        // a few passives, so the numbers show what a skill tree adds
        public string GetSkillBuild(Guid characterId) => null;

        public Task<IReadOnlyList<WebsitePlayer>> GetMyCharactersAsync()
        {
            IReadOnlyList<WebsitePlayer> list = owner
                ? new[] { GetPlayer(CharacterId), new WebsitePlayer { Id = otherCharacterId, UserId = userId, Name = "Wren", Identifier = "miner", CharacterIndex = 1 } }
                : Array.Empty<WebsitePlayer>();
            return Task.FromResult(list);
        }

        public IReadOnlyList<UserBankItem> GetStash() => stash.Select(x => new UserBankItem
        {
            Id = x.Id, UserId = x.UserId, ItemId = x.ItemId, Amount = x.Amount, Enchantment = x.Enchantment, Sockets = x.Sockets, Tag = x.Tag,
            Soulbound = x.Soulbound, TransmogrificationId = x.TransmogrificationId,
        }).ToList();

        public double GetGemKeepChance(Guid characterId) => 0.25;

        public WebsitePlayer GetPlayer(Guid characterId)
        {
            return new WebsitePlayer
            {
                Id = CharacterId,
                UserId = userId,
                UserName = "wren",
                Name = "Wren",
                CharacterIndex = 0,
                Skills = new SkillsExtended
                {
                    AttackLevel = 142, DefenseLevel = 156, StrengthLevel = 138, HealthLevel = 151, RangedLevel = 94,
                    MagicLevel = 128, HealingLevel = 163, SlayerLevel = 40, WoodcuttingLevel = 88, MiningLevel = 102,
                    FishingLevel = 71, FarmingLevel = 64, CraftingLevel = 80, CookingLevel = 77, SailingLevel = 52,
                    GatheringLevel = 45, AlchemyLevel = 38, TamingLevel = 61,
                },
                State = new CharacterState { Task = "Fighting", TaskArgument = "Healing", Island = "Kyo" },
                Appearance = new SyntyAppearance
                {
                    Gender = Option("gender") == "male" ? Gender.Male : Gender.Female,
                    Head = Number("head", 3), Hair = Number("hair", 12), Eyebrows = 2, FacialHair = Number("beard", -1),
                    SkinColor = "#E8C0A8", HairColor = "#A0461E", BeardColor = "#A0461E", EyeColor = "#3D8A4A",
                    StubbleColor = "#E8C0A8", ScarColor = "#E8C0A8", WarPaintColor = "#000000",
                    // no cape of her own, and the helmet shows (both as the game's toggles leave them by default)
                    Cape = Number("cape", -1), HelmetVisible = helmetVisible ?? Option("helmet") != "0", CapeHidden = capeHidden ?? Option("capehidden") != null,
                },
                // copies, so the page cannot change the "server's" stacks by accident
                InventoryItems = bag.Select(Copy).ToList(),
                BattlePets = pets,
                ActiveBattlePet = petOut == Guid.Empty ? (Guid?)null : petOut,
            };
        }

        private int Number(string option, int otherwise) => int.TryParse(Option(option), out var value) ? value : otherwise;

        // the helmet and cape switches, once used on the page (until then the options say)
        private bool? helmetVisible, capeHidden;

        public GearResult SetHelmetAndCape(Guid characterId, bool? helmet, bool? cape)
        {
            if (!owner && !administrator) return new GearResult { Refused = "You need to be signed in to do that.", Player = GetPlayer(CharacterId), Coins = coins };
            if (helmet != null) helmetVisible = helmet;
            if (cape != null) capeHidden = cape;
            return Result(1);
        }

        private static InventoryItem Copy(InventoryItem x) => new InventoryItem
        {
            Id = x.Id, ItemId = x.ItemId, Amount = x.Amount, Equipped = x.Equipped, Tag = x.Tag, Soulbound = x.Soulbound,
            Enchantment = x.Enchantment, Sockets = x.Sockets, Name = x.Name, TransmogrificationId = x.TransmogrificationId, Flags = x.Flags,
        };

        private InventoryItem Find(InventoryItem item) => item == null ? null : bag.FirstOrDefault(x => x.Id == item.Id);

        private GearResult Result(int done, int asked = 1, long gained = 0)
        {
            return new GearResult { Player = GetPlayer(CharacterId), Done = done, Asked = asked, Coins = coins, Gained = gained };
        }

        private EquipmentSlot SlotOf(InventoryItem stack) => GearRules.SlotOf(items.GetItem(stack.ItemId));

        private bool CanWear(InventoryItem stack)
        {
            var item = items.GetItem(stack.ItemId);
            return item != null && GearRules.SlotOf(item) != EquipmentSlot.None && RavenNest.BusinessLogic.Game.PlayerInventory.CanEquipItem(item, GetPlayer(CharacterId));
        }

        // one piece joins the plain stack of its kind again, as stacks do on the server
        private void PutBack(InventoryItem piece)
        {
            piece.Equipped = false;
            if (piece.Enchantment != null || piece.Sockets != null || piece.TransmogrificationId != null) return;
            var same = bag.FirstOrDefault(x => x != piece && !x.Equipped && x.ItemId == piece.ItemId && x.Enchantment == null && x.Sockets == null && x.TransmogrificationId == null);
            if (same == null) return;
            same.Amount += piece.Amount;
            bag.Remove(piece);
        }

        private InventoryItem TakeOne(InventoryItem stack)
        {
            if (stack.Amount <= 1) return stack;
            stack.Amount--;
            var one = Copy(stack);
            one.Id = Guid.NewGuid();
            one.Amount = 1;
            bag.Add(one);
            return one;
        }

        private bool Wear(InventoryItem stack)
        {
            if (stack == null || stack.Equipped || !CanWear(stack)) return false;
            var slot = SlotOf(stack);
            foreach (var wornNow in bag.Where(x => x.Equipped && SlotOf(x) == slot).ToList()) PutBack(wornNow);
            TakeOne(stack).Equipped = true;
            return true;
        }

        public GearResult Equip(Guid characterId, InventoryItem item) => Result(Wear(Find(item)) ? 1 : 0);

        // ---- Outfits: the same answers OutfitService gives, kept in memory ---------------------------------------------

        private readonly CharacterOutfits outfits = new CharacterOutfits();
        private readonly BattlePet[] pets =
        {
            new BattlePet { Id = Guid.NewGuid(), Name = "Bramble", Level = 14 },
            new BattlePet { Id = Guid.NewGuid(), Name = "Moss", Level = 6 },
        };
        private Guid petOut;

        // what a player who has trained two things has: the healing outfit that is on (one piece pinned), and a magic
        // one kept from the last time magic was trained
        private void FillOutfits()
        {
            outfits.Worn = Skill.Healing;
            outfits.Fight = Skill.Healing;
            var healing = outfits.GetOrAdd(Skill.Healing);
            foreach (var stack in bag.Where(x => x.Equipped))
            {
                var slot = (RavenNest.Models.EquipmentSlot)(int)SlotOf(stack);
                if (!OutfitRules.IsOutfitSlot(slot)) continue;
                healing.Set(slot, stack.Id, stack.ItemId, slot == RavenNest.Models.EquipmentSlot.Head);
            }
            healing.Pet = pets[0].Id;
            petOut = pets[0].Id;

            var magic = outfits.GetOrAdd(Skill.Magic);
            foreach (var name in new[] { "Conjurer Hood", "Phantom Chest", "Dragon Staff", "Phantom Amulet" })
            {
                var stack = byName.TryGetValue(name, out var item) ? bag.FirstOrDefault(x => x.ItemId == item.Id && !x.Equipped) : null;
                if (stack == null) continue;
                magic.Set((RavenNest.Models.EquipmentSlot)(int)SlotOf(stack), stack.Id, stack.ItemId, name == "Dragon Staff");
            }
            if (magic.Slots.Count == 0) outfits.Remove(Skill.Magic);
        }

        private OutfitChange Outfit(OutfitStatus status, Skill skill, bool changed = false)
        {
            if (!owner && !administrator) return new OutfitChange { Refused = "You need to be signed in to do that." };
            return new OutfitChange
            {
                Result = new OutfitResult { Status = status, Skill = (int)skill, Changed = changed, Outfits = outfits.ToString() },
                Outfits = CharacterOutfits.Parse(outfits.ToString()),
            };
        }

        private InventoryItem PieceOf(OutfitSlot entry)
        {
            return bag.FirstOrDefault(x => x.Id == entry.Item)
                ?? bag.Where(x => x.ItemId == entry.ItemId).OrderByDescending(x => x.Equipped).FirstOrDefault();
        }

        public OutfitChange GetOutfits(Guid characterId) => Outfit(OutfitStatus.Ok, Skill.None);

        public OutfitChange SetOutfitSlot(Guid characterId, Skill skill, EquipmentSlot slot, Guid inventoryItemId, bool pinned)
        {
            skill = OutfitRules.Normalize(skill);
            if (!owner && !administrator) return Outfit(OutfitStatus.Error, skill);
            if (skill == Skill.None) return Outfit(OutfitStatus.UnknownSkill, skill);
            var stack = bag.FirstOrDefault(x => x.Id == inventoryItemId);
            if (stack == null || SlotOf(stack) != slot || !OutfitRules.IsOutfitSlot((RavenNest.Models.EquipmentSlot)(int)slot) || !CanWear(stack)) return Outfit(OutfitStatus.BadItem, skill);
            var changed = false;
            if (outfits.Worn == skill && !stack.Equipped)
            {
                changed = Wear(stack);
                stack = bag.FirstOrDefault(x => x.Equipped && SlotOf(x) == slot) ?? stack;
            }
            outfits.GetOrAdd(skill).Set((RavenNest.Models.EquipmentSlot)(int)slot, stack.Id, stack.ItemId, pinned);
            return Outfit(OutfitStatus.Ok, skill, changed);
        }

        public OutfitChange ClearOutfitSlot(Guid characterId, Skill skill, EquipmentSlot slot)
        {
            skill = OutfitRules.Normalize(skill);
            if (!owner && !administrator) return Outfit(OutfitStatus.Error, skill);
            var outfit = outfits.Find(skill);
            if (outfit == null) return Outfit(OutfitStatus.NoOutfit, skill);
            outfit.Clear((RavenNest.Models.EquipmentSlot)(int)slot);
            if (outfit.Slots.Count == 0 && outfit.Pet == Guid.Empty) outfits.Remove(skill);
            return Outfit(OutfitStatus.Ok, skill);
        }

        public OutfitChange SetOutfitPet(Guid characterId, Skill skill, Guid petId)
        {
            skill = OutfitRules.Normalize(skill);
            if (!owner && !administrator) return Outfit(OutfitStatus.Error, skill);
            if (skill == Skill.None) return Outfit(OutfitStatus.UnknownSkill, skill);
            if (petId != Guid.Empty && !pets.Any(x => x.Id == petId)) return Outfit(OutfitStatus.BadPet, skill);
            outfits.GetOrAdd(skill).Pet = petId;
            if (outfits.Worn == skill && petId != Guid.Empty) petOut = petId;
            return Outfit(OutfitStatus.Ok, skill);
        }

        public OutfitChange WearOutfit(Guid characterId, Skill skill)
        {
            skill = OutfitRules.Normalize(skill);
            if (!owner && !administrator) return Outfit(OutfitStatus.Error, skill);
            var outfit = outfits.Find(skill);
            if (outfit == null) return Outfit(OutfitStatus.NoOutfit, skill);
            foreach (var entry in outfit.Slots)
            {
                var stack = PieceOf(entry);
                if (stack == null || stack.Equipped) continue;
                if (Wear(stack))
                {
                    var now = bag.FirstOrDefault(x => x.Equipped && x.ItemId == entry.ItemId);
                    if (now != null) entry.Item = now.Id;
                }
            }
            if (outfit.Pet != Guid.Empty) petOut = outfit.Pet;
            outfits.Worn = skill;
            return Outfit(OutfitStatus.Ok, skill, true);
        }

        public OutfitChange SetOutfitSwitches(Guid characterId, bool? changeWithTraining, bool? upgradeAutomatically)
        {
            if (!owner && !administrator) return Outfit(OutfitStatus.Error, Skill.None);
            if (changeWithTraining.HasValue) outfits.Follow = changeWithTraining.Value;
            if (upgradeAutomatically.HasValue) outfits.Upgrade = upgradeAutomatically.Value;
            return Outfit(OutfitStatus.Ok, Skill.None);
        }

        public GearResult Unequip(Guid characterId, InventoryItem item)
        {
            var stack = Find(item);
            if (stack == null || !stack.Equipped) return Result(0);
            PutBack(stack);
            return Result(1);
        }

        // The game's own comparison, slot by slot. The server's "equip best" also narrows the fitting class gear
        // lines to one for the whole outfit; this stand-in does not.
        public GearResult EquipBest(Guid characterId, string skill)
        {
            var outfit = GearOutfit.Parse(skill);
            var dressFor = outfit != null ? outfit.Skill : GearScore.Trained("Fighting", "Healing");
            foreach (var slot in GearRules.All)
            {
                while (true)
                {
                    var wornNow = bag.FirstOrDefault(x => x.Equipped && SlotOf(x) == slot.Slot);
                    var wornItem = wornNow == null ? null : items.GetItem(wornNow.ItemId);
                    var wornWorth = wornNow == null ? 0 : Worth(wornNow);
                    var better = bag
                        .Where(x => !x.Equipped && SlotOf(x) == slot.Slot && CanWear(x))
                        .OrderByDescending(Worth)
                        .FirstOrDefault(x => GearRules.Beats(items.GetItem(x.ItemId), Worth(x), wornItem, wornWorth, dressFor));
                    if (better == null || !Wear(better)) break;
                }
            }

            return Result(1);
        }

        private int Worth(InventoryItem stack)
        {
            return GearRules.Classic(items.GetItem(stack.ItemId), string.IsNullOrEmpty(stack.Enchantment) ? null : items.GetItemEnchantments(stack));
        }

        public GearResult SendToStash(Guid characterId, IReadOnlyList<InventoryItem> list)
        {
            var done = 0;
            foreach (var item in list)
            {
                var stack = Find(item);
                if (stack == null) continue;
                bag.Remove(stack);
                stash.Add(new UserBankItem
                {
                    Id = Guid.NewGuid(), UserId = userId, ItemId = stack.ItemId, Amount = stack.Amount, Enchantment = stack.Enchantment,
                    Sockets = stack.Sockets, Tag = stack.Tag, Soulbound = stack.Soulbound, TransmogrificationId = stack.TransmogrificationId,
                });
                done++;
            }

            return Result(done, list.Count);
        }

        public GearResult SendToCharacter(Guid characterId, Guid toCharacterId, InventoryItem item, long amount)
        {
            var stack = Find(item);
            if (stack == null) return Result(0);
            bag.Remove(stack);
            return Result(1);
        }

        public GearResult Sell(Guid characterId, InventoryItem item, long amount)
        {
            var gained = SellStack(Find(item), amount);
            return Result(gained.HasValue ? 1 : 0, 1, gained ?? 0);
        }

        public GearResult Sell(Guid characterId, IReadOnlyList<InventoryItem> list)
        {
            var done = 0;
            long gained = 0;
            foreach (var item in list)
            {
                var stack = Find(item);
                var sold = stack == null ? null : SellStack(stack, stack.Amount);
                if (!sold.HasValue) continue;
                gained += sold.Value;
                done++;
            }

            return Result(done, list.Count, gained);
        }

        private long? SellStack(InventoryItem stack, long amount)
        {
            if (stack == null || stack.Equipped) return null;
            var item = items.GetItem(stack.ItemId);
            if (item == null || item.Category == ItemCategory.StreamerToken) return null;
            amount = Math.Clamp(amount, 1, stack.Amount);
            stack.Amount -= amount;
            if (stack.Amount <= 0) bag.Remove(stack);
            var price = item.ShopSellPrice * amount;
            coins += price;
            return price;
        }

        public GearResult AddTransmogrification(Guid characterId, InventoryItem item, Guid skinItemId, UserBankItem fromStash)
        {
            var target = Find(item);
            if (target == null) return Result(0);

            if (fromStash != null)
            {
                var stashed = stash.FirstOrDefault(x => x.Id == fromStash.Id);
                if (stashed == null) return Result(0);
                stashed.Amount--;
                if (stashed.Amount <= 0) stash.Remove(stashed);
            }
            else
            {
                var skin = bag.FirstOrDefault(x => !x.Equipped && x.ItemId == skinItemId && string.IsNullOrEmpty(x.Enchantment));
                if (skin == null) return Result(0);
                skin.Amount--;
                if (skin.Amount <= 0) bag.Remove(skin);
            }

            // the old skin comes back, and the piece with the new one is a stack of its own
            if (target.TransmogrificationId.HasValue) Give(target.TransmogrificationId.Value, 1);
            if (!target.Equipped) target = TakeOne(target);
            target.TransmogrificationId = skinItemId;
            return Result(1);
        }

        public GearResult RemoveTransmogrification(Guid characterId, InventoryItem item)
        {
            var target = Find(item);
            if (target == null || !target.TransmogrificationId.HasValue) return Result(0);
            Give(target.TransmogrificationId.Value, 1);
            target.TransmogrificationId = null;
            if (!target.Equipped) PutBack(target);
            return Result(1);
        }

        private void Give(Guid itemId, long amount)
        {
            var same = bag.FirstOrDefault(x => !x.Equipped && x.ItemId == itemId && x.Enchantment == null && x.Sockets == null && x.TransmogrificationId == null);
            if (same != null) same.Amount += amount;
            else bag.Add(new InventoryItem { Id = Guid.NewGuid(), ItemId = itemId, Amount = amount });
        }

        // The server's two-step rule: a socket that holds a gem only answers with that gem and the chance to keep
        // it; the change is done when the call comes again naming the gem.
        public GemChange SocketGem(Guid characterId, Guid inventoryItemId, int kind, int tier, int socket, Guid? confirmedGem)
        {
            return ChangeGem(inventoryItemId, socket, confirmedGem, Gems.ItemId((GemKind)kind, (GemTier)tier), kind, tier);
        }

        public GemChange UnsocketGem(Guid characterId, Guid inventoryItemId, int socket, Guid? confirmedGem)
        {
            return ChangeGem(inventoryItemId, socket, confirmedGem, null, 0, 0);
        }

        private GemChange ChangeGem(Guid stackId, int socket, Guid? confirmedGem, Guid? newGem, int kind, int tier)
        {
            GemChange Answer(GemResult result, Guid id) => new GemChange { Result = result, ItemId = id, Player = GetPlayer(CharacterId) };

            var stack = bag.FirstOrDefault(x => x.Id == stackId);
            var item = stack == null ? null : items.GetItem(stack.ItemId);
            if (item == null) return Answer(GemResult.Fail(GemResultStatus.NoSuchItem), stackId);

            var views = GemManager.SocketsOf(item, stack.Sockets);
            if (views.Count == 0) return Answer(GemResult.Fail(GemResultStatus.NoSockets), stackId);
            if (socket < 1 || socket > views.Count) return Answer(new GemResult { Status = GemResultStatus.NoSuchSocket, SocketCount = views.Count }, stackId);

            var now = views.Select(x => x.GemId).ToList();
            var old = now[socket - 1];
            var keep = GetGemKeepChance(CharacterId);
            var result = new GemResult { Status = GemResultStatus.Ok, Kind = kind, Tier = tier, Socket = socket, SocketCount = views.Count, KeepChance = keep, ItemName = item.Name };

            if (!newGem.HasValue && !old.HasValue) return Answer(GemResult.Fail(GemResultStatus.EmptySocket), stackId);
            if (newGem.HasValue && !bag.Any(x => !x.Equipped && x.ItemId == newGem.Value && x.Amount > 0)) return Answer(GemResult.Fail(GemResultStatus.NoGem), stackId);

            if (old.HasValue)
            {
                Gems.TryGetGem(old.Value, out var oldKind, out var oldTier);
                result.OldKind = (int)oldKind;
                result.OldTier = (int)oldTier;
                if (confirmedGem != old.Value)
                {
                    result.Status = GemResultStatus.NeedsConfirm;
                    return Answer(result, stackId);
                }

                result.Kept = random.NextDouble() < keep;
                if (result.Kept) Give(old.Value, 1);
            }

            if (newGem.HasValue)
            {
                var gems = bag.First(x => !x.Equipped && x.ItemId == newGem.Value && x.Amount > 0);
                gems.Amount--;
                if (gems.Amount <= 0) bag.Remove(gems);
            }

            // one piece of a stack takes the gem: it becomes a stack of its own
            if (!stack.Equipped) stack = TakeOne(stack);
            now[socket - 1] = newGem;
            stack.Sockets = Gems.FormatSockets(now);
            return Answer(result, stack.Id);
        }

        public GearResult AddItem(Guid characterId, Item item, int amount)
        {
            if (!administrator || item == null) return Result(0);
            Give(item.Id, Math.Max(1, amount));
            return Result(1);
        }

        public long? SetCoins(Guid userId, long amount)
        {
            if (!administrator) return null;
            coins = Math.Max(0, amount);
            return coins;
        }
    }
}
