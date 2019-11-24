using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class MiningTaskProcessor : ResourceTaskProcessor
    {
        public override void Handle(IGameData gameData, GameSession session, Character character, DataModels.CharacterState state)
        {
            UpdateResourceGain(gameData, session, character, resources =>
            {
                var skills = gameData.GetSkills(character.SkillsId);
                var miningLevel = GameMath.ExperienceToLevel(skills.Mining);
                var chance = Random.NextDouble();
                var gotNugget = false;

                var runeNuggetDropChance = RuneNuggetDropChance + ((miningLevel - 70) * DropChanceIncrement);
                var addyNugetDropChance = AdamantiteNuggetDropChance + ((miningLevel - 50) * DropChanceIncrement);
                var mithrilNuggetDropChance = MithrilNuggetDropChance + ((miningLevel - 30) * DropChanceIncrement);
                var steelNuggetDropChance = SteelNuggetDropChance + ((miningLevel - 10) * DropChanceIncrement);
                var ironNuggetDropChancef = IronNuggetDropChance + miningLevel * DropChanceIncrement;

                if (miningLevel >= 70 && chance <= runeNuggetDropChance)
                {
                    IncrementItemStack(gameData, session, character, RuneNuggetId);
                    gotNugget = true;
                    // chance for rune 2F
                }
                if (miningLevel >= 50 && !gotNugget && chance <= addyNugetDropChance)
                {
                    IncrementItemStack(gameData, session, character, AdamantiteNuggetId);
                    gotNugget = true;
                    // chance for adamantite nugget
                }
                if (miningLevel >= 30 && !gotNugget && chance <= mithrilNuggetDropChance)
                {
                    IncrementItemStack(gameData, session, character, MithrilNuggetId);
                    gotNugget = true;
                    // chance for mithril  nugget
                }
                if (miningLevel >= 10 && !gotNugget && chance <= steelNuggetDropChance)
                {
                    IncrementItemStack(gameData, session, character, SteelNuggetId);
                    gotNugget = true;
                    // chance for steel
                }
                if (!gotNugget && chance <= ironNuggetDropChancef)
                {
                    IncrementItemStack(gameData, session, character, IronNuggetId);
                }

                ++resources.Ore;
            });
        }
    }
}
