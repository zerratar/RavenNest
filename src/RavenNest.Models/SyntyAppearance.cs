using System;

namespace RavenNest.Models
{
    public class SyntyAppearance
    {
        /// <summary>
        /// The jaw shadow every character had before stubble could be coloured: the fixed scar colour of the game's
        /// character material, which is what Synty's face art paints the stubble with.
        /// </summary>
        public const string DefaultStubbleColor = "#6C5148";

        public SyntyAppearance() { }
        public Guid Id { get; set; }
        public Gender Gender { get; set; }
        public int Hair { get; set; }
        public int Head { get; set; }
        public int Eyebrows { get; set; }
        public int FacialHair { get; set; }
        public int Cape { get; set; }
        public string SkinColor { get; set; }
        public string HairColor { get; set; }
        public string BeardColor { get; set; }
        public string EyeColor { get; set; }
        public bool HelmetVisible { get; set; }
        /// <summary>The shadow of a shave along the jaw (and over the lip on some faces).</summary>
        public string StubbleColor { get; set; }

        /// <summary>The scars on the faces drawn with them. Null from servers older than v0.9.6.</summary>
        public string ScarColor { get; set; }
        public string WarPaintColor { get; set; }
    }
}
