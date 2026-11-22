using RavenNest.Blazor.Services;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

using EquipmentSlot = RavenNest.BusinessLogic.Game.EquipmentSlot;

namespace RavenNest.Blazor.Components
{
    /// <summary>
    ///     What the character in the middle of the Equipment tab wears (js/rf-character-gear.js draws it): every worn
    ///     piece as the game needs it to dress a character, and the two toggles. The page says what is worn; which
    ///     parts that is, and how they look, is the script's and the game's export's business.
    /// </summary>
    public partial class PlayerGear
    {
        /// <summary>One piece as the game dresses a character in it: the look item's parts, material and what it hides.</summary>
        internal sealed class FigurePiece
        {
            public string id { get; set; }
            /// <summary>The slot's key ("head", "melee"): a piece looked at stands in for the worn one of its slot.</summary>
            public string slot { get; set; }
            /// <summary>The item is armor (its own category, whatever it looks like).</summary>
            public bool armor { get; set; }
            /// <summary>The type it is drawn as (a helmet with a hat's look is drawn from the hats).</summary>
            public int type { get; set; }
            /// <summary>The item's own type: the hand or bone a model of its own hangs on.</summary>
            public int slotType { get; set; }
            /// <summary>The look's ItemMaterial by name; null for none.</summary>
            public string mat { get; set; }
            public string male { get; set; }
            public string female { get; set; }
            /// <summary>The look's HeadMask.</summary>
            public int mask { get; set; }
            public string prefab { get; set; }
            public string prefabMale { get; set; }
            public string prefabFemale { get; set; }
        }

        private static readonly JsonSerializerOptions FigureJson = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

        private static bool IsHeadPiece(ItemType type) =>
            type == ItemType.Helmet || type == ItemType.Hat || type == ItemType.Mask || type == ItemType.HeadCovering;

        /// <summary>
        ///     A piece as the game would draw it (ItemController.SetItemAppearance): a transmogrified item is drawn as
        ///     its look, with the look's parts, material and mask; null for what is not drawn on a character at all.
        /// </summary>
        private FigurePiece FigureOf(Entry e)
        {
            if (e.Slot == EquipmentSlot.None || e.Slot == EquipmentSlot.Pet || e.Slot == EquipmentSlot.Boat) return null;
            var item = e.Item;
            var look = e.Stack.TransmogrificationId != null ? ItemService.GetItem(e.Stack.TransmogrificationId.Value) ?? item : item;
            var hasModelIds = !string.IsNullOrEmpty(look.MaleModelId) || !string.IsNullOrEmpty(look.FemaleModelId);
            var hasPrefab = !string.IsNullOrEmpty(look.GenericPrefab) || !string.IsNullOrEmpty(look.MalePrefab) || !string.IsNullOrEmpty(look.FemalePrefab);
            if (!hasModelIds && !hasPrefab) return null;
            return new FigurePiece
            {
                id = e.Stack.Id.ToString(),
                slot = GearRules.Get(e.Slot)?.Key,
                armor = item.Category == ItemCategory.Armor,
                type = (int)(IsHeadPiece(look.Type) && IsHeadPiece(item.Type) ? look.Type : item.Type),
                slotType = (int)item.Type,
                mat = look.Material == ItemMaterial.None ? null : look.Material.ToString(),
                male = Blank(look.MaleModelId),
                female = Blank(look.FemaleModelId),
                mask = (int)look.HeadMask,
                prefab = Blank(look.GenericPrefab),
                prefabMale = Blank(look.MalePrefab),
                prefabFemale = Blank(look.FemalePrefab),
            };
        }

        private static string Blank(string text) => string.IsNullOrEmpty(text) ? null : text;

        /// <summary>A piece for the script, as the text of a data attribute; null for what is not drawn.</summary>
        private string FigureData(Entry e)
        {
            var piece = FigureOf(e);
            return piece == null ? null : JsonSerializer.Serialize(piece, FigureJson);
        }

