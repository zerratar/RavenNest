using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.JSInterop;
using RavenNest.Blazor.Extensions;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Game;
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

namespace RavenNest.Blazor.Components
{
    /// <summary>
    ///     The state of the gear tabs and what each click does. The markup is in PlayerGear.razor; gems and skins
    ///     have their own files (PlayerGear.Gems.cs, PlayerGear.Skins.cs).
    /// </summary>
    public partial class PlayerGear : IAsyncDisposable
    {
        /// <summary>The character to show. The tab reads it again from the server when it opens.</summary>
        [Parameter] public WebsitePlayer Player { get; set; }

        /// <summary>The viewer may change things (their own character, or an administrator's view of one).</summary>
        [Parameter] public bool CanManage { get; set; }

        /// <summary>"equipment" or "inventory"; null shows both behind a switch of its own (the admin pages).</summary>
        [Parameter] public string View { get; set; }

        /// <summary>Where actions go; the real server when not given.</summary>
        [Parameter] public IGearBackend Backend { get; set; }

        /// <summary>An item to open with (the preview's screenshots): a stack's id.</summary>
        [Parameter] public Guid? InitialPick { get; set; }

        [Inject] private ItemService ItemService { get; set; }
        [Inject] private GearService GearService { get; set; }
        [Inject] private IJSRuntime JS { get; set; }
        [Inject] private IWebHostEnvironment Environment { get; set; }

        private IGearBackend Server => Backend ?? GearService;

        /// <summary>One stack of the inventory with everything the page shows about it, worked out once per change.</summary>
        internal sealed class Entry
        {
            public InventoryItem Stack;
            public Item Item;
            public EquipmentSlot Slot;
            public GearKind Kind;
            public string Group;
            public string Name;
            public string Picture;
            public ItemRarityBand Rarity;
            public string RarityClass;
            /// <summary>Its worth by the rule "equip best" starts from.</summary>
            public int Classic;
            /// <summary>The character meets its requirements.</summary>
            public bool CanWear;
            /// <summary>What is missing when it cannot be worn: "Needs Attack 120."</summary>
            public string Blocked;
            /// <summary>Better than the piece it is compared with (the outfit's in a skill's tab, the worn one on "Now"), by the best rule.</summary>
            public bool Upgrade;
            /// <summary>The one best rule (OutfitRules.BestPicks) picks this very piece for its slot, for the skill the page is about.</summary>
            public bool BestPick;
            public List<GemManager.SocketView> Sockets;
            public bool Enchanted;
            /// <summary>It can leave the bag (it is not worn).</summary>
            public bool CanMove;
            public bool CanSell;
            public string WhyNotSell;
            public long SellValue;
        }

        private enum Dialog { None, Sell, SellSeveral, StashSeveral, Skin, AddItem, Coins }

        // the character as the server last gave it, and the parameter it came for
        private WebsitePlayer player;
        private WebsitePlayer seenParameter;
        private long coins;
        private IReadOnlyList<WebsitePlayer> otherCharacters = Array.Empty<WebsitePlayer>();

        private List<Entry> entries = new List<Entry>();
        private Dictionary<Guid, Entry> byId = new Dictionary<Guid, Entry>();
        private Dictionary<EquipmentSlot, Entry> worn = new Dictionary<EquipmentSlot, Entry>();
        private GearSheet sheet;

        private string ownView = "equipment";
        private string notice;
        private bool noticeBad;
        private Dialog dialog;
        private Entry dialogEntry;
        private int clearSignal;

        // the Equipment tab
        private Skill? dressFor;
        private EquipmentSlot pickedSlot = EquipmentSlot.None;
        private Guid? pickedId;
        private bool detailOpen;
        private bool allStats;
        private string wearSearch = string.Empty;
        private GearKind wearKind = GearKind.All;
        private string wearSort = "slot";

        // the Inventory tab
        private string search = string.Empty;
        private string sort = "best";
        private GearKind invKind = GearKind.All;
        private string invGroup;
        private bool several;
        private readonly HashSet<Guid> selected = new HashSet<Guid>();
        private long sellAmount = 1;

        // administrators
        private Item addItem;
        private int addAmount = 1;
        private string coinsText;
        private string coinsMessage;
        private bool coinsError;

        // the page's script: dragging, and the character in the middle
        private ElementReference root;
        private ElementReference stageHost;
        private IJSObjectReference module;
        private DotNetObjectReference<PlayerGear> self;
        private bool attached;
        private Guid? figureOf;
        private string figureState = "is-loading";
        // the figure wears the character's gear (the 3D view with the gear export; the flat view shows the look alone)
        private bool figureDressed;
        private string figureWears;

        private string ActiveView => View ?? ownView;
        private bool CanAct => CanManage && player != null;
        private bool CanSocket => CanManage && player != null && Server.IsOwner(player);
        private bool IsAdmin => CanManage && Server.IsAdministrator;
        private IEnumerable<WebsitePlayer> OtherCharacters => otherCharacters.Where(x => x.Id != player.Id);

        /// <summary>The combat skill the character trains now; None when it trains none.</summary>
        private Skill Trained => GearScore.Trained(player?.State?.Task, player?.State?.TaskArgument);

