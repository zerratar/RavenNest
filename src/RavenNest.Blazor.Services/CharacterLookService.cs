using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Text.RegularExpressions;

namespace RavenNest.Blazor.Services
{
    /// <summary>
    ///     The character customizer (the Customization tab of a character; wwwroot/js/rf-character-designer.js): a
    ///     character's face, hair and colours. Only the owner (or an administrator) may change them, and only to parts
    ///     the game has; the game is told at once when the character is in a stream.
    /// </summary>
    public class CharacterLookService : RavenNestService
    {
        // what the game's Player prefab has (the game's tools/characters/export-characters-web.cs writes the same parts)
        public const int MaleHeads = 23, FemaleHeads = 22;
        public const int MaleEyebrows = 10, FemaleEyebrows = 7;
        public const int MaleBeards = 18;
        public const int Hairs = 38;

        private static readonly Regex HexColor = new Regex("^#?[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        private readonly GameData gameData;
        private readonly PlayerManager playerManager;

        public CharacterLookService(
            GameData gameData,
            PlayerManager playerManager,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.playerManager = playerManager;
        }

        /// <summary>The character's look as saved, or null when it is not the signed-in player's (nor are they an admin).</summary>
        public SyntyAppearance GetLook(Guid characterId)
        {
            var character = Allowed(characterId, out _);
            if (character == null) return null;
            var a = gameData.GetAppearance(character.SyntyAppearanceId);
            if (a == null) return null;
            return new SyntyAppearance
            {
                Id = a.Id,
                Gender = (Gender)(int)a.Gender,
                Hair = a.Hair,
                Head = a.Head,
                Eyebrows = a.Eyebrows,
                FacialHair = a.FacialHair,
                Cape = a.Cape,
                SkinColor = a.SkinColor,
                HairColor = a.HairColor,
                BeardColor = a.BeardColor,
                EyeColor = a.EyeColor,
                HelmetVisible = a.HelmetVisible,
                StubbleColor = a.StubbleColor,
                WarPaintColor = a.WarPaintColor,
            };
        }

        /// <summary>Saves a look: null when it worked, else why not ("notsignedin", "nocharacter", "invalid").</summary>
        public string SaveLook(Guid characterId, SyntyAppearance look)
        {
            var character = Allowed(characterId, out var error);
            if (character == null) return error;
            if (look == null) return "invalid";
            var current = GetLook(characterId);
            if (current == null) return "nocharacter";

            var female = look.Gender == Gender.Female;
            if (look.Gender != Gender.Male && !female) return "invalid";
            if (!InRange(look.Head, female ? FemaleHeads : MaleHeads, false)) return "invalid";
            if (!InRange(look.Hair, Hairs, true)) return "invalid";
            if (!InRange(look.Eyebrows, female ? FemaleEyebrows : MaleEyebrows, true)) return "invalid";
            if (!female && !InRange(look.FacialHair, MaleBeards, true)) return "invalid";
            var colors = new[] { look.SkinColor, look.HairColor, look.BeardColor, look.EyeColor, look.StubbleColor, look.WarPaintColor };
            foreach (var c in colors) if (c == null || !HexColor.IsMatch(c)) return "invalid";

            // the parts and colours from the page, the rest as it was
            current.Gender = look.Gender;
            current.Head = look.Head;
            current.Hair = look.Hair;
            current.Eyebrows = look.Eyebrows;
            current.FacialHair = female ? -1 : look.FacialHair;
            current.SkinColor = Hex(look.SkinColor);
            current.HairColor = Hex(look.HairColor);
            current.BeardColor = Hex(look.BeardColor);
            current.EyeColor = Hex(look.EyeColor);
            current.StubbleColor = Hex(look.StubbleColor);
            current.WarPaintColor = Hex(look.WarPaintColor);
            return playerManager.UpdateAppearance(characterId, current) ? null : "failed";
        }

        private DataModels.Character Allowed(Guid characterId, out string error)
        {
            var session = GetSession();
            if (session == null || !session.Authenticated)
            {
                error = "notsignedin";
                return null;
            }
            var character = gameData.GetCharacter(characterId);
            if (character == null || (character.UserId != session.UserId && !session.Administrator))
            {
                error = "nocharacter";
                return null;
            }
            error = null;
            return character;
        }

        private static bool InRange(int value, int count, bool canBeNone) => (canBeNone && value == -1) || (value >= 0 && value < count);

        private static string Hex(string c) => (c[0] == '#' ? c : "#" + c).ToUpperInvariant();
    }
}
