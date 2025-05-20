using RavenNest.DataAnnotations;
using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class SyntyAppearance : Entity<SyntyAppearance>
    {
        [PersistentData] private Gender gender;
        [PersistentData] private int hair;
        [PersistentData] private int head;
        [PersistentData] private int eyebrows;
        [PersistentData] private int facialHair;
        [PersistentData] private int cape;
        [PersistentData] private string skinColor;
        [PersistentData] private string hairColor;
        [PersistentData] private string beardColor;
        [PersistentData] private string eyeColor;
        [PersistentData] private bool helmetVisible;
        [PersistentData] private string stubbleColor;
        [PersistentData] private string warPaintColor;
    }
}
