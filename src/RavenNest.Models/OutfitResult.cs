using System;

namespace RavenNest.Models
{
    public enum OutfitStatus
    {
        Ok = 0,
        /// <summary>The character is not in this session, or is not the caller's.</summary>
        Error = 1,
        /// <summary>No such skill.</summary>
        UnknownSkill = 2,
        /// <summary>The skill has no outfit yet: nothing was worn.</summary>
        NoOutfit = 3,
        /// <summary>The item is not the character's, or cannot be worn in that slot or at its levels.</summary>
        BadItem = 4,
        /// <summary>The pet is not the character's, or is an egg.</summary>
        BadPet = 5,
    }

    /// <summary>
    /// The server's answer to everything about outfits (v0.9.7, the game's docs/outfits-design.md): the character's
    /// outfits as they are now, and what the call changed. The equipment itself reaches the game as inventory
    /// changes, like every other equipment change.
    /// </summary>
    public class OutfitResult
    {
        public OutfitStatus Status { get; set; }

        /// <summary>The character's outfits after the call: <see cref="CharacterOutfits"/>'s text form.</summary>
        public string Outfits { get; set; }

        /// <summary>The skill the call was about (a <see cref="Models.Skill"/>'s number): the outfit that was put on, saved or changed.</summary>
        public int Skill { get; set; } = (int)Models.Skill.None;

        /// <summary>Gear was put on.</summary>
        public bool Changed { get; set; }

        /// <summary>The kinds of item the outfit had that are gone (sold, traded, stashed), by item id.</summary>
        public Guid[] Gone { get; set; }

        /// <summary>What filled each of them, in the same order; an empty id where the character owns nothing for the slot.</summary>
        public Guid[] Filled { get; set; }

        /// <summary>The stable after the outfit's pet was sent out; null when the pet did not change.</summary>
        public BattlePet[] Pets { get; set; }

        public static OutfitResult Fail(OutfitStatus status) => new OutfitResult { Status = status };
    }

    /// <summary>
    /// Sent to the game a character plays in when its outfits were changed somewhere else (the website):
    /// <see cref="GameEventType.OutfitsChanged"/>. Gear that was put on comes as inventory changes.
    /// </summary>
    public class OutfitsChanged
    {
        public Guid CharacterId { get; set; }

        /// <summary><see cref="CharacterOutfits"/>'s text form.</summary>
        public string Outfits { get; set; }

        /// <summary>The stable, when the outfit's pet was sent out; null otherwise.</summary>
        public BattlePet[] Pets { get; set; }
    }
}
