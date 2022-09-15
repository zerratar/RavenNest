using ChartJs.Blazor.Common.Enums;
using Microsoft.AspNetCore.Components;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Reflection;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Bits;
using System.Security.Cryptography;

namespace RavenNest.Blazor.Components
{
    public partial class SkillDisplay : ComponentBase
    {
        [Parameter]
        public string SkillName { get; set; }
        [Parameter]
        public int? Styling { get; set; } //Optionally set the styling

        private SkillStyling SkillStyled { get; set; }

        private List<SkillStyling> SkillStylingList = new();

        protected override void OnParametersSet()
        {
            SetupStylingList();
            base.OnParametersSetAsync();
        }

        private void SetSkill(string skillName)
        {
            throw new NotImplementedException();
        }

        protected void SetupStylingList()
        {

            switch (Styling)
            {
                case 0:
                default:
                    SetIconListWithValidation("Attack", "fa-sharp fa-solid fa-sword");
                    SetIconListWithValidation("Defense", "fa-sharp fa-solid fa-shield");
                    SetIconListWithValidation("Strength", "fa-sharp fa-solid fa-dumbbell");
                    SetIconListWithValidation("Health", "fa-sharp fa-solid fa-heart");
                    SetIconListWithValidation("Woodcutting", "fa-sharp fa-solid fa-axe");
                    SetIconListWithValidation("Fishing", "fa-sharp fa-solid fa-fishing-rod");
                    SetIconListWithValidation("Mining", "fa-sharp fa-solid fa-hill-rockslide");
                    SetIconListWithValidation("Crafting", "fa-sharp fa-solid fa-hammer-crash");
                    SetIconListWithValidation("Cooking", "fa-sharp fa-solid fa-grill-fire");
                    SetIconListWithValidation("Farming", "fa-sharp fa-solid fa-farm");
                    SetIconListWithValidation("Slayer", "fa-sharp fa-solid fa-swords");
                    SetIconListWithValidation("Magic", "fa-sharp fa-solid fa-wand-sparkles");
                    SetIconListWithValidation("Ranged", "fa-sharp fa-solid fa-bow-arrow");
                    SetIconListWithValidation("Sailing", "fa-sharp fa-solid fa-sailboat");
                    SetIconListWithValidation("Healing", "fa-sharp fa-solid fa-hand-holding-medical");
                    break;
            }

        }

        protected void SetIconListWithValidation(string skillName, string stylingClass)
        {
            SetIconListWithValidation(skillName, stylingClass, null);
        }

        //Validate that I'm setting a valid skill or throw a exception
        protected void SetIconListWithValidation(string skillName, string stylingClass, string skillImgLocation)
        {
            //feel free to add any other validaton, such as checking Image exisits, etc
            var query = RavenNest.DataModels.Skills.SkillNames.FirstOrDefault(x => x == skillName);
            if (query != null)
            {
                var AddingSkillStyling = new SkillStyling(skillName, stylingClass, skillImgLocation);
                SkillStylingList.Add(AddingSkillStyling);
                if (SkillName.Equals(skillName, StringComparison.OrdinalIgnoreCase))
                    SkillStyled = AddingSkillStyling;
            }
            else
            {
                throw new Exception("Unknown skill for styling: " + skillName); //a bit aggressive..oops, have fun Zerratar
            }
        }

        protected class SkillStyling
        {
            public SkillStyling(string skillName, string stylingClass, string skillImgLocation)
            {
                SkillName = skillName;
                StylingClass = stylingClass;
                SkillImgLocation = skillImgLocation;
            }

            public string SkillName { get; set; }
            private string _stylingClass;
            public string StylingClass { get { return _stylingClass ?? ""; } set => _stylingClass = value; }
            public string SkillImgLocation { get; set; }
            public string SkillImgSource
            {
                get
                {
                    var fullStyleClass = StylingClass is null ? "" : "_" + StylingClass;
                    return SkillImgLocation is not null ? SkillImgLocation + "/" + SkillName + fullStyleClass + ".png" : null;
                }
            }
            public string SkillImgStylingClass
            {
                get
                {
                    return SkillName + "-" + StylingClass is null ? "icon" : StylingClass;
                }
            }
        }
    }


}
