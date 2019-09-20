using System;

namespace RavenNest.Models
{

    public class Appearance
    {
        public Guid Id { get; set; }

        public Gender Gender { get; set; }
        public SkinColor SkinColor { get; set; }
        public HairColor HairColor { get; set; }
        public HairColor BrowColor { get; set; }
        public HairColor BeardColor { get; set; }
        public EyeColor EyeColor { get; set; }
        public CostumeColor CostumeColor { get; set; }

        public int BaseModelNumber { get; set; }
        public int TorsoModelNumber { get; set; }
        public int BottomModelNumber { get; set; }
        public int FeetModelNumber { get; set; }
        public int HandModelNumber { get; set; }

        public int BeltModelNumber { get; set; }
        public int EyesModelNumber { get; set; }
        public int BrowsModelNumber { get; set; }
        public int MouthModelNumber { get; set; }
        public int MaleHairModelNumber { get; set; }
        public int FemaleHairModelNumber { get; set; }
        public int BeardModelNumber { get; set; }

        public ItemMaterial TorsoMaterial { get; set; }
        public ItemMaterial BottomMaterial { get; set; }
        public ItemMaterial FeetMaterial { get; set; }
        public ItemMaterial HandMaterial { get; set; }
        public bool HelmetVisible { get; set; }
        public int Revision { get; set; }
    }
}