namespace RavenNest.DataModels
{
    public enum StatusEffectType : int
    {
        None = 0, // always 0
        /// <summary>
        /// Heals the player instantly
        /// </summary>
        Heal,
        /// <summary>
        /// Heals the player over time
        /// </summary>
        HealOverTime,
        /// <summary>
        /// Increases the player's strength which effects melee damage
        /// </summary>
        IncreasedStrength,
        /// <summary>
        /// Increases the player's defense
        /// </summary>
        IncreasedDefense,
        /// <summary>
        /// Increases the player's dodge rate
        /// </summary>
        IncreasedDodge,
        /// <summary>
        /// Increases the player's accuracy
        /// </summary>
        IncreasedHitChance,
        /// <summary>
        /// Increase the player's movement speed
        /// </summary>
        IncreasedMovementSpeed,
        /// <summary>
        /// Increase the attack speed of both melee and ranged attacks
        /// </summary>
        IncreasedAttackSpeed, // Melee And Ranged
        /// <summary>
        /// Increase the casting speed for magic and healing
        /// </summary>
        IncreasedCastSpeed, // Magic and Healing
        /// <summary>
        /// Increases the player's melee attack power
        /// </summary>
        IncreasedAttackPower,
        /// <summary>
        /// Inccreases the player's ranged power
        /// </summary>
        IncreasedRangedPower,
        /// <summary>
        /// Increases the player's magic power
        /// </summary>
        IncreasedMagicPower,
        /// <summary>
        /// Increases the player's healing power
        /// </summary>
        IncreasedHealingPower,
        /// <summary>
        /// Increases the player's exp gain
        /// </summary>
        IncreasedExperienceGain,
        /// <summary>
        /// Increases the player's critical hit chance
        /// </summary>
        IncreaseCriticalHit,
        /// <summary>
        /// Applies poison attributes on all attacks, causing damage over time
        /// </summary>
        AttackAttributePoison,
        /// <summary>
        /// Applies bleeding attribute on all attacks, causing damage over time
        /// </summary>
        AttackAttributeBleeding,
        /// <summary>
        /// Applies burning attribute on all attacks, causing damage over time
        /// </summary>
        AttackAttributeBurning,
        /// <summary>
        /// Applies health steal attribute on all attacks, stealing health from the target
        /// </summary>
        AttackAttributeHealthSteal,
        /// <summary>
        /// Applies poison on the player, causing damage over time
        /// </summary>
        Poison,
        /// <summary>
        /// Applies bleeding on the player, causing damage over time
        /// </summary>
        Bleeding,
        /// <summary>
        /// Applies burning on the player, causing damage over time
        /// </summary>
        Burning,
        /// <summary>
        /// Takes a one time damage
        /// </summary>
        Damage,
        /// <summary>
        /// Applies a reduction in hit chance
        /// </summary>
        ReducedHitChance,
        /// <summary>
        /// Applies a slow effect on the player, reducing movement speed
        /// </summary>
        ReducedMovementSpeed,
        /// <summary>
        /// Applies a slow effect on the player, reducing attack speed for both melee and ranged attacks
        /// </summary>
        ReducedAttackSpeed,
        /// <summary>
        /// Applies a slow effect on the player, reducing magic and healing casting speed
        /// </summary>
        ReducedCastSpeed,
        /// <summary>
        /// Removes an item from the player's inventory
        /// </summary>
        RemoveItem = 800,
        /// <summary>
        /// Adds an item to the player's inventory
        /// </summary>
        AddItem = 900,
        /// <summary>
        ///     Teleports the player to a specified island
        /// </summary>
        TeleportToIsland = 999, // always 999
    }
}
