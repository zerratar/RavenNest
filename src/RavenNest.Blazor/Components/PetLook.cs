using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using RavenNest.Combat;

namespace RavenNest.Blazor.Components
{
    /// <summary>
    ///     How battle pets look on the website: the icon, name and css class of a role, an element and a strike, the
    ///     icon of a skill, the pictures (with a paw when one is missing) and the chat commands. Shared by the stable
    ///     tab (PetStable) and the guide (/pets), so both say the same thing the same way. Rules stay in
    ///     RavenNest.Combat; nothing here decides anything.
    /// </summary>
    public static class PetLook
    {
        public static readonly PetRole[] Roles = { PetRole.Tank, PetRole.Healer, PetRole.Damage, PetRole.Support, PetRole.Gatherer };
        public static readonly Element[] Elements = { Element.Fire, Element.Ice, Element.Lightning, Element.Poison, Element.Physical };

        public static string RoleName(PetRole r) => r.ToString();

        public static string RoleIcon(PetRole r)
        {
            switch (r)
            {
                case PetRole.Tank: return "fa-solid fa-shield-halved";
                case PetRole.Healer: return "fa-solid fa-heart";
                case PetRole.Damage: return "fa-solid fa-burst";
                case PetRole.Support: return "fa-solid fa-wand-magic-sparkles";
                default: return "fa-solid fa-leaf";
            }
        }

        /// <summary>What a role does, in the guide's own words.</summary>
        public static string RoleText(PetRole r)
        {
            switch (r)
            {
                case PetRole.Tank: return "Takes blows for you.";
                case PetRole.Healer: return "Keeps you standing.";
                case PetRole.Damage: return "Hits hard.";
                case PetRole.Support: return "Makes everyone around better.";
                default: return "Helps you find more while you gather.";
            }
        }

        public static string RoleClass(PetRole r) => "is-role-" + r.ToString().ToLowerInvariant();

        public static string ElementName(Element e) => e == Element.Physical ? "No element" : e.ToString();

        public static string ElementIcon(Element e)
        {
            switch (e)
            {
                case Element.Fire: return "fa-solid fa-fire";
                case Element.Ice: return "fa-solid fa-snowflake";
                case Element.Lightning: return "fa-solid fa-bolt";
                case Element.Poison: return "fa-solid fa-skull-crossbones";
                default: return "fa-regular fa-circle";
            }
        }

        public static string ElementClass(Element e) => "is-el-" + (e == Element.Physical ? "none" : e.ToString().ToLowerInvariant());

        public static string StrikeName(PetStrike s) => s == PetStrike.Bite ? "Bites" : s == PetStrike.Pounce ? "Pounces" : "Fires bolts";

        public static string StrikeIcon(PetStrike s) => s == PetStrike.Bite ? "fa-solid fa-tooth" : s == PetStrike.Pounce ? "fa-solid fa-paw" : "fa-solid fa-meteor";

        private static readonly Dictionary<string, string> skillIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["guard"] = "fa-shield-halved", ["harden"] = "fa-shield", ["bodyslam"] = "fa-burst", ["moo"] = "fa-cow",
            ["lick-wounds"] = "fa-heart", ["soothing-aura"] = "fa-heart-pulse", ["purify"] = "fa-droplet",
            ["pounce"] = "fa-paw", ["frenzy"] = "fa-gauge-high", ["fire-breath"] = "fa-fire", ["frost-breath"] = "fa-snowflake",
            ["venom-breath"] = "fa-smog", ["storm-breath"] = "fa-cloud-bolt", ["venom-sting"] = "fa-skull-crossbones",
            ["howl"] = "fa-bullhorn", ["hex"] = "fa-eye", ["wail"] = "fa-ghost", ["binky"] = "fa-carrot", ["spark"] = "fa-bolt",
            ["quack"] = "fa-volume-high", ["banana-toss"] = "fa-lemon", ["barrage"] = "fa-meteor",
            ["forage"] = "fa-leaf", ["keen-nose"] = "fa-magnifying-glass", ["dig"] = "fa-gem",
        };

