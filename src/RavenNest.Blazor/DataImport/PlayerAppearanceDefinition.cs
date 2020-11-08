using RavenNest.BusinessLogic.Game;
using CostumeColor = RavenNest.Models.CostumeColor;
using EyeColor = RavenNest.Models.EyeColor;
using Gender = RavenNest.Models.Gender;
using HairColor = RavenNest.Models.HairColor;
using SkinColor = RavenNest.Models.SkinColor;

namespace RavenNest
{
    public class PlayerAppearanceDefinition
    {
        public PlayerAppearanceDefinition(
            Gender gender,
            SkinColor skinColor,
            HairColor hairColor,
            HairColor browColor,
            HairColor beardColor,
            EyeColor eyeColor,
            CostumeColor costumeColor,
            int baseModelNumber,
            int torsoModelNumber,
            int bottomModelNumber,
            int feetModelNumber,
            int handModelNumber,
            int beltModelNumber,
            int eyesModelNumber,
            int browsModelNumber,
            int mouthModelNumber,
            int maleHairModelNumber,
            int femaleHairModelNumber,
            int beardModelNumber)
        {
            Gender = gender;
            SkinColor = skinColor;
            HairColor = hairColor;
            BrowColor = browColor;
            BeardColor = beardColor;
            EyeColor = eyeColor;
            CostumeColor = costumeColor;
            BaseModelNumber = baseModelNumber;
            TorsoModelNumber = torsoModelNumber;
            BottomModelNumber = bottomModelNumber;
            FeetModelNumber = feetModelNumber;
            HandModelNumber = handModelNumber;
            BeltModelNumber = beltModelNumber;
            EyesModelNumber = eyesModelNumber;
            BrowsModelNumber = browsModelNumber;
            MouthModelNumber = mouthModelNumber;
            MaleHairModelNumber = maleHairModelNumber;
            FemaleHairModelNumber = femaleHairModelNumber;
            BeardModelNumber = beardModelNumber;
        }

        public Gender Gender { get; }
        public SkinColor SkinColor { get; }
        public HairColor HairColor { get; }
        public HairColor BrowColor { get; }
        public HairColor BeardColor { get; }
        public EyeColor EyeColor { get; }
        public CostumeColor CostumeColor { get; }

        public int BaseModelNumber { get; }
        public int TorsoModelNumber { get; }
        public int BottomModelNumber { get; }
        public int FeetModelNumber { get; }
        public int HandModelNumber { get; }
        public int BeltModelNumber { get; }
        public int EyesModelNumber { get; }
        public int BrowsModelNumber { get; }
        public int MouthModelNumber { get; }
        public int MaleHairModelNumber { get; }
        public int FemaleHairModelNumber { get; }
        public int BeardModelNumber { get; }

        public static PlayerAppearanceDefinition Random =>
            new PlayerAppearanceDefinition(
                Utility.Random<Gender>(),
                Utility.Random<SkinColor>(),
                Utility.Random<HairColor>(),
                Utility.Random<HairColor>(),
                Utility.Random<HairColor>(),
                Utility.Random<EyeColor>(),
                Utility.Random<CostumeColor>(),
                Utility.Random(1, 20),
                Utility.Random(1, 7),
                Utility.Random(1, 7),
                1, //Utility.Random(0, 6),
                1, //Utility.Random(0, 4),
                Utility.Random(0, 10),
                Utility.Random(1, 7),
                Utility.Random(1, 15),
                Utility.Random(1, 10),
                Utility.Random(0, 10),
                Utility.Random(0, 20),
                Utility.Random(0, 10));
    }
}
