namespace RavenNest.BusinessLogic.Extended
{
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