        public static string SkillIcon(PetSkill skill) =>
            "fa-solid " + (skill != null && skillIcons.TryGetValue(skill.Id ?? "", out var icon) ? icon : "fa-star");

        /// <summary>The tint of a skill's icon: its element when it has one, else its role.</summary>
        public static string SkillClass(PetSkill skill)
        {
            var element = skill?.Active?.Effects?.Select(e => e.Element).FirstOrDefault(e => e.HasValue && e.Value != Element.Physical);
            return element.HasValue ? ElementClass(element.Value) : RoleClass(skill?.Role ?? PetRole.Damage);
        }

        /// <summary>A skill's numbers without its cooldown (PetsPage.SkillLine has both in one line).</summary>
        public static string SkillEffect(PetSkill skill)
        {
            if (skill == null) return "";
            if (!skill.IsActive || skill.Active.Effects.Any(e => e.Kind == SkillEffectKind.Taunt)) return skill.Description;
            var text = SkillTexts.Active(skill.Active);
            return text.Length > 0 ? Cap(text) : skill.Description;
        }

        /// <summary>The text with a capital first letter ("a heal of 150%" starts a line as "A heal of 150%").</summary>
        public static string Cap(string text) => string.IsNullOrEmpty(text) ? "" : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>Seconds between uses, or 0 for a skill that is always on (the gatherers').</summary>
        public static double Cooldown(PetSkill skill) => skill != null && skill.IsActive ? skill.Active.Cooldown : 0;

        /// <summary>A species whose model is the same at every stage: its picture is shown a little bigger per stage instead.</summary>
        public static bool SameLookEveryStage(PetSpecies s) => s.StageModels == null || s.StageModels.Length <= 1;

        public static string Picture(PetSpecies s, int stage) => RavenNest.Blazor.Pages.Front.PetsPage.Picture(s, stage);

        public static string EggPicture(PetSpecies s) => "/imgs/items/" + (s.EggName ?? "").Replace(" ", "-").ToLowerInvariant() + ".png";

        public static string GemPicture(GemKind kind) => "/imgs/items/" + Gems.RawName(kind).ToLowerInvariant() + ".png";

        public static string StarterCommand(PetSpecies s) => "!pets starter " + s.NameAt(0).Split(' ')[0].ToLowerInvariant();
        public static string TameCommand(PetSpecies s) => "!tame " + s.CreatureName.ToLowerInvariant();
        public static string HatchCommand(PetSpecies s) => "!pets hatch " + (s.EggName ?? "").ToLowerInvariant();

        /// <summary>The chance of a try, as "2%" or "0.2%".</summary>
        public static string Chance(double chance) => (chance * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";

        /// <summary>
        ///     A picture in a fixed box. When the file is missing the box stays and shows a paw, so nothing collapses and
        ///     no broken image shows (wwwroot/css/ravenfall-pets.css, .rf-petpic).
        /// </summary>
        public static RenderFragment Pic(string src, string css = null, string alt = "") => b =>
        {
            b.OpenElement(0, "span");
            b.AddAttribute(1, "class", "rf-petpic" + (string.IsNullOrEmpty(css) ? "" : " " + css));
            b.OpenElement(2, "img");
            b.AddAttribute(3, "src", src);
            b.AddAttribute(4, "alt", alt ?? "");
            b.AddAttribute(5, "loading", "lazy");
            b.AddAttribute(6, "onerror", "this.parentNode.classList.add('is-missing')");
            b.CloseElement();
            b.OpenElement(7, "span");
            b.AddAttribute(8, "class", "rf-petpic__paw");
            b.AddAttribute(9, "aria-hidden", "true");
            b.AddMarkupContent(10, "<i class=\"fa-solid fa-paw\"></i>");
            b.CloseElement();
            b.CloseElement();
        };
    }
}
