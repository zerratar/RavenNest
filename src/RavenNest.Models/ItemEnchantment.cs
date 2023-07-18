namespace RavenNest.Models
{
    public class ItemEnchantment
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public AttributeValueType ValueType { get; set; }
        public double Value { get; set; }
    }

    public class ItemStat
    {
        public ItemStat() { }
        public ItemStat(string name, int value, int enchantmentBonus)
        {
            this.Name = name;
            this.Value = value;
            this.Bonus = enchantmentBonus;
        }
        public string Name { get; set; }
        public int Value { get; set; }
        public int Bonus { get; set; }
    }
}
