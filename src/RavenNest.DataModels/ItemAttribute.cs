using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class ItemAttribute : Entity<ItemAttribute>
    {
        [PersistentData] private string name;
        [PersistentData] private string description;
        [PersistentData] private int attributeIndex;
        [PersistentData] private int type;
        [PersistentData] private string maxValue;
        [PersistentData] private string minValue;
        [PersistentData] private string defaultValue;
    }
}
