namespace RavenNest.Models
{
    public enum GameEventType
    {
        Raid = 0,
        WarRaid = 1,
        PlayerAdd = 2,
        PlayerRemove = 3,
        PlayerAppearance = 4,
        PlayerTask = 5,
        ItemBuy = 6,
        ItemSell = 7,
        ItemEquip = 8,
        ItemUnEquip = 9,
        ItemAdd = 10,
        ItemRemove = 34,

        PlayerNameUpdate = 16,
        PlayerExpUpdate = 17,
        PlayerJoinRaid = 18,
        PlayerJoinDungeon = 19,
        PlayerJoinArena = 20,
        PlayerMove = 21,
        PlayerAttack = 22,
        PlayerAction = 23,
        PlayerDuelAccept = 24,
        PlayerDuelDecline = 25,
        PlayerDuelRequest = 26,
        PlayerTravel = 27,


        ResourceUpdate = 11,
        ServerMessage = 12,
        SessionSettingsChanged = 13,
        VillageLevelUp = 14,
        VillageInfo = 15,
        ExpMultiplier = 28,
        ClanLevelChanged = 29,
        ClanSkillLevelChanged = 30,
        ServerTime = 31,
        PlayerRestedUpdate = 32,
        PubSubToken = 33,
        // Next = 35, since ItemRemove = 34

        StreamerPvP = 35,
        PlayerBeginRest = 36,
        PlayerEndRest = 37,

        PlayerStartRaid = 38,
        PlayerStartDungeon = 39,

        GameUpdated = 40,
        Unstuck = 41,
        Teleport = 42,

        ItemRemoveByCategory = 43,

        /// <summary>
        ///     Used for syncing one or more inventory items.
        /// </summary>
        ItemSync = 44,

        /// <summary>
        ///     Server requesting a certain log file to be sent from the client.
        /// </summary>
        PlayerLogRequest = 45,

        /// <summary>
        ///     Server requesting the current player state to be sent from the client.  
        /// </summary>
        GameStateRequest = 46,

        /// <summary>
        ///     The stream's tavern shelves changed (a listing made, edited, bought from, taken down or
        ///     expired). Carries a <see cref="StreamerShopInfo"/>.
        /// </summary>
        StreamerShopUpdated = 47,

        /// <summary>
        ///     A clan's logo, cape image or cape shapes changed. Carries a <see cref="ClanLook"/>; sent to
        ///     every game with a member of the clan in it.
        /// </summary>
        ClanLookChanged = 48,

        /// <summary>A character finished a quest: what it was and what it gave (QuestCompleted).</summary>
        QuestCompleted = 49,

        /// <summary>The stream's quest ended: done (with its rewards), out of time or stopped (StreamQuestEnded).</summary>
        StreamQuestEnded = 50,

        /// <summary>A character reached an achievement's tier and earned a title (AchievementReached).</summary>
        AchievementReached = 51,

        /// <summary>A viewer finished a streamer's bounty and got its reward (StreamerQuestClaimed).</summary>
        StreamerQuestClaimed = 52,

        /// <summary>A streamer's quest ended: a goal done (with what each helper got), out of time, stopped, or every bounty claimed (StreamerQuestEnded).</summary>
        StreamerQuestEnded = 53,

        /// <summary>A step of the streamer's story line moved: something found, or a step done (VillageStoryProgress).</summary>
        VillageStoryProgress = 54,

        /// <summary>A streamer's story line was finished, with its unlocks and the helpers' shares (VillageStoryCompleted).</summary>
        VillageStoryCompleted = 55,
    }
}
