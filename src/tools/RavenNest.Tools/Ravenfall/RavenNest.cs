using RavenNest.BusinessLogic;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.Tools.Ravenfall
{
    public static class RavenNest
    {
        public static void ReduceSkillExp(int skillIndex, Skills curSkills, double expToRemove)
        {
            var cs = curSkills.GetSkills();
            var skill = cs[skillIndex];
            var expLeftToRemove = expToRemove;

            while (expLeftToRemove > 0)
            {
                if (skill.Experience >= expLeftToRemove)
                {
                    skill.Experience -= expLeftToRemove;
                    break;
                }
                else if (skill.Experience < expLeftToRemove)
                {
                    var expBefore = skill.Experience;
                    skill.Experience = 0;
                    expLeftToRemove -= expBefore;

                    var lv = skill.Level;
                    var requirement = GameMath.ExperienceForLevel(lv);
                    --expLeftToRemove;
                    skill.Experience = requirement - 1;
                    skill.Level = lv - 1;
                }
            }
        }

        public static Guid[] GetAffectedSkillsIds()
        {
            return new Guid[] {
                Guid.Parse("77FF92F9-3A2D-4176-96EB-3657D759957D"),
                Guid.Parse("4ECF270A-F467-4881-9148-C6F90808FEF5"),
                Guid.Parse("A0CDB83A-2458-42C2-9090-D48799C0FDA2"),
                Guid.Parse("5CC9A049-477D-4C97-99C4-FB39017B8CFB"),
                Guid.Parse("6CCD2E5D-ED44-4F21-8AFE-1915D51E6C8F"),
                Guid.Parse("E0DE0E1B-CD8C-4B6B-8063-91DFFBCF1750"),
                Guid.Parse("D0BEDF58-9623-46FD-BB12-6FA82B195B96"),
                Guid.Parse("A8F952FA-37CF-4DF3-80B9-1EED1E989110"),
                Guid.Parse("EF5ADAD8-BC20-402C-9939-1554BADA802E"),
                Guid.Parse("876DC533-9029-48DD-8F7C-7F8AC193F6EE"),
                Guid.Parse("0D0C3812-2695-4EF3-9033-AC33C6B89253"),
                Guid.Parse("B83525EB-0BB8-420D-99F1-3FC32F5B785B"),
                Guid.Parse("EE02F5C5-C7F3-460E-871A-ACD0444F20F8"),
                Guid.Parse("08ED28D0-CE9B-4FDF-87C7-862C0B35474A"),
                Guid.Parse("224F038D-B394-45EF-A979-80FB0B4DFB7A"),
                Guid.Parse("8FCEF59D-CA0A-4926-90B0-E39D435A16BE"),
                Guid.Parse("BF11C1E4-5948-4502-99C9-CFCF0A354F8F"),
                Guid.Parse("5721528C-C586-4410-BDDF-958530D89EC7"),
                Guid.Parse("7A16F793-7DF1-48F6-887A-482ADF948065"),
                Guid.Parse("3046B8B4-05BB-4DCD-A9A6-F82F52EF90B7"),
                Guid.Parse("02ADE259-F3A6-4AA7-B5C4-98FC64487F85"),
                Guid.Parse("69D11B50-8B8B-4C65-AB66-E05AD0D73377"),
                Guid.Parse("60A1D8DB-8C83-4A6F-BB04-706558CEDDC6"),
                Guid.Parse("6616AA22-27C6-4CE2-BE44-DF75AB6C96CF"),
                Guid.Parse("925577C6-4507-4442-A937-34B918C3615B"),
                Guid.Parse("85BEB8A1-7611-4133-8270-437214652C89"),
                Guid.Parse("BC9A515D-F5A6-432E-8FA6-5B3DD322F50D"),
                Guid.Parse("24580477-4A9B-47AA-B71C-878B88151583"),
                Guid.Parse("F0714FE3-B6FB-4E57-9C55-28668642DF7B"),
                Guid.Parse("7F6E4B12-65A5-4CAF-82EC-8C33E123C22F"),
                Guid.Parse("019CCA17-2359-4481-92C3-6444F9471983")
            };
        }
    }
}
