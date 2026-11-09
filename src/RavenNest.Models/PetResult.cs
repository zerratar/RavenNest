namespace RavenNest.Models
{
    /// <summary>What a battle pet command did (docs/battle-pets-design.md in the game repo).</summary>
    public enum PetResultStatus
    {
        Ok,
        Error,
        NoSuchPet,
        NoSuchSpecies,
        TamingTooLow,
        StableFull,
        TooSoon,
        NoSuchItem,
        NotAPetItem,
        AlreadyAwake,
        CannotEvolve,
        NoSuchSkill,
        NotLearned,
        NoSuchSlot,
        BadName,
        ItemMissing,
    }

    public class PetResult
    {
        public PetResultStatus Status { get; set; }

        /// <summary>The pet the command was about (tamed, woken, sent out, renamed...).</summary>
        public BattlePet Pet { get; set; }

        /// <summary>The whole stable, after the change.</summary>
        public BattlePet[] Pets { get; set; }

        /// <summary>What is missing (evolving), or the level needed (taming), in words.</summary>
        public string Missing { get; set; }

        /// <summary>Levels the pet gained (experience).</summary>
        public int LevelsGained { get; set; }

        /// <summary>How many pets the stable holds (Taming, and the clan's Rookery).</summary>
        public int StableSize { get; set; }

        public static PetResult Fail(PetResultStatus status, string missing = null) => new PetResult { Status = status, Missing = missing };
    }
}