        /// <summary>Everything worn, the weapon in the hand by what is dressed for, and the helmet and cape as the player set them.</summary>
        private object FigureOutfit()
        {
            var appearance = player.Appearance;
            var style = GearRules.StyleOf(EffectiveSkill);
            var cape = appearance?.Cape ?? -1;
            string capeImage = null;
            if (cape >= 0 && player.Clan != null)
            {
                // a clan member's cape is the clan's: its shape (the role's own when it has one) and its picture
                cape = player.ClanRole?.CapeStyle ?? player.Clan.CapeStyle;
                if (player.Clan.CapeImageId != null) capeImage = "/api/clan/cape/" + player.Clan.Id + "?v=" + player.Clan.CapeImageId.Value.ToString("N");
            }

            return new
            {
                // what shows as worn: the gear that is on, or the outfit that is looked at
                pieces = ShownPieces().Select(FigureOf).Where(x => x != null).ToList(),
                style = style == RavenNest.Combat.AttackStyle.Ranged ? "ranged" : style == RavenNest.Combat.AttackStyle.Magic ? "magic" : style == RavenNest.Combat.AttackStyle.Healing ? "healing" : "melee",
                // the helmet switch hides the head piece that is worn; an outfit looked at with another one on its
                // head shows it, since that is what the player wants to see
                helmet = appearance == null || appearance.HelmetVisible || OtherHeadShown(),
                relaxed,
                cape,
                capeHidden = appearance != null && appearance.CapeHidden,
                capeImage,
            };
        }

        /// <summary>Changes when what the figure should wear changes: the worn pieces with their looks, the style, the toggles.</summary>
        private string FigureKey()
        {
            var appearance = player.Appearance;
            var worn = ShownPieces().Where(x => x.Slot != EquipmentSlot.None)
                .Select(x => x.Stack.ItemId.ToString("N") + ":" + (x.Stack.TransmogrificationId?.ToString("N") ?? ""))
                .OrderBy(x => x, StringComparer.Ordinal);
            return string.Join(",", worn) + "|" + GearRules.StyleOf(EffectiveSkill) + "|" + appearance?.HelmetVisible + "|" + appearance?.CapeHidden + "|" + appearance?.Cape + "|" + relaxed;
        }

        /// <summary>An outfit is looked at and its head piece is not the one worn.</summary>
        private bool OtherHeadShown()
        {
            if (!OutfitView) return false;
            var shown = ShownPieces().FirstOrDefault(x => x.Slot == EquipmentSlot.Head);
            if (shown == null) return false;
            return !worn.TryGetValue(EquipmentSlot.Head, out var on) || on.Stack.Id != shown.Stack.Id;
        }

        // ---- the figure's stance and the two switches ---------------------------------------------------------------

        /// <summary>The figure stands at ease with empty hands, or in the stance of its weapon (each viewer's own choice, kept in the browser).</summary>
        private bool relaxed;
        private bool stanceRead;

        private async Task ReadStanceAsync()
        {
            if (stanceRead || module == null) return;
            stanceRead = true;
            try { relaxed = await module.InvokeAsync<string>("pref", "gear.stance") == "relaxed"; }
            catch (JSException) { }
        }

        private async Task SetRelaxed(bool on)
        {
            relaxed = on;
            if (module != null)
            {
                try { await module.InvokeVoidAsync("setPref", "gear.stance", on ? "relaxed" : "combat"); }
                catch (JSException) { }
            }
            StateHasChanged();
        }

        private bool HelmetShown => player?.Appearance == null || player.Appearance.HelmetVisible;
        private bool CapeShown => player?.Appearance == null || !player.Appearance.CapeHidden;
        private bool HasCape => player?.Appearance != null && (player.Appearance.Cape >= 0 || player.Clan != null);

        private void SetHelmet(bool show)
        {
            if (!CanAct) return;
            if (Take(Server.SetHelmetAndCape(player.Id, show, null), "The helmet could not be changed. Try again in a moment."))
                Say(show ? "Your helmet shows again, in the game too." : "Your helmet is hidden, in the game too.");
        }

        private void SetCape(bool show)
        {
            if (!CanAct) return;
            if (Take(Server.SetHelmetAndCape(player.Id, null, !show), "The cape could not be changed. Try again in a moment."))
                Say(show ? "Your cape shows again, in the game too." : "Your cape is hidden, in the game too.");
        }
    }
}