        /// <summary>What the character does now, in a word or two ("Healing", "Woodcutting"); null when nothing.</summary>
        private string TrainingNow
        {
            get
            {
                var state = player?.State;
                if (state == null || string.IsNullOrWhiteSpace(state.Task)) return null;
                if (Trained != Skill.None) return Trained.ToString();
                var word = state.Task.Trim().Equals("Fighting", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(state.TaskArgument)
                    ? state.TaskArgument.Trim()
                    : state.Task.Trim();
                return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
            }
        }

        /// <summary>
        ///     The skill the numbers and the upgrade marks are for: the picked one, or on "Now" the skill of the outfit
        ///     that is on (the review: after the Magic outfit was put on by hand, "Now" still counted for Healing), or
        ///     what is trained when outfits are not known.
        /// </summary>
        private Skill EffectiveSkill => dressFor ?? (OutfitsKnown && WornOutfitSkill != Skill.None ? WornOutfitSkill : Trained);

        /// <summary>The skill the page is about, as a word: "Strength", "All"; empty for none.</summary>
        private string SkillWord => EffectiveSkill == Skill.None ? string.Empty : GearRules.SkillName(EffectiveSkill);

        private Entry PickedEntry => pickedId.HasValue && byId.TryGetValue(pickedId.Value, out var e) ? e : null;
        private int WearableCount => entries.Count(x => x.Slot != EquipmentSlot.None && !x.Stack.Equipped);
        private int WornCount => entries.Count(x => x.Stack.Equipped);

        /// <summary>The character owns something an outfit can hold (worn or in the bag): without it, "equip best" and the switches mean nothing.</summary>
        private bool HasGear => entries.Any(x => IsOutfitSlot(x.Slot));

        protected override async Task OnParametersSetAsync()
        {
            if (Player == null || ReferenceEquals(Player, seenParameter)) return;

            var otherCharacter = seenParameter == null || seenParameter.Id != Player.Id;
            seenParameter = Player;

            // the tab opens on what the server has now: the parameter may be as old as the page
            player = Server.GetPlayer(Player.Id) ?? Player;
            coins = Server.GetCoins(player);
            if (otherCharacter)
            {
                dressFor = null;
                pickedSlot = EquipmentSlot.None;
                pickedId = InitialPick;
                detailOpen = false;
                several = false;
                selected.Clear();
                dialog = Dialog.None;
                notice = null;
                moreSkills = false;
                ResetGemState();
            }

            LoadOutfits();
            Rebuild();
            if (otherCharacter && pickedId.HasValue && byId.TryGetValue(pickedId.Value, out var first)) pickedSlot = first.Slot;

            if (otherCharacter)
            {
                otherCharacters = CanManage && Server.IsOwner(player) ? await Server.GetMyCharactersAsync() ?? Array.Empty<WebsitePlayer>() : Array.Empty<WebsitePlayer>();
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (player == null) return;
            try
            {
                if (module == null)
                {
                    module = await JS.InvokeAsync<IJSObjectReference>("import", Environment.Versioned("js/rf-gear.js"));
                    self = DotNetObjectReference.Create(this);
                }

                if (!attached)
                {
                    attached = true;
                    await module.InvokeVoidAsync("attach", root, self);
                }

                if (ActiveView == "equipment")
                {
                    await ReadStanceAsync();
                    var wears = FigureKey();
                    if (figureOf != player.Id)
                    {
                        figureOf = player.Id;
                        figureWears = wears;
                        var mode = await module.InvokeAsync<string>("figure", stageHost, Environment.Versioned("js/rf-character-designer.js"),
                            Environment.Versioned("js/rf-character-gear.js"), player.Appearance, FigureOutfit());
                        figureDressed = mode == "3d+gear";
                        figureState = mode.StartsWith("3d") ? "is-3d" : mode == "2d" ? "is-flat" : "is-none";
                        StateHasChanged();
                    }
                    else if (figureDressed && figureWears != wears)
                    {
                        // something was put on or taken off, or another skill is dressed for: the server has
                        // answered and the page shows it, and now the figure does
                        figureWears = wears;
                        await module.InvokeVoidAsync("dressFigure", FigureOutfit());
                    }
                }
                else if (figureOf != null)
                {
                    figureOf = null;
                    figureDressed = false;
                    figureState = "is-loading";
                    await module.InvokeVoidAsync("dropFigure");
                }
            }
            catch (JSDisconnectedException)
            {
            }
            catch (JSException)
            {
                // without the script the page still works by click; the character's place shows a still figure
                if (figureState != "is-none")
                {
                    figureState = "is-none";
                    StateHasChanged();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (module != null)
                {
                    await module.InvokeVoidAsync("detach", root);
                    await module.DisposeAsync();
                }
            }
            catch (JSDisconnectedException)
            {
            }
            catch (JSException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            noticeTimer?.Cancel();
            self?.Dispose();
        }

        // ---- What the page shows, worked out from the character ------------------------------------------------------

        private void Rebuild()
        {
            entries = new List<Entry>();
            byId = new Dictionary<Guid, Entry>();
            worn = new Dictionary<EquipmentSlot, Entry>();

            foreach (var stack in player.InventoryItems ?? Array.Empty<InventoryItem>())
            {
                var item = ItemService.GetItem(stack.ItemId);
                if (item == null || stack.Amount <= 0) continue;

                var token = item.Category == ItemCategory.StreamerToken;
                var e = new Entry
                {
                    Stack = stack,
                    Item = item,
                    Slot = GearRules.SlotOf(item),
                    Kind = GearRules.KindOf(item),
                    Group = GearRules.GroupOf(item),
                    Name = stack.Name ?? item.Name,
                    // a piece with a skin shows the skin, as it does in the game
                    Picture = ItemService.GetInventoryItemImage(stack.TransmogrificationId ?? stack.ItemId, stack.Tag),
                    Rarity = ItemService.GetRarity(item),
                    RarityClass = ItemService.GetRarityClass(item),
                    Enchanted = !string.IsNullOrEmpty(stack.Enchantment),
                    Sockets = GemManager.SocketsOf(item, stack.Sockets),
                    CanMove = !stack.Equipped,
                    SellValue = item.ShopSellPrice * stack.Amount,
                };
                e.Classic = GearRules.Classic(item, e.Enchanted ? ItemService.GetItemEnchantments(stack) : null);
                e.Blocked = e.Slot == EquipmentSlot.None ? null : RequirementNote(item);
                e.CanWear = e.Slot != EquipmentSlot.None && e.Blocked == null && RavenNest.BusinessLogic.Game.PlayerInventory.CanEquipItem(item, player);
                e.CanSell = e.CanMove && stack.TransmogrificationId == null && !token;
                e.WhyNotSell = token ? "The vendor does not take streamer tokens."
                    : stack.TransmogrificationId != null ? "Remove the skin before selling this." : null;

                entries.Add(e);
                byId[stack.Id] = e;
                if (stack.Equipped && e.Slot != EquipmentSlot.None && !worn.ContainsKey(e.Slot)) worn[e.Slot] = e;
            }

            if (pickedId.HasValue && !byId.ContainsKey(pickedId.Value))
            {
                pickedId = null;
                detailOpen = false;
                ResetGemState();
            }

            selected.RemoveWhere(id => !byId.TryGetValue(id, out var e) || !e.CanMove);
            if (invKind != GearKind.All && !entries.Any(x => x.Kind == invKind))
            {
                invKind = GearKind.All;
                invGroup = null;
            }
            if (wearKind != GearKind.All && !entries.Any(x => x.Kind == wearKind && x.Slot != EquipmentSlot.None && !x.Stack.Equipped)) wearKind = GearKind.All;

            Judge();
        }

        /// <summary>The numbers and the upgrade marks, which follow the skill that is dressed for.</summary>
        private void Judge()
        {
            var skill = EffectiveSkill;
            // a skill without an outfit shows what making one would pick: asked again after every change
            proposal = NoOutfitYet && CanAct ? Server.ProposeOutfit(player.Id, OutfitSkill) : null;

            // an outfit that is looked at (kept or proposed): its numbers. The sheet reads what is marked worn, so the
            // marks are set for the count and put back (these stacks are the page's own copy of the server's answer).
            List<(InventoryItem Stack, bool Was)> marks = null;
            if (LooksAtOutfit)
            {
                var shown = new HashSet<Guid>(ShownPieces().Select(x => x.Stack.Id));
                marks = entries.Where(x => x.Slot != EquipmentSlot.None).Select(x => (x.Stack, x.Stack.Equipped)).ToList();
                foreach (var e in entries.Where(x => x.Slot != EquipmentSlot.None)) e.Stack.Equipped = shown.Contains(e.Stack.Id);
            }

            try
            {
                sheet = GearSheet.Build(player, GearRules.StyleOf(skill), Server.GetSkillBuild(player.Id), id => ItemService.GetItem(id),
                    stack => string.IsNullOrEmpty(stack.Enchantment) ? null : ItemService.GetItemEnchantments(stack));
            }
            finally
            {
                if (marks != null) foreach (var mark in marks) mark.Stack.Equipped = mark.Was;
            }

            // the arrows and "equip best would pick this": the one best rule the outfits and the server's equip best
            // share, for the outfit the page is about, measured against the piece it has in the slot
            var pieces = RulePieces(out _);
            var bestOutfit = BestOutfit();
            var picks = OutfitRules.BestPicks(bestOutfit, pieces);
            var picked = new HashSet<Guid>();
            foreach (var index in picks) if (index >= 0) picked.Add(pieces[index].Id);
            var fits = OutfitRules.FitsForBest(bestOutfit, pieces);

            foreach (var e in entries)
            {
                e.Upgrade = false;
                e.BestPick = false;
                if (e.Stack.Equipped || !e.CanWear || e.Slot == EquipmentSlot.None) continue;
                var current = SkillTab ? Shown(e.Slot) : (worn.TryGetValue(e.Slot, out var w) ? w : null);
                if (current != null && Same(current, e)) continue;
                if (!IsOutfitSlot(e.Slot))
                {
                    e.Upgrade = GearRules.Beats(e.Item, e.Classic, current?.Item, current?.Classic ?? 0, skill);
                    continue;
                }
                var look = !ItemLines.IsClassGear(e.Item.Material) && (e.Item.Type == ItemType.Hat || e.Item.Type == ItemType.Mask);
                if (e.Classic <= 0 || look) continue;
                var against = current?.Item.Material ?? ItemMaterial.None;
                e.Upgrade = GearScore.Beats(e.Item.Material, e.Classic, against, current?.Classic ?? 0, GearScore.KeepWorn(fits, against));
                e.BestPick = picked.Contains(e.Stack.Id);
            }
        }

        /// <summary>
        ///     Null when the character meets every requirement of the item, otherwise what is missing. A magic
        ///     requirement is met by either Magic or Healing, as the game's own tooltip says.
        /// </summary>
        private string RequirementNote(Item item)
        {
            var missing = Requirements(item).Where(x => !x.Met).Select(x => x.Skill + " " + x.Level).ToList();
            return missing.Count == 0 ? null : "Needs " + string.Join(", ", missing) + ".";
        }

        internal sealed record Requirement(string Skill, int Level, int Has, bool Met);

        private List<Requirement> Requirements(Item item)
        {
            var list = new List<Requirement>();
            var skills = player?.Skills;
            if (item == null || skills == null) return list;
            void Add(string skill, int level, int has)
            {
                if (level > 0) list.Add(new Requirement(skill, level, has, has >= level));
            }

            Add("Attack", item.RequiredAttackLevel, skills.AttackLevel);
            Add("Defense", item.RequiredDefenseLevel, skills.DefenseLevel);
            Add("Ranged", item.RequiredRangedLevel, skills.RangedLevel);
            Add("Magic or Healing", item.RequiredMagicLevel, Math.Max(skills.MagicLevel, skills.HealingLevel));
            Add("Slayer", item.RequiredSlayerLevel, skills.SlayerLevel);
            // the Beastmaster line needs Taming: no column of its own on the item, the line and the tier give it
            Add("Taming", ItemLines.TamingLevelFor(item.Material, item.Level), skills.TamingLevel);
            return list;
        }

        private IEnumerable<GearKind> WearKinds
        {
            get
            {
                yield return GearKind.All;
                foreach (var kind in new[] { GearKind.Weapons, GearKind.Armor, GearKind.Jewelry, GearKind.Pets, GearKind.Boats })
                {
                    if (entries.Any(x => x.Kind == kind && x.Slot != EquipmentSlot.None && !x.Stack.Equipped)) yield return kind;
                }
            }
        }

        private List<Entry> WearablePieces()
        {
            IEnumerable<Entry> list = entries.Where(x => x.Slot != EquipmentSlot.None && !x.Stack.Equipped);
            if (wearKind != GearKind.All) list = list.Where(x => x.Kind == wearKind);
            if (!string.IsNullOrWhiteSpace(wearSearch))
            {
                var needle = wearSearch.Trim();
                list = list.Where(x => x.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
            }

            switch (wearSort)
            {
                case "best":
                    // what the best rule would pick first, then what beats the piece in the slot, then by worth
                    return list.OrderByDescending(x => x.CanWear).ThenByDescending(x => x.BestPick).ThenByDescending(x => x.Upgrade)
                        .ThenByDescending(x => x.Classic).ThenBy(x => x.Name).ToList();
                case "name":
                    return list.OrderBy(x => x.Name).ToList();
                case "rarity":
                    return list.OrderByDescending(x => x.Rarity).ThenByDescending(x => x.Classic).ThenBy(x => x.Name).ToList();
                default:
                    return list
                        .OrderBy(x => Array.FindIndex(GearRules.All, s => s.Slot == x.Slot))
                        .ThenByDescending(x => x.CanWear)
                        .ThenByDescending(x => x.Classic)
                        .ThenByDescending(x => x.Rarity)
                        .ThenBy(x => x.Name)
                        .ToList();
            }
        }

        private int WearKindCount(GearKind kind) => entries.Count(x => x.Slot != EquipmentSlot.None && !x.Stack.Equipped && (kind == GearKind.All || x.Kind == kind));

        private IEnumerable<GearKind> Kinds
        {
            get
            {
                yield return GearKind.All;
                foreach (GearKind kind in Enum.GetValues(typeof(GearKind)))
                {
                    if (kind != GearKind.All && entries.Any(x => x.Kind == kind)) yield return kind;
                }
            }
        }

        private int KindCount(GearKind kind) => kind == GearKind.All ? entries.Count : entries.Count(x => x.Kind == kind);

        private List<string> GroupsOf(GearKind kind)
        {
            if (kind == GearKind.All) return new List<string>();
            return entries.Where(x => x.Kind == kind && x.Group != null).Select(x => x.Group).Distinct().OrderBy(x => x == "Other" ? "~" : x).ToList();
        }

        private List<Entry> VisibleEntries()
        {
            IEnumerable<Entry> list = entries;
            if (invKind != GearKind.All) list = list.Where(x => x.Kind == invKind);
            if (invGroup != null) list = list.Where(x => x.Group == invGroup);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var needle = search.Trim();
                list = list.Where(x => x.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
            }

            switch (sort)
            {
                case "name":
                    list = list.OrderBy(x => x.Name);
                    break;
                case "amount":
                    list = list.OrderByDescending(x => x.Stack.Amount).ThenBy(x => x.Name);
                    break;
                case "value":
                    list = list.OrderByDescending(x => x.SellValue).ThenBy(x => x.Name);
                    break;
                case "kind":
                    list = list.OrderBy(x => x.Kind).ThenBy(x => x.Group).ThenByDescending(x => x.Rarity).ThenBy(x => x.Name);
                    break;
                default:
                    // what is worn, then gear by how good it is, then everything else by rarity
                    list = list.OrderByDescending(x => x.Stack.Equipped)
                        .ThenByDescending(x => x.Slot != EquipmentSlot.None)
                        .ThenByDescending(x => x.Rarity)
                        .ThenByDescending(x => x.Classic)
                        .ThenBy(x => x.Kind)
                        .ThenBy(x => x.Name);
                    break;
            }

            return list.ToList();
        }

        private List<Entry> SelectedEntries() => entries.Where(x => selected.Contains(x.Stack.Id)).ToList();

        // ---- Words -----------------------------------------------------------------------------------------------------

        private static string Plural(long count, string word) => count.ToString("N0") + " " + word + (count == 1 ? "" : "s");

        private static string Coins(long amount) => amount.ToString("N0") + (amount == 1 ? " coin" : " coins");

        private string What(Entry e)
        {
            var item = e.Item;
            string what;
            switch (e.Kind)
            {
                case GearKind.Weapons: what = ItemService.GetTypeName(item); break;
                case GearKind.Armor:
                    what = e.Slot == EquipmentSlot.Shield ? "Shield" : e.Slot == EquipmentSlot.None ? "Armor" : GearRules.Get(e.Slot)?.Label + " armor";
                    break;
                case GearKind.Jewelry: what = e.Slot == EquipmentSlot.Ring ? "Ring" : "Amulet"; break;
                case GearKind.Pets: what = "Pet"; break;
                case GearKind.Boats: what = "Boat"; break;
                case GearKind.Gems: what = "Gem"; break;
                case GearKind.Food: what = "Food"; break;
                case GearKind.Potions: what = "Potion"; break;
                case GearKind.Scrolls: what = "Scroll"; break;
                case GearKind.Resources: what = e.Group != null && e.Group != "Other" ? e.Group + " resource" : "Resource"; break;
                default: what = item.Category == ItemCategory.StreamerToken ? "Streamer token" : item.Category.ToString(); break;
            }

            if (item.Category == ItemCategory.Cosmetic) what += ", for looks";
            if (RavenNest.Models.ItemLines.IsClassGear(item.Material)) return what;
            var material = ItemService.GetMaterialName(item);
            return string.IsNullOrEmpty(material) || material == "-" || material == "None" ? what : what + ", " + material;
        }

        private string TileTitle(Entry e)
        {
            var parts = new List<string>();
            if (e.Stack.Amount > 1) parts.Add(e.Stack.Amount.ToString("N0"));
            parts.Add(ItemService.GetRarityName(e.Item));
            if (e.Stack.Equipped) parts.Add("worn");
            if (e.BestPick) parts.Add("equip best would pick it");
            else if (e.Upgrade) parts.Add(SkillTab ? "better than the outfit's piece" : "better than what is worn");
            if (e.Enchanted) parts.Add("enchanted");
            if (e.Stack.Soulbound) parts.Add("soulbound");
            return e.Name + " (" + string.Join(", ", parts) + ")";
        }

        private static string SocketsTitle(Entry e)
        {
            var filled = e.Sockets.Count(x => !x.Empty);
            return filled + " of " + Plural(e.Sockets.Count, "gem socket") + " filled";
        }

        private static string EnchantmentName(ItemEnchantment enchantment)
        {
            var name = (enchantment.Name ?? string.Empty).Replace('_', ' ').ToLowerInvariant();
            return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static string EnchantmentValue(ItemEnchantment enchantment)
        {
            return enchantment.ValueType == AttributeValueType.Percent
                ? ((int)(enchantment.Value * 100)) + "%"
                : ((int)enchantment.Value).ToString();
        }

        /// <summary>The stats of a piece with its enchantments counted, decimals kept.</summary>
        private List<GearStatLine> StatsOf(Entry e)
        {
            return GearRules.StatsOf(e.Item, e.Enchanted ? ItemService.GetItemEnchantments(e.Stack) : null);
        }

        internal sealed record CompareRow(string Label, string This, string Worn, string Change, int Delta);

        /// <summary>The two pieces side by side, stat by stat, with enchantments counted.</summary>
        private List<CompareRow> Compare(Entry e, Entry other)
        {
            var mine = StatsOf(e);
            var theirs = StatsOf(other);
            var rows = new List<CompareRow>();
            foreach (var stat in mine.Select(x => x.Stat).Concat(theirs.Select(x => x.Stat)).Distinct())
            {
                var a = mine.FirstOrDefault(x => x.Stat == stat);
                var b = theirs.FirstOrDefault(x => x.Stat == stat);
                var line = a ?? b;
                var av = a?.Total ?? 0;
                var bv = b?.Total ?? 0;
                // a difference too small to show is no difference
                var same = line.Text(av) == line.Text(bv);
                var delta = same ? 0 : av > bv ? 1 : -1;
                rows.Add(new CompareRow(line.Label, line.Text(av), line.Text(bv),
                    same ? "same" : (delta > 0 ? "+" : "-") + line.Text(Math.Abs(av - bv)), delta));
            }

            int Health(Item item) => RavenNest.Combat.CombatMath.IsArmorPiece((int)item.Type) ? RavenNest.Combat.CombatMath.ArmorHealth(item.ArmorPower) : 0;
            var ha = Health(e.Item);
            var hb = Health(other.Item);
            if (ha != 0 || hb != 0)
            {
                rows.Add(new CompareRow("Health", ha.ToString("N0"), hb.ToString("N0"),
                    ha == hb ? "same" : (ha > hb ? "+" : "-") + Math.Abs(ha - hb).ToString("N0"), Math.Sign(ha - hb)));
            }

            return rows;
        }

        /// <summary>The differences in a few words for the line under the character, what is gained first: "+1.5% Heal Shield".</summary>
        private List<(string Text, int Delta)> Gains(Entry e, Entry other, int most)
        {
            return Compare(e, other)
                .Where(x => x.Delta != 0)
                .OrderByDescending(x => x.Delta)
                .Take(most)
                .Select(x => (x.Change + " " + x.Label, x.Delta))
                .ToList();
        }

        // ---- Picking -----------------------------------------------------------------------------------------------------

        /// <summary>"Now": the gear worn this moment, where equipping puts it on at once.</summary>
        private void PickNow()
        {
            if (!dressFor.HasValue) return;
            dressFor = null;
            ClearPick();
            Judge();
        }

        private void PickSkill(Skill skill)
        {
            // a second click on the picked skill does nothing (it used to go back to "Now" without saying so)
            if (dressFor == skill) return;
            dressFor = skill;
            // the slots may show another outfit now: what was picked in them is no longer what is there
            if (OutfitsKnown) ClearPick();
            Judge();
        }

        private void ClearPick()
        {
            pickedSlot = EquipmentSlot.None;
            pickedId = null;
            detailOpen = false;
            ResetGemState();
        }

        private void ClickSlot(GearSlot slot)
        {
            var picked = PickedEntry;

            // a piece is picked and this is where it goes: put it on
            if (CanAct && picked != null && !picked.Stack.Equipped && picked.Slot == slot.Slot && picked.CanWear && Shown(slot.Slot) != picked)
            {
                // into the outfit of the skill that is picked, or onto the body ("Now")
                if (SkillTab && IsOutfitSlot(slot.Slot)) AddToOutfit(picked); else Equip(picked);
                return;
            }

            if (pickedSlot == slot.Slot && (picked == null || picked.Stack.Equipped))
            {
                ClearPick();
                return;
            }

            pickedSlot = slot.Slot;
            pickedId = Shown(slot.Slot)?.Stack.Id;
            ResetGemState();
        }

        // What the last press on the Equipment tab's bag was made with: "mouse", "pen" or "touch".
        private string lastPointer;

        private void ClickTile(Entry e, bool wearView)
        {
            if (several)
            {
                if (!e.CanMove) return;
                if (!selected.Add(e.Stack.Id)) selected.Remove(e.Stack.Id);
                return;
            }

            ResetGemState();

            // A finger has no hover, so there is no card beside the piece: a tap opens the same information as a
            // sheet, with the comparison and the Equip button.
            if (wearView && lastPointer == "touch")
            {
                pickedId = e.Stack.Id;
                pickedSlot = e.Slot;
                detailOpen = true;
                return;
            }

            if (pickedId == e.Stack.Id)
            {
                ClearPick();
                return;
            }

            pickedId = e.Stack.Id;
            pickedSlot = wearView ? e.Slot : EquipmentSlot.None;
        }

        /// <summary>The gems, enchantment and skin of the worn piece a comparison is against, in a few words; empty when it has none.</summary>
        private string WornExtras(Entry wornNow)
        {
            var parts = new List<string>();
            foreach (var socket in wornNow.Sockets)
            {
                if (!socket.Empty) parts.Add(socket.Name + " (" + socket.Bonus + ")");
            }

            if (wornNow.Enchanted)
            {
                foreach (var enchantment in ItemService.GetItemEnchantments(wornNow.Stack))
                {
                    parts.Add(EnchantmentName(enchantment) + " +" + EnchantmentValue(enchantment));
                }
            }

            return parts.Count == 0 ? string.Empty : ", which has " + string.Join(", ", parts);
        }

        private void DoubleClickTile(Entry e, bool wearView)
        {
            if (several || !CanAct || !wearView) return;
            if (!byId.TryGetValue(e.Stack.Id, out var now) || now.Stack.Equipped || !now.CanWear) return;
            if (SkillTab && IsOutfitSlot(now.Slot)) AddToOutfit(now); else Equip(now);
        }

        /// <summary>A piece dragged from the bag onto a slot (the page's script calls this).</summary>
        [JSInvokable]
        public void DropOnSlot(string stackId, string slotKey)
        {
            if (!CanAct || !Guid.TryParse(stackId, out var id) || !byId.TryGetValue(id, out var e)) return;
            var slot = GearRules.Get(e.Slot);
            if (slot == null || slot.Key != slotKey || (e.Stack.Equipped && !SkillTab)) return;
            if (!e.CanWear)
            {
                Say(e.Blocked, true);
            }
            else if (SkillTab)
            {
                // a skill's outfit is looked at: the piece goes into it (and on, only if that outfit is the one on)
                if (IsOutfitSlot(e.Slot)) AddToOutfit(e); else Say("The " + slot.Label.ToLowerInvariant() + " is not part of an outfit.", true);
            }
            else
            {
                Equip(e);
            }

            StateHasChanged();
        }

        /// <summary>A worn piece dragged from its slot into the bag.</summary>
        [JSInvokable]
        public void DropOnBag(string stackId)
        {
            if (!CanAct || !Guid.TryParse(stackId, out var id) || !byId.TryGetValue(id, out var e) || !e.Stack.Equipped) return;
            // a skill's tab changes its outfit, never what is worn
            if (SkillTab) return;
            Unequip(e);
            StateHasChanged();
        }

        private void OnSearch(string value)
        {
            search = value ?? string.Empty;
        }

        private void PickKind(GearKind kind)
        {
            invKind = kind;
            invGroup = null;
        }

        private void PickGroup(string group)
        {
            invGroup = group;
        }

        private void ClearFilters()
        {
            clearSignal++;
            search = string.Empty;
            invKind = GearKind.All;
            invGroup = null;
        }

        private void StartSeveral()
        {
            several = true;
            selected.Clear();
            pickedId = null;
            ResetGemState();
        }

        private void StopSeveral()
        {
            several = false;
            selected.Clear();
        }

        private void SelectAll(List<Entry> shown)
        {
            foreach (var e in shown)
            {
                if (e.CanMove) selected.Add(e.Stack.Id);
            }
        }

        private void OpenSeveral(bool selling)
        {
            if (selected.Count == 0) return;
            dialog = selling ? Dialog.SellSeveral : Dialog.StashSeveral;
        }

        private void CloseDialog()
        {
            dialog = Dialog.None;
            dialogEntry = null;
            skinError = null;
        }

        // ---- Doing things: the server does them, and the page shows what the server says the character is now ----------

        private System.Threading.CancellationTokenSource noticeTimer;

        /// <summary>Says what an action did, over the page, and takes it away again after a few seconds.</summary>
        private void Say(string text, bool bad = false)
        {
            notice = text;
            noticeBad = bad;
            noticeTimer?.Cancel();
            var timer = noticeTimer = new System.Threading.CancellationTokenSource();
            _ = ForgetNotice(timer.Token, bad ? 12000 : 6000);
        }

        private async Task ForgetNotice(System.Threading.CancellationToken token, int afterMilliseconds)
        {
            try
            {
                await Task.Delay(afterMilliseconds, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            notice = null;
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>Takes the server's answer: the character as it is now, and why when nothing was done.</summary>
        private bool Take(GearResult result, string failed)
        {
            if (result == null)
            {
                Say(failed, true);
                return false;
            }

            if (result.Player != null)
            {
                player = result.Player;
                coins = result.Coins;
                Rebuild();
            }

            if (result.Refused != null)
            {
                Say(result.Refused, true);
                return false;
            }

            if (result.Done == 0)
            {
                Say(failed, true);
                return false;
            }

            return true;
        }

        private void Equip(Entry e)
        {
            var slot = e.Slot;
            var name = e.Name;
            var was = worn.TryGetValue(slot, out var before) ? before.Name : null;
            notice = null;
            if (!Take(Server.Equip(player.Id, e.Stack), name + " was not put on. It may be in a trade or being enchanted, or the game changed it a moment ago.")) return;

            // the worn piece can be a new stack (one taken from a stack of several): follow it
            pickedId = worn.TryGetValue(slot, out var now) ? now.Stack.Id : null;
            // what happened, and whether the outfit that is on still has something else there
            var kept = OutfitsKnown && Drifted(slot) ? KeptIn(WornOutfit, slot) : null;
            Say(name + " is on" + (was != null && was != name ? ", in place of " + was : "") + "."
                + (kept != null ? " Your " + GearRules.SkillName(WornOutfitSkill) + " outfit still keeps " + kept.Name + " there: save it to the outfit, or it comes back when the outfit next goes on." : ""));
            if (ActiveView == "equipment")
            {
                pickedSlot = slot;
                // put on from the sheet or the drawer: it closes, so the figure and the slot it went into show
                detailOpen = false;
            }
        }

        private void Unequip(Entry e)
        {
            var itemId = e.Stack.ItemId;
            var name = e.Name;
            notice = null;
            if (!Take(Server.Unequip(player.Id, e.Stack), name + " was not taken off. The game may have changed it a moment ago.")) return;
            Say(name + " is off, back in the bag.");

            // it is back in the bag, perhaps joined with others like it
            if (ActiveView == "equipment")
            {
                pickedId = null;
            }
            else
            {
                var back = entries.FirstOrDefault(x => x.Stack.Id == e.Stack.Id) ?? entries.FirstOrDefault(x => x.Stack.ItemId == itemId && !x.Stack.Equipped);
                pickedId = back?.Stack.Id;
            }
        }

        private void EquipBest()
        {
            // "Now" with outfits: equip best is the outfit that is on filled with the best (pinned slots stay), so
            // what is worn and that outfit stay one and the same, and its rule is the outfits' one best rule
            if (OutfitsKnown && !dressFor.HasValue && WornOutfitSkill != Skill.None)
            {
                FillOutfitBest(WornOutfitSkill);
                return;
            }

            var before = SnapWorn();
            // "all" and the gathering skills have no word of their own for the server's rule: what is trained decides
            var word = dressFor.HasValue && GearRules.DressFor.Contains(dressFor.Value) ? dressFor.Value.ToString().ToLowerInvariant() : null;
            if (!Take(Server.EquipBest(player.Id, word), "Equip best did not run. Try again in a moment.")) return;

            var list = Changes(before, SnapWorn());
            var forWhat = word != null ? " for " + dressFor : Trained != Skill.None ? " for " + Trained : "";
            Say(list.Length == 0 ? "Already wearing the best" + forWhat + "." : "Equip best" + forWhat + " changed " + list + ".");
            ClearPick();
        }

        private void SendToStash(Entry e)
        {
            var name = e.Name;
            var amount = e.Stack.Amount;
            if (!Take(Server.SendToStash(player.Id, new[] { e.Stack }), name + " was not sent. It may be in a trade or being enchanted.")) return;
            Say((amount > 1 ? amount.ToString("N0") + " " : "") + name + " sent to the stash.");
            ClearPick();
        }

        private void SendToCharacter(Entry e, WebsitePlayer to)
        {
            var name = e.Name;
            if (!Take(Server.SendToCharacter(player.Id, to.Id, e.Stack, e.Stack.Amount), name + " was not sent. It may be in a trade or being enchanted.")) return;
            Say(name + " sent to " + to.Alias + ".");
            ClearPick();
        }

        private void OpenSell(Entry e)
        {
            dialogEntry = e;
            sellAmount = e.Stack.Amount;
            dialog = Dialog.Sell;
        }

        private long ClampedSellAmount(Entry e) => Math.Clamp(sellAmount, 1, Math.Max(1, e.Stack.Amount));

        private static List<long> QuickAmounts(long max)
        {
            var list = new List<long>();
            foreach (var step in max > 1000 ? new long[] { 1000, 10000, 50000, 100000 } : new long[] { 1, 2, 5, 10 })
            {
                if (step < max) list.Add(step);
            }

            list.Add(max);
            return list;
        }

        private void SellOne()
        {
            var e = dialogEntry;
            CloseDialog();
            if (e == null || !byId.ContainsKey(e.Stack.Id)) return;
            var amount = Math.Clamp(sellAmount, 1, Math.Max(1, e.Stack.Amount));
            var name = e.Name;
            var result = Server.Sell(player.Id, e.Stack, amount);
            if (!Take(result, name + " was not sold. It may be in a trade or being enchanted.")) return;
            Say("Sold " + (amount > 1 ? amount.ToString("N0") + " " : "") + name + " for " + Coins(result.Gained) + ".");
            if (!byId.ContainsKey(e.Stack.Id)) ClearPick();
        }

        private void DoSeveral(bool selling)
        {
            var chosen = SelectedEntries().Where(x => selling ? x.CanSell : x.CanMove).Select(x => x.Stack).ToList();
            CloseDialog();
            if (chosen.Count == 0) return;

            var result = selling ? Server.Sell(player.Id, chosen) : Server.SendToStash(player.Id, chosen);
            if (!Take(result, selling ? "Nothing was sold. The items may be in use, or the game changed them a moment ago." : "Nothing was sent. The items may be in use, or the game changed them a moment ago.")) return;

            var left = result.Asked - result.Done;
            var text = selling
                ? "Sold " + Plural(result.Done, "stack") + " for " + Coins(result.Gained) + "."
                : "Sent " + Plural(result.Done, "stack") + " to the stash.";
            if (left > 0) text += " " + Plural(left, "stack") + " could not be " + (selling ? "sold" : "sent") + " (in use, or already gone).";
            Say(text, left > 0);
            several = false;
            selected.Clear();
        }

        // ---- Administrators ------------------------------------------------------------------------------------------------

        private async Task<IEnumerable<Item>> SearchItem(string searchText)
        {
            return await ItemService.SearchAsync(searchText);
        }

        private void AddItem()
        {
            var item = addItem;
            var amount = Math.Max(1, addAmount);
            CloseDialog();
            if (item == null) return;
            if (Take(Server.AddItem(player.Id, item, amount), item.Name + " was not added.")) Say("Added " + amount.ToString("N0") + " " + item.Name + ".");
            addItem = null;
            addAmount = 1;
        }

        private void OpenCoins()
        {
            coinsText = coins.ToString();
            coinsMessage = null;
            dialog = Dialog.Coins;
        }

        private void SetCoins()
        {
            var text = (coinsText ?? string.Empty).Trim();
            var sign = text.StartsWith("+") ? 1 : text.StartsWith("-") ? -1 : 0;
            if (sign != 0) text = text.Substring(1);
            if (!AutoJoinSpending.TryParseLimit(text, out var amount) || amount == null)
            {
                coinsError = true;
                coinsMessage = "Type an amount, like 10000000, 10m, or +500k.";
                return;
            }

            var target = sign == 0 ? amount.Value : coins + sign * amount.Value;
            var now = Server.SetCoins(player.UserId, target);
            if (now == null)
            {
                coinsError = true;
                coinsMessage = "Could not set the coins.";
                return;
            }

            coins = now.Value;
            coinsText = coins.ToString();
            coinsError = false;
            coinsMessage = "Coins set to " + coins.ToString("N0") + ".";
        }
    }
}
