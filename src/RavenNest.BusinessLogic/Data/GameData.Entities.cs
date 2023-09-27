using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.Extensions.Logging;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Telepathy;

namespace RavenNest.BusinessLogic.Data
{
    public partial class GameData
    {
        private void EnsureMagicAttributes()
        {

            var attributes = this.itemAttributes.Entities;

            for (var i = 0; i < DataModels.Skills.SkillNames.Length; ++i)
            {
                var sn = DataModels.Skills.SkillNames[i];
                var existing = attributes.FirstOrDefault(x => x.Name == sn.ToUpper());
                if (existing != null)
                {
                    continue;
                }

                Add(new ItemAttribute
                {
                    Id = Guid.NewGuid(),
                    Description = "Increases " + sn + " by 25%",
                    Name = sn.ToUpper(),
                    AttributeIndex = i,
                    DefaultValue = "5%",
                    MaxValue = "25%",
                    MinValue = "1%",
                    Type = 1
                });
            }

            if (attributes.FirstOrDefault(x => x.Name == "AIM") == null)
            {
                Add(new ItemAttribute
                {
                    Id = Guid.NewGuid(),
                    Description = "Increases Aim by 20%",
                    Name = "AIM",
                    AttributeIndex = DataModels.Skills.SkillNames.Length + 1,
                    DefaultValue = "5%",
                    MaxValue = "20%",
                    MinValue = "1%",
                    Type = 1
                });
            }

            if (attributes.FirstOrDefault(x => x.Name == "POWER") == null)
            {
                Add(new ItemAttribute
                {
                    Id = Guid.NewGuid(),
                    Description = "Increases Power by 20%",
                    Name = "POWER",
                    AttributeIndex = DataModels.Skills.SkillNames.Length + 2,
                    DefaultValue = "5%",
                    MaxValue = "20%",
                    MinValue = "1%",
                    Type = 1
                });
            }

            if (attributes.FirstOrDefault(x => x.Name == "ARMOR") == null)
            {
                Add(new ItemAttribute
                {
                    Id = Guid.NewGuid(),
                    Description = "Increases Armor by 20%",
                    Name = "ARMOR",
                    AttributeIndex = DataModels.Skills.SkillNames.Length + 3,
                    DefaultValue = "5%",
                    MaxValue = "20%",
                    MinValue = "1%",
                    Type = 1
                });
            }
        }

        public TypedItems GetKnownItems()
        {
            var i = this.items.Entities;
            if (typedItems == null)
            {
                obsoleteItems = new ObsoleteItems
                {
                    IronNugget = GetOrCreateItem(i, "Iron Nugget", ItemCategory.Resource, ItemType.Mining),
                    SteelNugget = GetOrCreateItem(i, "Steel Nugget", ItemCategory.Resource, ItemType.Mining),
                    MithrilNugget = GetOrCreateItem(i, "Mithril Nugget", ItemCategory.Resource, ItemType.Mining),
                    AdamantiteNugget = GetOrCreateItem(i, "Adamantite Nugget", ItemCategory.Resource, ItemType.Mining),
                    RuneNugget = GetOrCreateItem(i, "Rune Nugget", ItemCategory.Resource, ItemType.Mining),
                    DragonScale = GetOrCreateItem(i, "Dragon Scale", ItemCategory.Resource, ItemType.Mining),
                    Lionite = GetOrCreateItem(i, "Lionite", ItemCategory.Resource, ItemType.Mining),
                    Ethereum = GetOrCreateItem(i, "Ethereum", ItemCategory.Resource, ItemType.Mining),
                    PhantomCore = GetOrCreateItem(i, "Phantom Core", ItemCategory.Resource, ItemType.Mining),
                    AbraxasSpirit = GetOrCreateItem(i, "Abraxas Spirit", ItemCategory.Resource, ItemType.Mining),
                    AncientHeart = GetOrCreateItem(i, "Ancient Heart", ItemCategory.Resource, ItemType.Mining),
                    AtlarusLight = GetOrCreateItem(i, "Atlarus Light", ItemCategory.Resource, ItemType.Mining),

                    OreIngot = GetOrCreateItem(i, "Ore ingot", ItemCategory.Resource, ItemType.Mining),
                    WoodPlank = GetOrCreateItem(i, "Wood Plank", ItemCategory.Resource, ItemType.Woodcutting),
                };

                ItemSet GetOrCreateItemSet(string typeName, int levelRequirement = 0)
                {
                    return new ItemSet
                    {
                        Boots = GetOrCreateItem(i, typeName + " Boots", ItemCategory.Armor, ItemType.Boots).LevelRequirement(levelRequirement),
                        Gloves = GetOrCreateItem(i, typeName + " Gloves", ItemCategory.Armor, ItemType.Gloves).LevelRequirement(levelRequirement),
                        Helmet = GetOrCreateItem(i, typeName + " Helmet", ItemCategory.Armor, ItemType.Helmet).LevelRequirement(levelRequirement),
                        Leggings = GetOrCreateItem(i, typeName + " Leggings", ItemCategory.Armor, ItemType.Leggings).LevelRequirement(levelRequirement),
                        Chest = GetOrCreateItem(i, typeName + " Chest", ItemCategory.Armor, ItemType.Chest).LevelRequirement(levelRequirement),
                        Shield = GetOrCreateItem(i, typeName + " Shield", ItemCategory.Armor, ItemType.Shield).GenericPrefab("Character/Weapons/Shields/" + typeName + " Shield", false).LevelRequirement(levelRequirement),
                        Sword = GetOrCreateItem(i, typeName + " Sword", ItemCategory.Weapon, ItemType.OneHandedSword).GenericPrefab("Character/Weapons/Swords/" + typeName + " Sword").LevelRequirement(levelRequirement),
                        Axe = GetOrCreateItem(i, typeName + " Axe", ItemCategory.Weapon, ItemType.OneHandedAxe).GenericPrefab("Character/Weapons/Axes/" + typeName + " Axe").LevelRequirement(levelRequirement),
                        Bow = GetOrCreateItem(i, typeName + " Bow", ItemCategory.Weapon, ItemType.TwoHandedBow).LevelRequirement(levelRequirement),
                        Spear = GetOrCreateItem(i, typeName + " Spear", ItemCategory.Weapon, ItemType.TwoHandedSpear, typeName + " 2H Spear").GenericPrefab("Character/Weapons/Spears/" + typeName + " Spear", false).LevelRequirement(levelRequirement),
                        Staff = GetOrCreateItem(i, typeName + " Staff", ItemCategory.Weapon, ItemType.TwoHandedStaff, typeName + " 2H Staff").GenericPrefab("Character/Weapons/Staffs/" + typeName + " Staff", false).LevelRequirement(levelRequirement),
                        TwoHandedAxe = GetOrCreateItem(i, typeName + " 2H Axe", ItemCategory.Weapon, ItemType.TwoHandedAxe).GenericPrefab("Character/Weapons/Axes/" + typeName + " 2H Axe").LevelRequirement(levelRequirement),
                        TwoHandedSword = GetOrCreateItem(i, typeName + " 2H Sword", ItemCategory.Weapon, ItemType.TwoHandedSword).GenericPrefab("Character/Weapons/Swords/" + typeName + " 2H Sword").LevelRequirement(levelRequirement),
                        Katana = GetOrCreateItem(i, typeName + " Katana", ItemCategory.Weapon, ItemType.TwoHandedSword).GenericPrefab("Character/Weapons/Swords/" + typeName + " Katana", false).LevelRequirement(levelRequirement),
                    };
                }

                // ensure we have these items in the database
                typedItems = new TypedItems
                {
                    // Pets
                    Pets = GetOrCreatePets(i),

                    // Item Sets
                    Bronze = GetOrCreateItemSet("Bronze", 1),
                    Iron = GetOrCreateItemSet("Iron", 1),
                    Steel = GetOrCreateItemSet("Steel", 10),
                    Black = GetOrCreateItemSet("Black", 20),
                    Mithril = GetOrCreateItemSet("Mithril", 30),
                    Adamantite = GetOrCreateItemSet("Adamantite", 50),
                    Rune = GetOrCreateItemSet("Rune", 70),
                    Dragon = GetOrCreateItemSet("Dragon", 90),
                    Abraxas = GetOrCreateItemSet("Abraxas", 120),
                    Phantom = GetOrCreateItemSet("Phantom", 150),
                    Lionsbane = GetOrCreateItemSet("Lionsbane", 200),
                    Ether = GetOrCreateItemSet("Ether", 280),
                    Ancient = GetOrCreateItemSet("Ancient", 340),
                    Atlarus = GetOrCreateItemSet("Atlarus", 400),

                    //ElderBronze = GetOrCreateItemSet("Elder Bronze"),
                    //ElderIron = GetOrCreateItemSet("Elder Iron"),
                    //ElderSteel = GetOrCreateItemSet("Elder Steel"),
                    //ElderMithril = GetOrCreateItemSet("Elder Mithril"),
                    //ElderAdamantite = GetOrCreateItemSet("Elder Adamantite"),
                    //ElderRune = GetOrCreateItemSet("Elder Rune"),
                    //ElderDragon = GetOrCreateItemSet("Elder Dragon"),
                    //ElderAbraxas = GetOrCreateItemSet("Elder Abraxas"),
                    //ElderPhantom = GetOrCreateItemSet("Elder Phantom"),
                    //ElderEther = GetOrCreateItemSet("Elder Ether"),
                    //ElderLionite = GetOrCreateItemSet("Elder Lionsbane"),
                    //ElderAncient = GetOrCreateItemSet("Elder Ancient"),
                    //ElderAtlarus = GetOrCreateItemSet("Elder Atlarus"),


                    GoldRing = GetOrCreateItem(i, "Gold Ring", ItemCategory.Ring, ItemType.Ring),
                    EmeraldRing = GetOrCreateItem(i, "Emerald Ring", ItemCategory.Ring, ItemType.Ring),
                    RubyRing = GetOrCreateItem(i, "Ruby Ring", ItemCategory.Ring, ItemType.Ring),
                    DragonRing = GetOrCreateItem(i, "Dragon Ring", ItemCategory.Ring, ItemType.Ring),
                    PhantomRing = GetOrCreateItem(i, "Phantom Ring", ItemCategory.Ring, ItemType.Ring),
                    GoldAmulet = GetOrCreateItem(i, "Gold Amulet", ItemCategory.Amulet, ItemType.Amulet),
                    EmeraldAmulet = GetOrCreateItem(i, "Emerald Amulet", ItemCategory.Amulet, ItemType.Amulet),
                    RubyAmulet = GetOrCreateItem(i, "Ruby Amulet", ItemCategory.Amulet, ItemType.Amulet),
                    DragonAmulet = GetOrCreateItem(i, "Dragon Amulet", ItemCategory.Amulet, ItemType.Amulet),
                    PhantomAmulet = GetOrCreateItem(i, "Phantom Amulet", ItemCategory.Amulet, ItemType.Amulet),

                    // Item Drops
                    Hearthstone = GetOrCreateItem(i, "Hearthstone", "A magically infused stone.", ItemCategory.Resource, ItemType.Alchemy),
                    WanderersGem = GetOrCreateItem(i, "Wanderer's Gem", "A gem that has the essence of distant lands.", ItemCategory.Resource, ItemType.Alchemy),
                    IronEmblem = GetOrCreateItem(i, "Iron Emblem", "A signet representing Ironhill", ItemCategory.Resource, ItemType.Alchemy),
                    KyoCrystal = GetOrCreateItem(i, "Kyo Crystal", "A radiant crystal resonating with Kyo's energy", ItemCategory.Resource, ItemType.Alchemy),
                    HeimRune = GetOrCreateItem(i, "Heim Rune", "A rune infused with Heim's magic", ItemCategory.Resource, ItemType.Alchemy),
                    AtriasFeather = GetOrCreateItem(i, "Atria's Feather", "A magical feather tied to Atria", ItemCategory.Resource, ItemType.Alchemy),
                    EldarasMark = GetOrCreateItem(i, "Eldara's Mark", "A seal bearing Eldara's mark", ItemCategory.Resource, ItemType.Alchemy),
                    Realmstone = GetOrCreateItem(i, "Realmstone", "A precious stone allowing teleportation across islands.", ItemCategory.Resource, ItemType.Alchemy),

                    SantaHat = GetOrCreateItem(i, "Santa Hat", "A festive hat worn by Santa.", ItemCategory.Armor, ItemType.Helmet),

                    ExpMultiplierScroll = GetOrCreateItem(i, "Exp Multiplier Scroll", "A scroll that increases the global experience multiplier by 1 and extends the timer with 15 minutes.", ItemCategory.Scroll, ItemType.Scroll),
                    DungeonScroll = GetOrCreateItem(i, "Dungeon Scroll", "A scroll that allows you to instantanously start and enter a dungeon.", ItemCategory.Scroll, ItemType.Scroll),
                    RaidScroll = GetOrCreateItem(i, "Raid Scroll", "A scroll that allows you to instantanously start and enter a raid.", ItemCategory.Scroll, ItemType.Scroll),

                    // accessories
                    ArchersRing = GetOrCreateItem(i, "Archers Ring", ItemCategory.Ring, ItemType.Ring),
                    ArchersRingII = GetOrCreateItem(i, "Archers Ring II", ItemCategory.Ring, ItemType.Ring),
                    ArchersRingIII = GetOrCreateItem(i, "Archers Ring III", ItemCategory.Ring, ItemType.Ring),
                    ArchersRingIV = GetOrCreateItem(i, "Archers Ring IV", ItemCategory.Ring, ItemType.Ring),
                    MagesRing = GetOrCreateItem(i, "Mages Ring", ItemCategory.Ring, ItemType.Ring),
                    MagesRingII = GetOrCreateItem(i, "Mages Ring II", ItemCategory.Ring, ItemType.Ring),
                    MagesRingIII = GetOrCreateItem(i, "Mages Ring III", ItemCategory.Ring, ItemType.Ring),
                    MagesRingIV = GetOrCreateItem(i, "Mages Ring IV", ItemCategory.Ring, ItemType.Ring),
                    ArchmagesPendant = GetOrCreateItem(i, "Archmages Pendant", ItemCategory.Amulet, ItemType.Amulet),
                    KnightsEmblem = GetOrCreateItem(i, "Knights Emblem", ItemCategory.Amulet, ItemType.Amulet),
                    OwlsEyeRing = GetOrCreateItem(i, "Owls Eye Ring", ItemCategory.Ring, ItemType.Ring),
                    RingOfTheCelestial = GetOrCreateItem(i, "Ring Of The Celestial", ItemCategory.Ring, ItemType.Ring),
                    WarriorsMightRing = GetOrCreateItem(i, "Warriors Might Ring", ItemCategory.Ring, ItemType.Ring),
                    WindcallersAmulet = GetOrCreateItem(i, "Windcallers Amulet", ItemCategory.Amulet, ItemType.Amulet),

                    // Tokens
                    ChristmasToken = GetOrCreateItem(i, "Christmas Token", ItemCategory.Scroll, ItemType.Scroll),
                    HalloweenToken = GetOrCreateItem(i, "Halloween Token", ItemCategory.Scroll, ItemType.Scroll),
                    AbraxasToken = GetOrCreateItem(i, "Abraxas Token", ItemCategory.Scroll, ItemType.Scroll),
                    RuneToken = GetOrCreateItem(i, "Rune Token", ItemCategory.Scroll, ItemType.Scroll),

                    // Gathering - Cooking
                    Water = GetOrCreateItem(i, "Water", "Essential for life and a key ingredient in many recipes.", ItemCategory.Resource, ItemType.Gathering),
                    Mushroom = GetOrCreateItem(i, "Mushroom", "A versatile fungus that adds flavor to dishes.", ItemCategory.Resource, ItemType.Gathering),
                    Salt = GetOrCreateItem(i, "Salt", "A mineral that enhances the taste of food.", ItemCategory.Resource, ItemType.Gathering),
                    Yeast = GetOrCreateItem(i, "Yeast", "A key ingredient for bread-making. It feeds on sugars and causes dough to rise, providing bread's fluffy texture.", ItemCategory.Resource, ItemType.Farming),
                    BlackPepper = GetOrCreateItem(i, "Black Pepper", "A spicy seasoning that adds kick to dishes.", ItemCategory.Resource, ItemType.Gathering),

                    // Gathering - Alchemy
                    Sand = GetOrCreateItem(i, "Sand", "Granular material that can be melted to create vials.", ItemCategory.Resource, ItemType.Gathering),
                    Hemp = GetOrCreateItem(i, "Hemp", "A fibrous plant material, used for creating strings.", ItemCategory.Resource, ItemType.Gathering),
                    Resin = GetOrCreateItem(i, "Resin", "A sticky substance, often combined with wood pulp to make paper.", ItemCategory.Resource, ItemType.Gathering),

                    Yarrow = GetOrCreateItem(i, "Yarrow", "A herb with healing properties", ItemCategory.Resource, ItemType.Gathering), // used for health potion
                    RedClover = GetOrCreateItem(i, "Red Clover", "The Red Clover is said to have great healing properties.", ItemCategory.Resource, ItemType.Gathering), // used for great health potion
                    Comfrey = GetOrCreateItem(i, "Comfrey", "A herb used for creating regenerative potions", ItemCategory.Resource, ItemType.Gathering), // used for regen potion (regenerate hp over time)
                    Sage = GetOrCreateItem(i, "Sage", "A herb that hardens the skin when properly consumed.", ItemCategory.Resource, ItemType.Gathering), // used for defense potion
                    Mugwort = GetOrCreateItem(i, "Mugwort", "A herb with great protective properties.", ItemCategory.Resource, ItemType.Gathering), // used for great defense potion
                    Lavender = GetOrCreateItem(i, "Lavender", "A herb that can increase the strength of the user", ItemCategory.Resource, ItemType.Gathering), // used for strength potion
                    Goldenrod = GetOrCreateItem(i, "Goldenrod", "A herb known to bolster the consumer's physical might.", ItemCategory.Resource, ItemType.Gathering),
                    Elderflower = GetOrCreateItem(i, "Elderflower", "A fragrant herb enhancing magical capabilities.", ItemCategory.Resource, ItemType.Gathering),
                    Wormwood = GetOrCreateItem(i, "Wormwood", "A bitter herb that amplifies magical energies.", ItemCategory.Resource, ItemType.Gathering),
                    Valerian = GetOrCreateItem(i, "Valerian", "A calming herb that steadies the archer's aim.", ItemCategory.Resource, ItemType.Gathering),
                    Skullcap = GetOrCreateItem(i, "Skullcap", "A herb that sharpens focus, benefiting ranged prowess.", ItemCategory.Resource, ItemType.Gathering),
                    Chamomile = GetOrCreateItem(i, "Chamomile", "A soothing herb that augments healing energies.", ItemCategory.Resource, ItemType.Gathering),
                    LemonBalm = GetOrCreateItem(i, "Lemon Balm", "A fragrant herb, known to enhance healing magics.", ItemCategory.Resource, ItemType.Gathering),

                    // Farming - Cooking
                    Cabbage = GetOrCreateItem(i, "Cabbage", "A green leafy vegetable with layers of overlapping leaves. Commonly used in salads and various dishes.", ItemCategory.Resource, ItemType.Farming),
                    Eggs = GetOrCreateItem(i, "Eggs", "An essential ingredient in many culinary dishes.", ItemCategory.Resource, ItemType.Farming),
                    Milk = GetOrCreateItem(i, "Milk", "A creamy liquid, used in cooking and baking.", ItemCategory.Resource, ItemType.Farming),
                    RawChicken = GetOrCreateItem(i, "Raw Chicken", "Uncooked poultry, ready for the frying pan.", ItemCategory.Resource, ItemType.Farming),
                    RawChickenLeg = GetOrCreateItem(i, "Raw Chicken Leg", "Uncooked poultry leg, ready to be fried.", ItemCategory.Resource, ItemType.Farming),
                    RawBeef = GetOrCreateItem(i, "Raw Meat", "Uncooked beef, a staple in many dishes.", ItemCategory.Resource, ItemType.Farming),
                    RawPork = GetOrCreateItem(i, "Raw Pork", "Uncooked pork, waiting to be seasoned and cooked.", ItemCategory.Resource, ItemType.Farming),
                    Wheat = GetOrCreateItem(i, "Wheat", "Golden grains, the base for many baked goods.", ItemCategory.Resource, ItemType.Farming),
                    Tomato = GetOrCreateItem(i, "Tomato", "A juicy fruit, essential in salads and sauces.", ItemCategory.Resource, ItemType.Farming),
                    Potato = GetOrCreateItem(i, "Potato", "A starchy vegetable, versatile in cooking.", ItemCategory.Resource, ItemType.Farming),
                    Apple = GetOrCreateItem(i, "Apple", "A sweet and crunchy fruit.", ItemCategory.Resource, ItemType.Farming),
                    Carrots = GetOrCreateItem(i, "Carrots", "A crunchy vegetable, rich in vitamins.", ItemCategory.Resource, ItemType.Farming),
                    Garlic = GetOrCreateItem(i, "Garlic", "A pungent ingredient, adding depth to dishes.", ItemCategory.Resource, ItemType.Farming),
                    Cumin = GetOrCreateItem(i, "Cumin", "A spicy seed, adding warmth to dishes.", ItemCategory.Resource, ItemType.Farming),
                    Coriander = GetOrCreateItem(i, "Coriander", "A fragrant herb, brightening up meals.", ItemCategory.Resource, ItemType.Farming),
                    Paprika = GetOrCreateItem(i, "Paprika", "A vibrant spice, adding color and flavor.", ItemCategory.Resource, ItemType.Farming),
                    Turmeric = GetOrCreateItem(i, "Turmeric", "A golden spice, known for its earthy flavor.", ItemCategory.Resource, ItemType.Farming),
                    Onion = GetOrCreateItem(i, "Onion", "A flavorful bulb, adding zest to meals.", ItemCategory.Resource, ItemType.Farming),
                    Grapes = GetOrCreateItem(i, "Grapes", "Sweet fruits, enjoyed fresh or as wine.", ItemCategory.Resource, ItemType.Farming),
                    Truffle = GetOrCreateItem(i, "Truffle", "A rare fungus, cherished in gourmet dishes.", ItemCategory.Resource, ItemType.Farming),

                    // Farming - Alchemy
                    //LunarBlossom = GetOrCreateItem(i, "Lunar Blossom", "A flower with gentle curative properties.", ItemCategory.Resource, ItemType.Farming),
                    //SolarBloom = GetOrCreateItem(i, "Solar Bloom", "A sun-loving flower that amplifies potion effects.", ItemCategory.Resource, ItemType.Farming),
                    //Thornleaf = GetOrCreateItem(i, "Thornleaf", "A prickly plant that enhances offensive capabilities.", ItemCategory.Resource, ItemType.Farming),
                    //GuardianFern = GetOrCreateItem(i, "Guardian Fern", "A sturdy plant that strengthens defenses.", ItemCategory.Resource, ItemType.Farming),
                    //Windroot = GetOrCreateItem(i, "Windroot", "A tuber that can enhance movement and reflexes.", ItemCategory.Resource, ItemType.Farming),
                    //Starflower = GetOrCreateItem(i, "Starflower", "A radiant flower that only blooms under starlit nights, rumored to hold cosmic power.", ItemCategory.Resource, ItemType.Farming), // lv 850

                    // Alchemy - Ingredients
                    String = GetOrCreateItem(i, "String", "A sturdy string, often used in crafting and alchemy.", ItemCategory.Resource, ItemType.Alchemy),
                    Paper = GetOrCreateItem(i, "Paper", "A thin sheet made from pressed plant fibers. Perfect for recording knowledge.", ItemCategory.Resource, ItemType.Alchemy),
                    Vial = GetOrCreateItem(i, "Vial", "A small container made of glass. Ideal for storing various concoctions.", ItemCategory.Resource, ItemType.Alchemy),
                    WoodPulp = GetOrCreateItem(i, "Wood Pulp", "Mashed wood fibers. Can be processed to make paper.", ItemCategory.Resource, ItemType.Alchemy), // used for creating paper

                    // Alchemy - Produced items                    
                    TomeOfHome = GetOrCreateItem(i, "Tome of Home", "A magical tome that allows the user to teleport to Home island.", ItemCategory.Potion, ItemType.Potion),
                    TomeOfAway = GetOrCreateItem(i, "Tome of Away", "A magical tome that allows the user to teleport to Away island.", ItemCategory.Potion, ItemType.Potion),
                    TomeOfIronhill = GetOrCreateItem(i, "Tome of Ironhill", "A magical tome that allows the user to teleport to Ironhill.", ItemCategory.Potion, ItemType.Potion),
                    TomeOfKyo = GetOrCreateItem(i, "Tome of Kyo", "A magical tome that allows the user to teleport to Kyo.", ItemCategory.Potion, ItemType.Potion),
                    TomeOfHeim = GetOrCreateItem(i, "Tome of Heim", "A magical tome that allows the user to teleport to Heim.", ItemCategory.Potion, ItemType.Potion),
                    TomeOfAtria = GetOrCreateItem(i, "Tome of Atria", "A magical tome that allows the user to teleport to Atria.", ItemCategory.Potion, ItemType.Potion),
                    TomeOfEldara = GetOrCreateItem(i, "Tome of Eldara", "A magical tome that allows the user to teleport to Eldara.", ItemCategory.Potion, ItemType.Potion),
                    TomeOfTeleportation = GetOrCreateItem(i, "Tome of Teleportation", "A magical tome that allows the user to teleport to a chosen island.", ItemCategory.Potion, ItemType.Potion),

                    HealthPotion = GetOrCreateItem(i, "Health Potion", "Restores a small portion of health instantly.", ItemCategory.Potion, ItemType.Potion),
                    GreatHealthPotion = GetOrCreateItem(i, "Great Health Potion", "Restores a large portion of health instantly.", ItemCategory.Potion, ItemType.Potion),
                    RegenPotion = GetOrCreateItem(i, "Regen Potion", "Gradually restores health over a short duration.", ItemCategory.Potion, ItemType.Potion),
                    DefensePotion = GetOrCreateItem(i, "Defense Potion", "Boosts defense, reducing damage taken for a limited time.", ItemCategory.Potion, ItemType.Potion),
                    GreatDefensePotion = GetOrCreateItem(i, "Great Defense Potion", "Significantly boosts defense, greatly reducing damage taken for an extended period.", ItemCategory.Potion, ItemType.Potion),
                    StrengthPotion = GetOrCreateItem(i, "Strength Potion", "Increases physical power for a limited duration.", ItemCategory.Potion, ItemType.Potion),
                    GreatStrengthPotion = GetOrCreateItem(i, "Great Strength Potion", "Greatly increases physical power for an extended period.", ItemCategory.Potion, ItemType.Potion),
                    MagicPotion = GetOrCreateItem(i, "Magic Potion", "Amplifies magical abilities for a short span.", ItemCategory.Potion, ItemType.Potion),
                    GreatMagicPotion = GetOrCreateItem(i, "Great Magic Potion", "Significantly amplifies magical abilities for a longer duration.", ItemCategory.Potion, ItemType.Potion),
                    RangedPotion = GetOrCreateItem(i, "Ranged Potion", "Enhances ranged accuracy and power for a short time.", ItemCategory.Potion, ItemType.Potion),
                    GreatRangedPotion = GetOrCreateItem(i, "Great Ranged Potion", "Significantly enhances ranged accuracy and power for an extended period.", ItemCategory.Potion, ItemType.Potion),
                    HealingPotion = GetOrCreateItem(i, "Healing Potion", "Boosts the effectiveness of healing spells and abilities for a limited time.", ItemCategory.Potion, ItemType.Potion),
                    GreatHealingPotion = GetOrCreateItem(i, "Great Healing Potion", "Significantly boosts the effectiveness of healing spells and abilities for an extended duration.", ItemCategory.Potion, ItemType.Potion),

                    // Woodcutting
                    Logs = GetOrCreateItem(i, "Logs", "Basic timber, perfect for simple crafting needs.", ItemCategory.Resource, ItemType.Woodcutting),
                    BristleLogs = GetOrCreateItem(i, "Bristle Logs", "Rough and rugged logs, ideal for durable crafts.", ItemCategory.Resource, ItemType.Woodcutting),
                    GlowbarkLogs = GetOrCreateItem(i, "Glowbark Logs", "Luminescent logs that emit a soft glow. Used in mystical crafts.", ItemCategory.Resource, ItemType.Woodcutting),
                    MystwoodLogs = GetOrCreateItem(i, "Mystwood Logs", "Enchanted timber known to be favored by wizards.", ItemCategory.Resource, ItemType.Woodcutting),
                    SandriftLogs = GetOrCreateItem(i, "Sandrift Logs", "Logs that carry the essence of deserts and dunes.", ItemCategory.Resource, ItemType.Woodcutting),
                    PineheartLogs = GetOrCreateItem(i, "Pineheart Logs", "Aromatic timber, it resonates with nature's spirit.", ItemCategory.Resource, ItemType.Woodcutting),
                    EbonshadeLogs = GetOrCreateItem(i, "Ebonshade Logs", "Dark and dense logs, sought after for their strength.", ItemCategory.Resource, ItemType.Woodcutting),
                    IronbarkLogs = GetOrCreateItem(i, "Ironbark Logs", "Sturdy logs with a metallic sheen. Nearly as hard as iron.", ItemCategory.Resource, ItemType.Woodcutting),
                    FrostbiteLogs = GetOrCreateItem(i, "Frostbite Logs", "Cold to the touch, these logs are found in the iciest regions.", ItemCategory.Resource, ItemType.Woodcutting),
                    DragonwoodLogs = GetOrCreateItem(i, "Dragonwood Logs", "Rare logs infused with the fiery essence of dragons.", ItemCategory.Resource, ItemType.Woodcutting),
                    GoldwillowLogs = GetOrCreateItem(i, "Goldwillow Logs", "Golden logs known to bring good fortune.", ItemCategory.Resource, ItemType.Woodcutting),
                    ShadowoakLogs = GetOrCreateItem(i, "Shadowoak Logs", "Ancient timber that holds the secrets of the shadows.", ItemCategory.Resource, ItemType.Woodcutting),

                    // Fishing
                    RawSprat = GetOrCreateItem(i, "Raw Sprat", "A tiny, silver fish. Great for a quick snack.", ItemCategory.Resource, ItemType.Fishing),
                    RawShrimp = GetOrCreateItem(i, "Raw Shrimp", "Small and pinkish, they're a popular catch.", ItemCategory.Resource, ItemType.Fishing),
                    RawRedSeaBass = GetOrCreateItem(i, "Raw Red Sea Bass", "A vibrant fish with a strong flavor.", ItemCategory.Resource, ItemType.Fishing),
                    RawBass = GetOrCreateItem(i, "Raw Bass", "A popular freshwater fish with a mild taste.", ItemCategory.Resource, ItemType.Fishing),
                    RawPerch = GetOrCreateItem(i, "Raw Perch", "Striped and feisty, a common catch in many lakes.", ItemCategory.Resource, ItemType.Fishing),
                    RawSalmon = GetOrCreateItem(i, "Raw Salmon", "A strong swimmer known for its rich, pink flesh.", ItemCategory.Resource, ItemType.Fishing),
                    RawCrab = GetOrCreateItem(i, "Raw Crab", "A crustacean with sharp pincers. Tasty when cooked.", ItemCategory.Resource, ItemType.Fishing),
                    RawLobster = GetOrCreateItem(i, "Raw Lobster", "A sea delicacy with a tough shell but tender meat.", ItemCategory.Resource, ItemType.Fishing),
                    RawBlueLobster = GetOrCreateItem(i, "Raw Blue Lobster", "A rare and vibrant variation of lobster.", ItemCategory.Resource, ItemType.Fishing),
                    RawSwordfish = GetOrCreateItem(i, "Raw Swordfish", "A powerful fish known for its elongated bill.", ItemCategory.Resource, ItemType.Fishing),
                    RawPufferFish = GetOrCreateItem(i, "Raw Puffer Fish", "Inflates when threatened. Handle with care!", ItemCategory.Resource, ItemType.Fishing),
                    RawOctopus = GetOrCreateItem(i, "Raw Octopus", "Eight-armed creature of the deep. Slippery and smart.", ItemCategory.Resource, ItemType.Fishing),
                    RawMantaRay = GetOrCreateItem(i, "Raw Manta Ray", "Graceful glider of the ocean. Beware its tail.", ItemCategory.Resource, ItemType.Fishing),
                    RawKraken = GetOrCreateItem(i, "Raw Kraken", "Mythical sea monster known to pull ships under.", ItemCategory.Resource, ItemType.Fishing),
                    RawLeviathan = GetOrCreateItem(i, "Raw Leviathan", "A gargantuan sea creature spoken of in legends.", ItemCategory.Resource, ItemType.Fishing),
                    RawPoseidonsGuardian = GetOrCreateItem(i, "Raw Poseidon's Guardian", "Said to be the protector of Poseidon's realm. A rare and majestic catch.", ItemCategory.Resource, ItemType.Fishing),

                    // Cooking - Resource Creation
                    Flour = GetOrCreateItem(i, "Flour", "Ground wheat, essential for baking and cooking.", ItemCategory.Resource, ItemType.Cooking),
                    Sugar = GetOrCreateItem(i, "Sugar", "A sweet crystalline substance often used in baking and cooking to enhance flavors.", ItemCategory.Resource, ItemType.Cooking),
                    Cinnamon = GetOrCreateItem(i, "Cinnamon", "A fragrant spice obtained from the inner bark of certain trees. Adds a warm and aromatic flavor.", ItemCategory.Resource, ItemType.Cooking),
                    Butter = GetOrCreateItem(i, "Butter", "Creamy and rich, perfect for adding flavor.", ItemCategory.Resource, ItemType.Cooking),
                    Cheese = GetOrCreateItem(i, "Cheese", "Aged to perfection, adding depth to dishes.", ItemCategory.Resource, ItemType.Cooking),
                    SpiceMix = GetOrCreateItem(i, "Spice Mix", "A blend of spices, ready to kick up the heat.", ItemCategory.Resource, ItemType.Cooking),
                    Ham = GetOrCreateItem(i, "Ham", "Salty and savory, cured to perfection.", ItemCategory.Resource, ItemType.Cooking),
                    Cacao = GetOrCreateItem(i, "Cacao", "The base of all chocolate delights.", ItemCategory.Resource, ItemType.Cooking),
                    Chocolate = GetOrCreateItem(i, "Chocolate", "Melted and molded bliss.", ItemCategory.Resource, ItemType.Cooking),
                    GoldenLeaf = GetOrCreateItem(i, "Golden Leaf", "An exquisite ingredient for elite dishes.", ItemCategory.Resource, ItemType.Cooking),

                    // Cooking - Edibles and not so edible..
                    RedWine = GetOrCreateItem(i, "Red Wine", "Aged gracefully, pairs well with hearty dishes.", ItemCategory.Food, ItemType.Potion),
                    RoastedChicken = GetOrCreateItem(i, "Roasted Chicken", "Roasted to a golden brown. Juicy and flavorful.", ItemCategory.Food, ItemType.Food),
                    RoastedPork = GetOrCreateItem(i, "Roasted Pork", "Tender and savory, perfect with applesauce.", ItemCategory.Food, ItemType.Food),
                    RoastBeef = GetOrCreateItem(i, "Roast Beef", "Grilled with care, boasting a robust flavor.", ItemCategory.Food, ItemType.Food),
                    CookedChickenLeg = GetOrCreateItem(i, "Cooked Chicken Leg", "Crisp on the outside, tender within.", ItemCategory.Food, ItemType.Food),
                    ChocolateChipCookies = GetOrCreateItem(i, "Chocolate Chip Cookies", "Sweet bites filled with gooey chocolate.", ItemCategory.Food, ItemType.Food),
                    ApplePie = GetOrCreateItem(i, "Apple Pie", "A warm slice of home. Flaky crust with sweet filling.", ItemCategory.Food, ItemType.Food),
                    Bread = GetOrCreateItem(i, "Bread", "Baked daily. Crunchy crust with soft center.", ItemCategory.Food, ItemType.Food),
                    HamSandwich = GetOrCreateItem(i, "Ham Sandwich", "Layered with ham and fresh vegetables.", ItemCategory.Food, ItemType.Food),
                    Skewers = GetOrCreateItem(i, "Skewers", "Grilled delights on a stick.", ItemCategory.Food, ItemType.Food),
                    Steak = GetOrCreateItem(i, "Steak", "Succulent and perfectly seared.", ItemCategory.Food, ItemType.Food),

                    GrilledCheese = GetOrCreateItem(i, "Grilled Cheese", "A classic comfort dish, this grilled cheese sandwich oozes with melted cheese and is complemented by slices of savory ham. The bread, crisped to perfection with a buttery exterior, offers a delightful crunch with every bite.", ItemCategory.Food, ItemType.Food, "Grilled Ham and Cheese Sandwich"),

                    // Cooking - Fish
                    Sprat = GetOrCreateItem(i, "Sprat", "Lightly fried with a golden crust.", ItemCategory.Food, ItemType.Food),
                    Shrimp = GetOrCreateItem(i, "Shrimp", "Turned a delicate pink, succulent and flavorful.", ItemCategory.Food, ItemType.Food),
                    RedSeaBass = GetOrCreateItem(i, "Red Sea Bass", "Grilled to perfection, highlighting its natural flavors.", ItemCategory.Food, ItemType.Food),
                    Bass = GetOrCreateItem(i, "Bass", "Flaky and tender, with a slight hint of the sea.", ItemCategory.Food, ItemType.Food),
                    Perch = GetOrCreateItem(i, "Perch", "Seared lightly, maintaining its juicy core.", ItemCategory.Food, ItemType.Food),
                    Salmon = GetOrCreateItem(i, "Salmon", "Rich in omega-3, cooked to enhance its natural richness.", ItemCategory.Food, ItemType.Food),
                    Crab = GetOrCreateItem(i, "Crab", "Steamed to bring out the sweetness in its flesh.", ItemCategory.Food, ItemType.Food),
                    Lobster = GetOrCreateItem(i, "Lobster", "Red shell on the outside, tender meat on the inside.", ItemCategory.Food, ItemType.Food),
                    BlueLobster = GetOrCreateItem(i, "Blue Lobster", "A delicacy that combines visual appeal with taste.", ItemCategory.Food, ItemType.Food),
                    Swordfish = GetOrCreateItem(i, "Swordfish", "Thick steaks grilled to seal in the moisture.", ItemCategory.Food, ItemType.Food),
                    PufferFish = GetOrCreateItem(i, "Puffer Fish", "Skillfully prepared to ensure every bite is safe and delectable.", ItemCategory.Food, ItemType.Food),
                    Octopus = GetOrCreateItem(i, "Octopus", "Tenderized to perfection, a dish of exquisite taste.", ItemCategory.Food, ItemType.Food),
                    MantaRay = GetOrCreateItem(i, "Manta Ray", "Unique and flavorful, a treat from the deep.", ItemCategory.Food, ItemType.Food),
                    Kraken = GetOrCreateItem(i, "Kraken", "Legends speak of its taste, as vast as its tales.", ItemCategory.Food, ItemType.Food),

                    LeviathansRoyalStew = GetOrCreateItem(i, "Leviathan's Royal Stew", "This is a hearty stew that combines the tender meat of the Leviathan with a variety of other ingredients to create a flavorful dish worthy of its namesake.", ItemCategory.Food, ItemType.Food),
                    PoseidonsGuardianFeast = GetOrCreateItem(i, "Poseidon's Guardian Feast", "A luxurious dish that showcases the divine nature of Poseidon's Guardian. It involves a series of preparations that results in a meal fit for a deity.", ItemCategory.Food, ItemType.Food),

                    // Failed attempts. The disgusting versions, should not be consumed

                    BurnedGrilledCheese = GetOrCreateItem(i, "Burned Grilled Cheese", "A blackened remnant of what was once a delicious sandwich. The cheese has hardened and the bread is charred beyond recognition, emitting a bitter, burnt aroma. A testament to culinary distractions.", ItemCategory.Food, ItemType.Food),

                    MuddledLeviathanBroth = GetOrCreateItem(i, "Muddled Leviathan Broth", "What was meant to be a rich broth is now a cloudy mess.", ItemCategory.Food, ItemType.Food),
                    RuinedGuardianDelight = GetOrCreateItem(i, "Ruined Guardian Delight", "An attempt at luxury, but now a dish of despair.", ItemCategory.Food, ItemType.Food),

                    BurnedChicken = GetOrCreateItem(i, "Burned Chicken", "Charred to a crisp, its original flavors lost in the blackened exterior.", ItemCategory.Food, ItemType.Food),
                    BurnedPork = GetOrCreateItem(i, "Burned Pork", "From succulence to ashes, a somber reminder of culinary misjudgment.", ItemCategory.Food, ItemType.Food),
                    BurnedBeef = GetOrCreateItem(i, "Burned Beef", "Overcooked to the point of losing its juiciness; a dry shadow of its former self.", ItemCategory.Food, ItemType.Food),
                    BurnedChickenLeg = GetOrCreateItem(i, "Burned Chicken Leg", "Its once tender meat now concealed under a layer of char.", ItemCategory.Food, ItemType.Food),
                    BurnedChocolateChipCookies = GetOrCreateItem(i, "Burned Chocolate Chip Cookies", "Bitter remnants of a once-sweet treat. A melancholic crunch awaits.", ItemCategory.Food, ItemType.Food),
                    BurnedApplePie = GetOrCreateItem(i, "Burned Apple Pie", "Its inviting filling cruelly trapped beneath a scorched crust.", ItemCategory.Food, ItemType.Food),
                    BurnedBread = GetOrCreateItem(i, "Burned Bread", "Once soft and airy, now hard and darkened. A poignant contrast.", ItemCategory.Food, ItemType.Food),
                    BurnedSkewers = GetOrCreateItem(i, "Burned Skewers", "Overexposed to the flames, leaving only the charred remains of a meal.", ItemCategory.Food, ItemType.Food),
                    BurnedSteak = GetOrCreateItem(i, "Burned Steak", "Far from medium-rare; a grim testament to overzealous grilling.", ItemCategory.Food, ItemType.Food),
                    BurnedSprat = GetOrCreateItem(i, "Burned Sprat", "Tiny fish reduced to brittle, overcooked morsels.", ItemCategory.Food, ItemType.Food),
                    BurnedShrimp = GetOrCreateItem(i, "Burned Shrimp", "Its delightful pink hue replaced by an ominous black.", ItemCategory.Food, ItemType.Food),
                    BurnedRedSeaBass = GetOrCreateItem(i, "Burned Red Sea Bass", "Its delicate flavors now overshadowed by the bitterness of overcooking.", ItemCategory.Food, ItemType.Food),
                    BurnedBass = GetOrCreateItem(i, "Burned Bass", "From the depths of the sea to the abyss of overcooking.", ItemCategory.Food, ItemType.Food),
                    BurnedPerch = GetOrCreateItem(i, "Burned Perch", "The searing went a step too far, robbing it of its natural taste.", ItemCategory.Food, ItemType.Food),
                    BurnedSalmon = GetOrCreateItem(i, "Burned Salmon", "Its rich, pink tones now hidden beneath a layer of char.", ItemCategory.Food, ItemType.Food),
                    BurnedCrab = GetOrCreateItem(i, "Burned Crab", "Its sweet meat now tainted by the bitterness of the flames.", ItemCategory.Food, ItemType.Food),
                    BurnedLobster = GetOrCreateItem(i, "Burned Lobster", "A gourmet's nightmare: From a vibrant red to a sorrowful black.", ItemCategory.Food, ItemType.Food),
                    BurnedBlueLobster = GetOrCreateItem(i, "Burned Blue Lobster", "Its striking hue tragically concealed by overcooking.", ItemCategory.Food, ItemType.Food),
                    BurnedSwordfish = GetOrCreateItem(i, "Burned Swordfish", "Its majestic nature tarnished by an unforgiving flame.", ItemCategory.Food, ItemType.Food),
                    BurnedPufferFish = GetOrCreateItem(i, "Burned Puffer Fish", "Not just dangerous, but now also distressingly overcooked.", ItemCategory.Food, ItemType.Food),
                    BurnedOctopus = GetOrCreateItem(i, "Burned Octopus", "Its tentacles crisped up, a far cry from its potential tenderness.", ItemCategory.Food, ItemType.Food),
                    BurnedMantaRay = GetOrCreateItem(i, "Burned Manta Ray", "From the grace of the oceans to the tragedy of the grill.", ItemCategory.Food, ItemType.Food),
                    BurnedKraken = GetOrCreateItem(i, "Burned Kraken", "Such a legendary creature deserved a better culinary fate.", ItemCategory.Food, ItemType.Food),

                    // Mining
                    Emerald = GetOrCreateItem(i, "Emerald", "A captivating green gemstone known for its vibrant hue and clarity.", ItemCategory.Resource, ItemType.Mining),
                    Ruby = GetOrCreateItem(i, "Ruby", "A deep red gem that glows passionately under light, often seen as a symbol of love.", ItemCategory.Resource, ItemType.Mining),
                    Sapphire = GetOrCreateItem(i, "Sapphire", "A mesmerizing blue gem, emblematic of the vastness and depth of the oceans.", ItemCategory.Resource, ItemType.Mining),
                    Topaz = GetOrCreateItem(i, "Topaz", "A translucent gem varying from yellow to blue, representing both the warmth of the sun and the coolness of the sky.", ItemCategory.Resource, ItemType.Mining),
                    PhilosophersStone = GetOrCreateItem(i, "Philosopher's Stone", "An elusive stone surrounded by myths, said to have the power to turn any metal into gold.", ItemCategory.Resource, ItemType.Mining),

                    CopperOre = GetOrCreateItem(i, "Copper Ore", "A reddish-brown mineral used for creating bronze.", ItemCategory.Resource, ItemType.Mining),
                    TinOre = GetOrCreateItem(i, "Tin Ore", "Soft and malleable, this metal is combined with copper ore to produce bronze.", ItemCategory.Resource, ItemType.Mining),
                    IronOre = GetOrCreateItem(i, "Iron Ore", "A commonly found metal ore.", ItemCategory.Resource, ItemType.Mining),
                    Coal = GetOrCreateItem(i, "Coal", "Dark and dusty, it's a primary source of energy for various industries.", ItemCategory.Resource, ItemType.Mining),
                    Silver = GetOrCreateItem(i, "Silver", "A shiny and ductile metal, often associated with luxury and wealth.", ItemCategory.Resource, ItemType.Mining),
                    Gold = GetOrCreateItem(i, "Gold Nugget", "A small chunk of this precious metal known for its distinct shine and value.", ItemCategory.Resource, ItemType.Mining),
                    MithrilOre = GetOrCreateItem(i, "Mithril Ore", "A rare, silver-like metal known for its strength and lightweight properties. Found deep within mountain cores.", ItemCategory.Resource, ItemType.Mining),
                    AdamantiteOre = GetOrCreateItem(i, "Adamantite Ore", "A green-tinted metal, famed for its nearly impenetrable nature.", ItemCategory.Resource, ItemType.Mining),
                    RuneOre = GetOrCreateItem(i, "Rune Ore", "A mystical ore infused with ancient magics. Often seen glowing with a soft blue hue.", ItemCategory.Resource, ItemType.Mining),
                    DragonOre = GetOrCreateItem(i, "Dragon Ore", "An incredibly rare ore that is said to contain the power of dragons. Radiates a faint warmth.", ItemCategory.Resource, ItemType.Mining),
                    AbraxasOre = GetOrCreateItem(i, "Abraxas Ore", "A deep purple ore that seems to constantly shift and shimmer, as if holding a universe within.", ItemCategory.Resource, ItemType.Mining),
                    PhantomOre = GetOrCreateItem(i, "Phantom Ore", "Almost translucent, this ore appears and disappears with one's gaze. Elusive and otherworldly.", ItemCategory.Resource, ItemType.Mining),
                    LioniteOre = GetOrCreateItem(i, "Lionite Ore", "Golden and radiant, this ore roars with the strength and majesty of a lion.", ItemCategory.Resource, ItemType.Mining),
                    EthereumOre = GetOrCreateItem(i, "Ethereum Ore", "This dark purple ore exudes a mysterious energy, as if holding secrets from other realms.", ItemCategory.Resource, ItemType.Mining),
                    AncientOre = GetOrCreateItem(i, "Ancient Ore", "An ore from times long past, it holds memories of the world before.", ItemCategory.Resource, ItemType.Mining),
                    AtlarusOre = GetOrCreateItem(i, "Atlarus Ore", "A radiant gemstone-like ore, named after a legendary sky deity.", ItemCategory.Resource, ItemType.Mining),
                    Eldrium = GetOrCreateItem(i, "Eldrium", "A crystal that pulses with a deep and ancient power, like the heartbeat of the earth itself.", ItemCategory.Resource, ItemType.Mining),

                    // Crafting
                    BronzeBar = GetOrCreateItem(i, "Bronze Bar", "A solid bar of bronze, crafted by combining copper and tin. Often used for beginner crafts.", ItemCategory.Resource, ItemType.Crafting),
                    IronBar = GetOrCreateItem(i, "Iron Bar", "This hardy bar of iron is often used as the backbone of many strong tools and weapons.", ItemCategory.Resource, ItemType.Crafting),
                    SteelBar = GetOrCreateItem(i, "Steel Bar", "A bar of steel, known for its superior strength compared to iron. A common choice for advanced weaponry.", ItemCategory.Resource, ItemType.Crafting),
                    MithrilBar = GetOrCreateItem(i, "Mithril Bar", "A shimmering bar of mithril, lightweight and durable. Favored in the crafting of elven artifacts.", ItemCategory.Resource, ItemType.Crafting),
                    AdamantiteBar = GetOrCreateItem(i, "Adamantite Bar", "This vibrant green bar holds incredible resistance. Often sought after for the toughest of armors.", ItemCategory.Resource, ItemType.Crafting),
                    RuneBar = GetOrCreateItem(i, "Rune Bar", "Engraved with ancient symbols, this bar possesses magical properties that make it invaluable in enchanting.", ItemCategory.Resource, ItemType.Crafting),
                    DragonBar = GetOrCreateItem(i, "Dragon Bar", "Forged in dragonfire, this bar has a fiery red glow and is said to contain the spirit of dragons.", ItemCategory.Resource, ItemType.Crafting),
                    AbraxasBar = GetOrCreateItem(i, "Abraxas Bar", "Gleaming with a celestial shine, this bar is believed to be blessed by the gods.", ItemCategory.Resource, ItemType.Crafting),
                    PhantomBar = GetOrCreateItem(i, "Phantom Bar", "This ethereal bar seems to phase in and out of existence, perfect for crafting ghostly tools and armor.", ItemCategory.Resource, ItemType.Crafting),
                    LioniteBar = GetOrCreateItem(i, "Lionite Bar", "Golden and majestic, this bar is as fierce and noble as the creature it's named after.", ItemCategory.Resource, ItemType.Crafting),
                    EthereumBar = GetOrCreateItem(i, "Ethereum Bar", "Refined from its dark purple ore, this bar seems to hum with otherworldly energy.", ItemCategory.Resource, ItemType.Crafting),
                    AncientBar = GetOrCreateItem(i, "Ancient Bar", "A relic from a bygone age, this bar holds the wisdom and power of ancient civilizations.", ItemCategory.Resource, ItemType.Crafting),
                    AtlarusBar = GetOrCreateItem(i, "Atlarus Bar", "Radiating with a bright, golden shine, this bar is imbued with the essence of sunlit realms. Said to be the material of the gods.", ItemCategory.Resource, ItemType.Crafting),

                    ElderBronzeBar = GetOrCreateItem(i, "Elder Bronze Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderIronBar = GetOrCreateItem(i, "Elder Iron Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderSteelBar = GetOrCreateItem(i, "Elder Steel Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderMithrilBar = GetOrCreateItem(i, "Elder Mithril Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderAdamantiteBar = GetOrCreateItem(i, "Elder Adamantite Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderRuneBar = GetOrCreateItem(i, "Elder Rune Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderDragonBar = GetOrCreateItem(i, "Elder Dragon Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderAbraxasBar = GetOrCreateItem(i, "Elder Abraxas Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderPhantomBar = GetOrCreateItem(i, "Elder Phantom Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderLioniteBar = GetOrCreateItem(i, "Elder Lionite Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderEthereumBar = GetOrCreateItem(i, "Elder Ethereum Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderAncientBar = GetOrCreateItem(i, "Elder Ancient Bar", ItemCategory.Resource, ItemType.Crafting),
                    ElderAtlarusBar = GetOrCreateItem(i, "Elder Atlarus Bar", ItemCategory.Resource, ItemType.Crafting),

                    SilverBar = GetOrCreateItem(i, "Silver Bar", "A gleaming bar of refined silver, its lustrous shine catching the eye of any who behold it. Valued for its conductivity and malleability in various crafts.", ItemCategory.Resource, ItemType.Crafting),
                    GoldBar = GetOrCreateItem(i, "Gold Bar", "This opulent bar of pure gold exudes wealth and prestige. Prized by royalty and artisans alike, it has been a symbol of power and luxury for eons.", ItemCategory.Resource, ItemType.Crafting),
                };

                // Make sure new equipments have stats.
                EnsureEquipmentStatsOnSets(typedItems);
                EnsureItemRecipes(typedItems);
                EnsureItemEffects(typedItems);
                UpgradeItemsAndRemoveDrops(typedItems, obsoleteItems);
                EnsureDungeonAndRaidDrops(typedItems);
                EnsureResourceDropRates(typedItems);
            }
            return typedItems;
        }

        private ItemPets GetOrCreatePets(IReadOnlyList<Item> i)
        {
            return new ItemPets
            {
                BaconRavenPet = GetOrCreateItem(i, "Bacon Raven Pet", ItemCategory.Pet, ItemType.Pet),
                BatPet = GetOrCreateItem(i, "Bat Pet", ItemCategory.Pet, ItemType.Pet),
                BearPet = GetOrCreateItem(i, "Bear Pet", ItemCategory.Pet, ItemType.Pet),
                BlackSantaRaven = GetOrCreateItem(i, "Black Santa Raven", ItemCategory.Pet, ItemType.Pet),
                BlueOrbPet = GetOrCreateItem(i, "Blue Orb Pet", ItemCategory.Pet, ItemType.Pet),
                DeerPet = GetOrCreateItem(i, "Deer Pet", ItemCategory.Pet, ItemType.Pet),
                DiamondRavenPet = GetOrCreateItem(i, "Diamond Raven Pet", ItemCategory.Pet, ItemType.Pet),
                DiscoRavenPet = GetOrCreateItem(i, "Disco Raven Pet", ItemCategory.Pet, ItemType.Pet),
                FoxPet = GetOrCreateItem(i, "Fox Pet", ItemCategory.Pet, ItemType.Pet),
                GhostPet = GetOrCreateItem(i, "Ghost Pet", ItemCategory.Pet, ItemType.Pet),
                GreenOrbPet = GetOrCreateItem(i, "Green Orb Pet", ItemCategory.Pet, ItemType.Pet),
                GreenSantaMetalon = GetOrCreateItem(i, "Green Santa Metalon", ItemCategory.Pet, ItemType.Pet),
                MagicSantaRaven = GetOrCreateItem(i, "Magic Santa Raven", ItemCategory.Pet, ItemType.Pet),
                PolarBearPet = GetOrCreateItem(i, "Polar Bear Pet", ItemCategory.Pet, ItemType.Pet),
                PumpkinPet = GetOrCreateItem(i, "Pumpkin Pet", ItemCategory.Pet, ItemType.Pet),
                PurpleSantaMetalon = GetOrCreateItem(i, "Purple Santa Metalon", ItemCategory.Pet, ItemType.Pet),
                Rajah = GetOrCreateItem(i, "Rajah", ItemCategory.Pet, ItemType.Pet),
                RavenPet = GetOrCreateItem(i, "Raven Pet", ItemCategory.Pet, ItemType.Pet),
                RedOrbPet = GetOrCreateItem(i, "Red Orb Pet", ItemCategory.Pet, ItemType.Pet),
                RedPandaPet = GetOrCreateItem(i, "Red Panda Pet", ItemCategory.Pet, ItemType.Pet),
                RedSantaMetalon = GetOrCreateItem(i, "Red Santa Metalon", ItemCategory.Pet, ItemType.Pet),
                SantaRaven = GetOrCreateItem(i, "Santa Raven", ItemCategory.Pet, ItemType.Pet),
                SpiderPet = GetOrCreateItem(i, "Spider Pet", ItemCategory.Pet, ItemType.Pet),
                TurdRavenPet = GetOrCreateItem(i, "Turd Raven Pet", ItemCategory.Pet, ItemType.Pet),
                WerewolfPet = GetOrCreateItem(i, "Werewolf Pet", ItemCategory.Pet, ItemType.Pet),
                WolfPet = GetOrCreateItem(i, "Wolf Pet", ItemCategory.Pet, ItemType.Pet),
                YetiPet = GetOrCreateItem(i, "Yeti Pet", ItemCategory.Pet, ItemType.Pet),
            };
        }

        private void EnsureEquipmentStatsOnSets(TypedItems typedItems)
        {
            // Shield should have same stats as leggings
            EnsureEquipmentStats(typedItems.Bronze);
            EnsureEquipmentStats(typedItems.Iron);
            EnsureEquipmentStats(typedItems.Steel);
            EnsureEquipmentStats(typedItems.Black);
            EnsureEquipmentStats(typedItems.Mithril);
            EnsureEquipmentStats(typedItems.Adamantite);
            EnsureEquipmentStats(typedItems.Rune);
            EnsureEquipmentStats(typedItems.Dragon);
            EnsureEquipmentStats(typedItems.Abraxas);
            EnsureEquipmentStats(typedItems.Phantom);
            EnsureEquipmentStats(typedItems.Ether);
            EnsureEquipmentStats(typedItems.Lionsbane);
            EnsureEquipmentStats(typedItems.Ancient);
            EnsureEquipmentStats(typedItems.Atlarus);
        }

        private void EnsureEquipmentStats(ItemSet current)
        {
            // make sure new shield has stats, same as leggings
            const float katanaPowerFactor = 1.06f;
            const float katanaAimFactor = 0.94f;
            const float spearPowerFactor = 1.04f;
            const float spearAimFactor = 0.95f;
            const float axeOneHandedPowerFactor = 1.06f;
            const float axeOneHandedAimFactor = 0.94f;
            const float axePowerFactor = 1.1f;
            const float axeAimFactor = 0.88f;
            const float oneHandedSword = 0.7f;

            if (current.Sword.WeaponPower == 0) current.Sword.WeaponPower = (int)(current.TwoHandedSword.WeaponPower * oneHandedSword);
            if (current.Sword.WeaponAim == 0) current.Sword.WeaponAim = (int)(current.TwoHandedSword.WeaponAim * oneHandedSword);
            if (current.Shield.ArmorPower == 0) current.Shield.ArmorPower = current.Leggings.ArmorPower;
            if (current.Axe != null)
            {
                if (current.Axe.WeaponPower == 0) current.Axe.WeaponPower = (int)(current.Sword.WeaponPower * axeOneHandedPowerFactor);
                if (current.Axe.WeaponAim == 0) current.Axe.WeaponAim = (int)(current.Sword.WeaponAim * axeOneHandedAimFactor);
            }
            if (current.TwoHandedAxe != null)
            {
                if (current.TwoHandedAxe.WeaponPower == 0) current.TwoHandedAxe.WeaponPower = (int)(current.TwoHandedSword.WeaponPower * axePowerFactor);
                if (current.TwoHandedAxe.WeaponAim == 0) current.TwoHandedAxe.WeaponAim = (int)(current.TwoHandedSword.WeaponAim * axeAimFactor);
            }
            /*if (current.Spear.WeaponPower == 0) */
            current.Spear.WeaponPower = (int)(current.TwoHandedSword.WeaponPower * spearPowerFactor);
            /*if (current.Spear.WeaponAim == 0) */
            current.Spear.WeaponAim = (int)(current.TwoHandedSword.WeaponAim * spearAimFactor);
            if (current.Katana.WeaponPower == 0) current.Katana.WeaponPower = (int)(current.TwoHandedSword.WeaponPower * katanaPowerFactor);
            if (current.Katana.WeaponAim == 0) current.Katana.WeaponAim = (int)(current.TwoHandedSword.WeaponAim * katanaAimFactor);
        }

        private void EnsureDungeonAndRaidDrops(TypedItems typedItems)
        {
            EnsureDrop(12, 1, typedItems.SantaHat, 0.05f, 0.0175f); // Santa hat 
            EnsureDrop(12, 1, typedItems.ChristmasToken, 0.05f, 0.0175f); // Christmas Token
            EnsureDrop(10, 1, typedItems.HalloweenToken, 0.05f, 0.0175f); // Halloween Token

            // Pet drops, available in all types of dungeon or raids
            EnsureDrop(typedItems.Pets.FoxPet, 0.05);
            EnsureDrop(typedItems.Pets.DeerPet, 0.05);
            EnsureDrop(typedItems.Pets.BearPet, 0.05);
            EnsureDrop(typedItems.Pets.BlueOrbPet, 0.05);
            EnsureDrop(typedItems.Pets.WolfPet, 0.05);
            EnsureDrop(typedItems.Pets.GreenOrbPet, 0.05);
            EnsureDrop(typedItems.Pets.PolarBearPet, 0.05);
            EnsureDrop(typedItems.Pets.RedOrbPet, 0.05);
            EnsureDrop(typedItems.Pets.RedPandaPet, 0.05);
            EnsureDrop(typedItems.BronzeBar, 0.05);
            EnsureDrop(typedItems.IronBar, 0.05);
            EnsureDrop(typedItems.SteelBar, 0.05);
            EnsureDrop(typedItems.MithrilBar, 0.05);
            EnsureDrop(typedItems.AdamantiteBar, 0.05);

            // drop resources! this should be mid tier resources
            EnsureDrop(typedItems.Lavender, 0.05);
            EnsureDrop(typedItems.Elderflower, 0.05);
            EnsureDrop(typedItems.Valerian, 0.05);
            EnsureDrop(typedItems.Chamomile, 0.05);
            EnsureDrop(typedItems.Coriander, 0.05);
            EnsureDrop(typedItems.Paprika, 0.05);
            EnsureDrop(typedItems.Turmeric, 0.05);
            EnsureDrop(typedItems.Sugar, 0.05);
            EnsureDrop(typedItems.Cinnamon, 0.05);
            EnsureDrop(typedItems.Apple, 0.05);
            EnsureDrop(typedItems.Carrots, 0.05);
            EnsureDrop(typedItems.Garlic, 0.05);
            EnsureDrop(typedItems.Onion, 0.05);
            EnsureDrop(typedItems.Milk, 0.05);

            // make sure we drop black stuff
            EnsureDrop(typedItems.Black.Helmet, 0.05);
            EnsureDrop(typedItems.Black.Boots, 0.05);
            EnsureDrop(typedItems.Black.Staff, 0.05);
            EnsureDrop(typedItems.Black.Katana, 0.05);
            EnsureDrop(typedItems.Black.Boots, 0.05);
            EnsureDrop(typedItems.Black.Gloves, 0.05);
            EnsureDrop(typedItems.Black.Axe, 0.05);
            EnsureDrop(typedItems.Black.TwoHandedAxe, 0.05);
            EnsureDrop(typedItems.Black.TwoHandedSword, 0.05);
            EnsureDrop(typedItems.Black.Spear, 0.05);

            // Dropping non-craftables
            EnsureDrop(typedItems.ArchersRing, 0.05);
            EnsureDrop(typedItems.ArchersRingII, 0.05);
            EnsureDrop(typedItems.ArchersRingIII, 0.05, slayerLevelRequirement: 30);
            EnsureDrop(typedItems.MagesRing, 0.05);
            EnsureDrop(typedItems.MagesRingII, 0.05);
            EnsureDrop(typedItems.MagesRingIII, 0.05, slayerLevelRequirement: 30);

            // Dropping tome resources
            EnsureDrop(typedItems.Hearthstone, 0.05);
            EnsureDrop(typedItems.WanderersGem, 0.05);
            EnsureDrop(typedItems.IronEmblem, 0.04);
            EnsureDrop(typedItems.KyoCrystal, 0.04);
            EnsureDrop(typedItems.HeimRune, 0.03);
            EnsureDrop(typedItems.AtriasFeather, 0.03);
            EnsureDrop(typedItems.EldarasMark, 0.03);

            // scrolls
            EnsureDrop(typedItems.ExpMultiplierScroll, 0.02);
            EnsureDrop(typedItems.RaidScroll, 0.02);
            EnsureDrop(typedItems.DungeonScroll, 0.01);

            // Exclusive to Heroic
            EnsureDrop(typedItems.MagesRingIV, 0.05, 4, slayerLevelRequirement: 60);
            EnsureDrop(typedItems.ArchersRingIV, 0.05, 4, slayerLevelRequirement: 60);
            EnsureDrop(typedItems.ArchmagesPendant, 0.05, 4, slayerLevelRequirement: 100);
            EnsureDrop(typedItems.KnightsEmblem, 0.05, 4, slayerLevelRequirement: 100);
            EnsureDrop(typedItems.OwlsEyeRing, 0.05, 4, slayerLevelRequirement: 100);
            EnsureDrop(typedItems.RingOfTheCelestial, 0.05, 4, slayerLevelRequirement: 100);
            EnsureDrop(typedItems.WarriorsMightRing, 0.05, 4, slayerLevelRequirement: 100);
            EnsureDrop(typedItems.WindcallersAmulet, 0.05, 4, slayerLevelRequirement: 100);

            EnsureDrop(typedItems.DragonBar, 0.05, 4);
            EnsureDrop(typedItems.AbraxasBar, 0.05, 4);
            EnsureDrop(typedItems.PhantomBar, 0.05, 4);
            EnsureDrop(typedItems.LioniteBar, 0.05, 4);
            EnsureDrop(typedItems.EthereumBar, 0.05, 4);
            EnsureDrop(typedItems.AncientBar, 0.05, 4);
            EnsureDrop(typedItems.AtlarusBar, 0.05, 4);
            EnsureDrop(typedItems.RawPufferFish, 0.05, 4);
            EnsureDrop(typedItems.RawOctopus, 0.05, 4);
            EnsureDrop(typedItems.RawMantaRay, 0.05, 4);
            EnsureDrop(typedItems.RawKraken, 0.05, 4);
            EnsureDrop(typedItems.Cacao, 0.05, 4);
            EnsureDrop(typedItems.Truffle, 0.05, 4);
            EnsureDrop(typedItems.Goldenrod, 0.05, 4);
            EnsureDrop(typedItems.Wormwood, 0.05, 4);
            EnsureDrop(typedItems.Skullcap, 0.05, 4);
            EnsureDrop(typedItems.LemonBalm, 0.05, 4);
            EnsureDrop(typedItems.Realmstone, 0.05, 4);
        }

        private void EnsureResourceDropRates(TypedItems items)
        {
            var farming = RavenNest.Models.Skill.Farming;
            var gathering = RavenNest.Models.Skill.Gathering;
            var woodcutting = RavenNest.Models.Skill.Woodcutting;
            var mining = RavenNest.Models.Skill.Mining;
            var fishing = RavenNest.Models.Skill.Fishing;

            #region Mining
            EnsureDropRate(10, items.Sapphire, 120, 0.2, mining);
            EnsureDropRate(20, items.Silver, 120, 0.2, mining);
            EnsureDropRate(20, items.Emerald, 120, 0.2, mining);
            EnsureDropRate(30, items.Ruby, 120, 0.2, mining);
            EnsureDropRate(30, items.Gold, 120, 0.2, mining);

            EnsureDropRate(1, items.CopperOre, 10, 0.2, mining);
            EnsureDropRate(1, items.TinOre, 10, 0.2, mining);
            EnsureDropRate(15, items.IronOre, 15, 0.2, mining);
            EnsureDropRate(20, items.Silver, 20, 0.2, mining);
            EnsureDropRate(30, items.Coal, 30, 0.2, mining);
            EnsureDropRate(40, items.Gold, 60, 0.2, mining);
            EnsureDropRate(60, items.MithrilOre, 90, 0.2, mining);
            EnsureDropRate(80, items.AdamantiteOre, 150, 0.2, mining);
            EnsureDropRate(110, items.RuneOre, 250, 0.2, mining);
            EnsureDropRate(180, items.DragonOre, 400, 0.2, mining);
            EnsureDropRate(250, items.Eldrium, 500, 0.2, mining);
            EnsureDropRate(350, items.AbraxasOre, 700, 0.2, mining);
            EnsureDropRate(450, items.PhantomOre, 1100, 0.2, mining);
            EnsureDropRate(575, items.LioniteOre, 1600, 0.2, mining);
            EnsureDropRate(700, items.EthereumOre, 2500, 0.2, mining);
            EnsureDropRate(850, items.AncientOre, 10800, 0.2, mining);
            EnsureDropRate(999, items.AtlarusOre, 21600, 0.2, mining);

            #endregion

            #region Woodcutting, poor woodcutting have no resources.
            EnsureDropRate(001, items.Logs, 10, 0.2, woodcutting);
            EnsureDropRate(010, items.BristleLogs, 20, 0.15, woodcutting);
            EnsureDropRate(015, items.GlowbarkLogs, 45, 0.14, woodcutting);
            EnsureDropRate(030, items.MystwoodLogs, 60, 0.13, woodcutting);
            EnsureDropRate(050, items.SandriftLogs, 120, 0.12, woodcutting);
            EnsureDropRate(070, items.PineheartLogs, 180, 0.11, woodcutting);
            EnsureDropRate(100, items.EbonshadeLogs, 300, 0.10, woodcutting);
            EnsureDropRate(130, items.IronbarkLogs, 400, 0.10, woodcutting);
            EnsureDropRate(170, items.FrostbiteLogs, 500, 0.09, woodcutting);
            EnsureDropRate(200, items.DragonwoodLogs, 700, 0.09, woodcutting);
            EnsureDropRate(240, items.GoldwillowLogs, 800, 0.08, woodcutting);
            EnsureDropRate(300, items.ShadowoakLogs, 1000, 0.08, woodcutting);
            #endregion

            #region For Cooking
            EnsureDropRate(1, items.Wheat, 10, 0.2, farming);
            EnsureDropRate(1, items.Water, 10, 0.2, gathering);
            EnsureDropRate(5, items.Potato, 15, 0.15, farming);
            EnsureDropRate(10, items.Tomato, 20, 0.15, farming);
            EnsureDropRate(10, items.Yeast, 60, 0.09, farming);

            EnsureDropRate(15, items.Mushroom, 30, 0.12, gathering);
            EnsureDropRate(20, items.Salt, 40, 0.12, gathering);
            EnsureDropRate(25, items.BlackPepper, 50, 0.12, gathering);
            EnsureDropRate(30, items.Cumin, 60, 0.10, farming);
            EnsureDropRate(40, items.Coriander, 90, 0.10, farming);
            EnsureDropRate(50, items.Paprika, 120, 0.10, farming);
            EnsureDropRate(60, items.Turmeric, 150, 0.10, farming);
            EnsureDropRate(100, items.Sugar, 360, 0.09, gathering);
            EnsureDropRate(120, items.Cinnamon, 360, 0.09, gathering);
            EnsureDropRate(70, items.Apple, 200, 0.10, farming);
            EnsureDropRate(80, items.Carrots, 250, 0.10, farming);
            EnsureDropRate(90, items.Garlic, 300, 0.10, farming);
            EnsureDropRate(100, items.Onion, 360, 0.10, farming);
            EnsureDropRate(120, items.Milk, 420, 0.09, farming);
            EnsureDropRate(140, items.Eggs, 500, 0.09, farming);
            EnsureDropRate(160, items.RawChicken, 600, 0.09, farming);
            EnsureDropRate(200, items.RawPork, 750, 0.08, farming);
            EnsureDropRate(240, items.RawBeef, 900, 0.08, farming);
            EnsureDropRate(320, items.Grapes, 1080, 0.07, farming);
            EnsureDropRate(400, items.Cacao, 1320, 0.06, farming);
            EnsureDropRate(800, items.Truffle, 7200, 0.05, farming);  // Added truffle as a rare ingredient at a higher level

            #endregion

            #region For Alchemy

            // gathering
            EnsureDropRate(5, items.Sand, 15, 0.2, gathering);
            EnsureDropRate(10, items.Yarrow, 15, 0.2, gathering);
            EnsureDropRate(20, items.Hemp, 30, 0.19, gathering);
            EnsureDropRate(30, items.Resin, 30, 0.19, gathering);
            EnsureDropRate(40, items.Comfrey, 60, 0.15, gathering);
            EnsureDropRate(60, items.Sage, 180, 0.15, gathering);
            EnsureDropRate(80, items.Lavender, 300, 0.12, gathering);
            EnsureDropRate(100, items.Elderflower, 420, 0.12, gathering);
            EnsureDropRate(120, items.Valerian, 600, 0.1, gathering);
            EnsureDropRate(140, items.Chamomile, 600, 0.1, gathering);
            EnsureDropRate(180, items.RedClover, 900, 0.1, gathering);
            EnsureDropRate(230, items.Mugwort, 1800, 0.09, gathering);
            EnsureDropRate(280, items.Goldenrod, 3600, 0.09, gathering);
            EnsureDropRate(330, items.Wormwood, 3600, 0.08, gathering);
            EnsureDropRate(400, items.Skullcap, 3600, 0.08, gathering);
            EnsureDropRate(500, items.LemonBalm, 7200, 0.07, gathering);
            //EnsureDropRate(740, items.GaleLeaf, 7200, 0.07, gathering);
            //EnsureDropRate(810, items.PhoenixFlower, 7200, 0.06, gathering);
            //EnsureDropRate(880, items.SteelFern, 14400, 0.06, gathering);
            //EnsureDropRate(950, items.DivineBud, 14400, 0.05, gathering);
            //EnsureDropRate(999, items.SageHerb, 21600, 0.05, gathering);
            #endregion

            #region Fishing
            EnsureDropRate(1, items.RawSprat, 10, 0.2, fishing);
            EnsureDropRate(5, items.RawShrimp, 20, 0.2, fishing);
            EnsureDropRate(20, items.RawRedSeaBass, 60, 0.19, fishing);
            EnsureDropRate(50, items.RawBass, 120, 0.19, fishing);
            EnsureDropRate(70, items.RawPerch, 200, 0.17, fishing);
            EnsureDropRate(100, items.RawSalmon, 300, 0.17, fishing);
            EnsureDropRate(130, items.RawCrab, 500, 0.15, fishing);
            EnsureDropRate(170, items.RawLobster, 800, 0.15, fishing);
            EnsureDropRate(220, items.RawBlueLobster, 1400, 0.13, fishing);
            EnsureDropRate(280, items.RawSwordfish, 1800, 0.13, fishing);
            EnsureDropRate(350, items.RawPufferFish, 3600, 0.1, fishing);
            EnsureDropRate(420, items.RawOctopus, 3600, 0.1, fishing);
            EnsureDropRate(500, items.RawMantaRay, 7200, 0.09, fishing);
            EnsureDropRate(700, items.RawKraken, 7200, 0.09, fishing);
            EnsureDropRate(900, items.RawLeviathan, 14400, 0.07, fishing);
            #endregion

            RemoveMissingItemDrops();
        }

        private void RemoveMissingItemDrops()
        {
            var l = GetResourceItemDrops().ToList();
            foreach (var drop in l)
            {
                if (GetItem(drop.ItemId) == null)
                {
                    Remove(drop);
                }
            }
        }

        private void EnsureItemRecipes(TypedItems items)
        {
            Ingredient Ingredient(Item item, int amount = 1)
            {
                return new Ingredient { Item = item, Amount = amount };
            }

            Ingredient[] Ingredients(params Item[] item)
            {
                return RavenNest.BusinessLogic.Data.Ingredient.FromArray(item);
            }

            #region Alchemy

            //// Alchemy - Processed Ingredients
            //EnsureAlchemyRecipe(200, items.DraconicEssence, items.DragonEye);
            //EnsureAlchemyRecipe(220, items.BatWingPowder, items.BatWing);
            //EnsureAlchemyRecipe(240, items.PhoenixEssence, items.PhoenixFeather);
            //EnsureAlchemyRecipe(260, items.GorgonDust, items.GorgonScale);
            //EnsureAlchemyRecipe(280, items.UnicornElixir, items.UnicornHorn);


            // Tome Base
            EnsureAlchemyRecipe(20, items.String, items.Hemp);
            EnsureAlchemyRecipe(30, items.WoodPulp, items.Logs);
            EnsureAlchemyRecipe(40, items.Paper, items.WoodPulp, items.Resin);

            // Potions
            EnsureAlchemyRecipe(10, items.HealthPotion, items.Vial, items.Yarrow);
            EnsureAlchemyRecipe(30, items.RegenPotion, items.Vial, items.Comfrey);
            EnsureAlchemyRecipe(50, items.DefensePotion, items.Vial, items.Sage);
            EnsureAlchemyRecipe(70, items.StrengthPotion, items.Vial, items.Lavender);
            EnsureAlchemyRecipe(80, items.MagicPotion, items.Vial, items.Elderflower);
            EnsureAlchemyRecipe(100, items.RangedPotion, items.Vial, items.Valerian);
            EnsureAlchemyRecipe(120, items.HealingPotion, items.Vial, items.Chamomile);
            EnsureAlchemyRecipe(160, items.GreatHealthPotion, items.Vial, items.RedClover);
            EnsureAlchemyRecipe(200, items.GreatDefensePotion, items.Vial, items.Mugwort);
            EnsureAlchemyRecipe(240, items.GreatStrengthPotion, items.Vial, items.Goldenrod);
            EnsureAlchemyRecipe(280, items.GreatMagicPotion, items.Vial, items.Wormwood);
            EnsureAlchemyRecipe(360, items.GreatRangedPotion, items.Vial, items.Skullcap);
            EnsureAlchemyRecipe(400, items.GreatHealingPotion, items.Vial, items.LemonBalm);

            // Tomes
            EnsureAlchemyRecipe(80, items.TomeOfHome, items.Paper, items.String, items.Hearthstone);
            EnsureAlchemyRecipe(150, items.TomeOfAway, items.Paper, items.String, items.WanderersGem);
            EnsureAlchemyRecipe(220, items.TomeOfIronhill, items.Paper, items.String, items.IronEmblem);
            EnsureAlchemyRecipe(290, items.TomeOfKyo, items.Paper, items.String, items.KyoCrystal);
            EnsureAlchemyRecipe(360, items.TomeOfHeim, items.Paper, items.String, items.HeimRune);
            EnsureAlchemyRecipe(430, items.TomeOfAtria, items.Paper, items.String, items.AtriasFeather);
            EnsureAlchemyRecipe(500, items.TomeOfEldara, items.Paper, items.String, items.EldarasMark);
            EnsureAlchemyRecipe(700, items.TomeOfTeleportation, items.Paper, items.String, items.Realmstone);

            #endregion

            #region Crafting

            // basic material crafting
            EnsureCraftingRecipe(20, items.SilverBar, items.Silver, items.Coal);

            // Potion Base
            EnsureCraftingRecipe(20, items.Vial, items.Sand, items.Coal);

            EnsureCraftingRecipe(30, items.GoldBar, items.Gold, items.Coal, items.Coal);
            EnsureCraftingRecipe(30, items.GoldRing, items.GoldBar, items.GoldBar);
            EnsureCraftingRecipe(30, items.GoldAmulet, items.GoldBar, items.GoldBar);
            EnsureCraftingRecipe(40, items.EmeraldRing, items.GoldRing, items.Emerald);
            EnsureCraftingRecipe(40, items.EmeraldAmulet, items.GoldAmulet, items.Emerald);
            EnsureCraftingRecipe(70, items.RubyRing, items.GoldRing, items.Ruby);
            EnsureCraftingRecipe(70, items.RubyAmulet, items.GoldAmulet, items.Ruby);
            EnsureCraftingRecipe(100, items.DragonRing, items.GoldRing, items.DragonOre);
            EnsureCraftingRecipe(100, items.DragonAmulet, items.GoldAmulet, items.DragonOre);
            EnsureCraftingRecipe(130, items.PhantomRing, items.GoldRing, items.PhantomOre);
            EnsureCraftingRecipe(130, items.PhantomAmulet, items.GoldAmulet, items.PhantomOre);

            // Bars

            void EnsureCraftingRecipeSet(int barLevel, ItemSet set, Item primaryMetal, Item woodenIngredient)
            {
                EnsureCraftingRecipe((barLevel - 1) + 5, set.Boots, Ingredient(primaryMetal, 3));
                EnsureCraftingRecipe((barLevel - 1) + 6, set.Gloves, Ingredient(primaryMetal, 3));
                EnsureCraftingRecipe((barLevel - 1) + 6, set.Sword, Ingredient(primaryMetal, 3));
                EnsureCraftingRecipe((barLevel - 1) + 7, set.Helmet, Ingredient(primaryMetal, 3));
                EnsureCraftingRecipe((barLevel - 1) + 10, set.Axe, Ingredient(primaryMetal, 5));
                EnsureCraftingRecipe((barLevel - 1) + 14, set.Bow, Ingredient(primaryMetal, 4), Ingredient(woodenIngredient, 2));
                EnsureCraftingRecipe((barLevel - 1) + 14, set.Staff, Ingredient(primaryMetal, 4), Ingredient(woodenIngredient, 2));
                EnsureCraftingRecipe((barLevel - 1) + 14, set.Spear, Ingredient(primaryMetal, 4), Ingredient(woodenIngredient, 2));
                EnsureCraftingRecipe((barLevel - 1) + 14, set.TwoHandedSword, Ingredient(primaryMetal, 5));
                EnsureCraftingRecipe((barLevel - 1) + 14, set.TwoHandedAxe, Ingredient(primaryMetal, 5));
                EnsureCraftingRecipe((barLevel - 1) + 15, set.Katana, Ingredient(primaryMetal, 5));
                EnsureCraftingRecipe((barLevel - 1) + 15, set.Shield, Ingredient(primaryMetal, 4));
                EnsureCraftingRecipe((barLevel - 1) + 16, set.Leggings, Ingredient(primaryMetal, 4));
                EnsureCraftingRecipe((barLevel - 1) + 18, set.Chest, Ingredient(primaryMetal, 5));
            }

            EnsureCraftingRecipe(001, items.BronzeBar, items.CopperOre, items.TinOre);
            EnsureCraftingRecipe(010, items.IronBar, items.IronOre, items.IronOre);
            EnsureCraftingRecipe(015, items.SteelBar, items.IronOre, items.Coal);
            EnsureCraftingRecipe(050, items.MithrilBar, (Ingredient)items.MithrilOre, new Ingredient { Item = items.Coal, Amount = 4 });
            EnsureCraftingRecipe(070, items.AdamantiteBar, (Ingredient)items.AdamantiteOre, new Ingredient { Item = items.Coal, Amount = 6 });
            EnsureCraftingRecipe(090, items.RuneBar, (Ingredient)items.RuneOre, new Ingredient { Item = items.Coal, Amount = 8 });
            EnsureCraftingRecipe(120, items.DragonBar, (Ingredient)items.DragonOre, new Ingredient { Item = items.Coal, Amount = 10 });
            EnsureCraftingRecipe(150, items.AbraxasBar, (Ingredient)items.AbraxasOre, new Ingredient { Item = items.Coal, Amount = 15 });
            EnsureCraftingRecipe(180, items.PhantomBar, (Ingredient)items.PhantomOre, new Ingredient { Item = items.Coal, Amount = 20 });
            EnsureCraftingRecipe(220, items.LioniteBar, (Ingredient)items.LioniteOre, new Ingredient { Item = items.Coal, Amount = 25 });
            EnsureCraftingRecipe(260, items.EthereumBar, (Ingredient)items.EthereumOre, new Ingredient { Item = items.Coal, Amount = 30 });
            EnsureCraftingRecipe(300, items.AncientBar, (Ingredient)items.AncientOre, new Ingredient { Item = items.Coal, Amount = 40 });
            EnsureCraftingRecipe(350, items.AtlarusBar, (Ingredient)items.AtlarusOre, new Ingredient { Item = items.Coal, Amount = 50 });
            EnsureCraftingRecipe(400, items.ElderBronzeBar, (Ingredient)items.BronzeBar, Ingredient(items.Eldrium, 2), Ingredient(items.Coal, 60));
            EnsureCraftingRecipe(450, items.ElderIronBar, (Ingredient)items.IronBar, Ingredient(items.Eldrium, 4), Ingredient(items.Coal, 70));
            EnsureCraftingRecipe(500, items.ElderSteelBar, (Ingredient)items.SteelBar, Ingredient(items.Eldrium, 6), Ingredient(items.Coal, 80));
            EnsureCraftingRecipe(550, items.ElderMithrilBar, (Ingredient)items.MithrilBar, Ingredient(items.Eldrium, 8), Ingredient(items.Coal, 90));
            EnsureCraftingRecipe(600, items.ElderAdamantiteBar, (Ingredient)items.AdamantiteBar, Ingredient(items.Eldrium, 10), Ingredient(items.Coal, 100));
            EnsureCraftingRecipe(650, items.ElderRuneBar, (Ingredient)items.RuneBar, Ingredient(items.Eldrium, 15), Ingredient(items.Coal, 110));
            EnsureCraftingRecipe(700, items.ElderDragonBar, (Ingredient)items.DragonBar, Ingredient(items.Eldrium, 20), Ingredient(items.Coal, 120));
            EnsureCraftingRecipe(750, items.ElderAbraxasBar, (Ingredient)items.AbraxasBar, Ingredient(items.Eldrium, 25), Ingredient(items.Coal, 130));
            EnsureCraftingRecipe(800, items.ElderPhantomBar, (Ingredient)items.PhantomBar, Ingredient(items.Eldrium, 30), Ingredient(items.Coal, 140));
            EnsureCraftingRecipe(850, items.ElderLioniteBar, (Ingredient)items.LioniteBar, Ingredient(items.Eldrium, 35), Ingredient(items.Coal, 160));
            EnsureCraftingRecipe(900, items.ElderEthereumBar, (Ingredient)items.EthereumBar, Ingredient(items.Eldrium, 40), Ingredient(items.Coal, 180));
            EnsureCraftingRecipe(950, items.ElderAtlarusBar, (Ingredient)items.AtlarusBar, Ingredient(items.Eldrium, 50), Ingredient(items.Coal, 200));

            EnsureCraftingRecipeSet(1, items.Bronze, items.BronzeBar, items.Logs);
            EnsureCraftingRecipeSet(10, items.Iron, items.IronBar, items.Logs);
            EnsureCraftingRecipeSet(15, items.Steel, items.SteelBar, items.BristleLogs);
            EnsureCraftingRecipeSet(50, items.Mithril, items.MithrilBar, items.GlowbarkLogs);
            EnsureCraftingRecipeSet(70, items.Adamantite, items.AdamantiteBar, items.MystwoodLogs);
            EnsureCraftingRecipeSet(90, items.Rune, items.RuneBar, items.SandriftLogs);
            EnsureCraftingRecipeSet(120, items.Dragon, items.DragonBar, items.PineheartLogs);
            EnsureCraftingRecipeSet(150, items.Abraxas, items.AbraxasBar, items.EbonshadeLogs);
            EnsureCraftingRecipeSet(180, items.Phantom, items.PhantomBar, items.IronbarkLogs);
            EnsureCraftingRecipeSet(220, items.Lionsbane, items.LioniteBar, items.FrostbiteLogs);
            EnsureCraftingRecipeSet(260, items.Ether, items.EthereumBar, items.DragonwoodLogs);
            EnsureCraftingRecipeSet(300, items.Ancient, items.AncientBar, items.GoldwillowLogs);
            EnsureCraftingRecipeSet(400, items.Atlarus, items.AtlarusBar, items.ShadowoakLogs);

            #endregion

            #region Cooking
            // cooking fish
            EnsureCookingRecipe(1, items.Sprat, items.BurnedSprat, 0.2, 1, items.RawSprat);
            EnsureCookingRecipe(5, items.Shrimp, items.BurnedShrimp, 0.2, 1, items.RawShrimp);
            EnsureCookingRecipe(20, items.RedSeaBass, items.BurnedRedSeaBass, 0.2, 1, items.RawRedSeaBass);
            EnsureCookingRecipe(50, items.Bass, items.BurnedBass, 0.2, 1, items.RawBass);
            EnsureCookingRecipe(70, items.Perch, items.BurnedPerch, 0.2, 1, items.RawPerch);
            EnsureCookingRecipe(100, items.Salmon, items.BurnedSalmon, 0.2, 1, items.RawSalmon);
            EnsureCookingRecipe(130, items.Crab, items.BurnedCrab, 0.2, 1, items.RawCrab);
            EnsureCookingRecipe(170, items.Lobster, items.BurnedLobster, 0.2, 1, items.RawLobster);
            EnsureCookingRecipe(220, items.BlueLobster, items.BurnedBlueLobster, 0.2, 1, items.RawBlueLobster);
            EnsureCookingRecipe(280, items.Swordfish, items.BurnedSwordfish, 0.2, 1, items.RawSwordfish);
            EnsureCookingRecipe(350, items.PufferFish, items.BurnedPufferFish, 0.2, 1, items.RawPufferFish);
            EnsureCookingRecipe(420, items.Octopus, items.BurnedOctopus, 0.2, 1, items.RawOctopus);
            EnsureCookingRecipe(500, items.MantaRay, items.BurnedMantaRay, 0.2, 1, items.RawMantaRay);
            EnsureCookingRecipe(700, items.Kraken, items.BurnedKraken, 0.2, 1, items.RawKraken);

            // cooking various meats and stuff
            EnsureCookingRecipeGuaranteed(10, items.Flour, Ingredient(items.Wheat));
            EnsureCookingRecipe(30, items.Bread, items.BurnedBread, 0.2, 1, items.Flour, items.Water, items.Salt, items.Yeast);
            EnsureCookingRecipeGuaranteed(60, items.HamSandwich, Ingredients(items.Bread, items.Butter, items.Ham));
            EnsureCookingRecipeGuaranteed(70, items.Butter, Ingredients(items.Milk));
            EnsureCookingRecipe(80, items.RoastedPork, items.BurnedPork, 0.2, 1, items.RawPork);
            EnsureCookingRecipeGuaranteed(90, items.Ham, Ingredients(items.Salt, items.BlackPepper, items.RawPork));

            EnsureCookingRecipe(100, items.RoastedChicken, items.BurnedChicken, 0.2, 1, items.RawChicken);

            EnsureCookingRecipeGuaranteed(110, items.Cheese, Ingredients(items.Milk, items.Yeast, items.Salt));
            EnsureCookingRecipeGuaranteed(140, items.RawChickenLeg, 2, Ingredients(items.RawChicken));
            EnsureCookingRecipe(140, items.CookedChickenLeg, items.BurnedChickenLeg, 0.2, 1, items.RawChickenLeg);

            EnsureCookingRecipe(150, items.GrilledCheese, items.BurnedGrilledCheese, 0.2, 1, items.Bread, items.Butter, items.Cheese, items.Ham);
            EnsureCookingRecipe(180, items.RoastBeef, items.BurnedBeef, 0.2, 1, items.RawBeef);
            EnsureCookingRecipe(200, items.ApplePie, items.BurnedApplePie, 0.2, 1, items.Apple, items.Sugar, items.Butter, items.Flour, items.Cinnamon);
            EnsureCookingRecipe(250, items.Steak, items.BurnedSteak, 0.2, 1, items.RawBeef, items.Salt, items.BlackPepper);
            EnsureCookingRecipeGuaranteed(400, items.Chocolate, Ingredients(items.Cacao, items.Milk, items.Sugar));

            EnsureCookingRecipe(300, items.Skewers, items.BurnedSkewers, 0.2, 1, items.RawBeef, items.SpiceMix);

            EnsureCookingRecipe(450, items.ChocolateChipCookies, items.BurnedChocolateChipCookies, 0.2, 1, items.Chocolate, items.Sugar, items.Butter, items.Flour);

            EnsureCookingRecipeGuaranteed(500, items.RedWine, Ingredient(items.Grapes, 10));


            EnsureCookingRecipeGuaranteed(30,
                "Spice Mix", "A masterful medley of choice spices, this mix is a culinary revelation. The robust warmth of cumin mingles with the golden glow of turmeric, while the citrusy zing of coriander dances with the deep richness of black pepper. A hint of paprika adds an extra layer of complexity. Together, these ingredients work in concert, seasoned by the foundational touch of salt. A must-have in every kitchen, ensuring every dish sings with flavor.",
                items.SpiceMix, items.Salt, items.BlackPepper, items.Cumin, items.Coriander, items.Paprika, items.Turmeric);

            EnsureCookingRecipeGuaranteed(850, 10,
                "Golden Leaf", "A pinnacle of culinary luxury, the Golden Leaf is meticulously crafted from the purest gold nuggets. Each leaf, thin and delicate, gleams with an unmatched opulence. Its creation is a testament to the art of gastronomy, allowing chefs to garnish their creations with a touch of the sublime. Beyond its shimmering beauty, the Golden Leaf symbolizes the zenith of culinary achievement, turning any dish into a masterpiece of elegance and prestige. For those who seek to dazzle and awe, no ingredient is more coveted.",
                items.GoldenLeaf,
                items.Gold);

            EnsureCookingRecipe(900,
                "Leviathan's Royal Stew", "This is a hearty stew that combines the tender meat of the Leviathan with a variety of other ingredients to create a flavorful dish worthy of its namesake.",
                items.LeviathansRoyalStew,
                items.MuddledLeviathanBroth, 0.5, 1,
                items.RawLeviathan, items.Water, items.SpiceMix, items.Onion, items.Tomato, items.Flour, items.Mushroom, items.RoastBeef, items.Butter, items.RedWine);

            EnsureCookingRecipe(999,
                "Poseidon's Guardian Feast", "A luxurious dish that showcases the divine nature of Poseidon's Guardian. It involves a series of preparations that results in a meal fit for a deity.",
                items.PoseidonsGuardianFeast,
                items.RuinedGuardianDelight, 0.5, 1,
                items.RawPoseidonsGuardian, items.SpiceMix, items.Milk, items.Eggs, items.RoastedChicken, items.Cheese, items.Tomato, items.Onion, items.Flour, items.GoldenLeaf);
            #endregion
        }

        private void EnsureItemEffects(TypedItems typedItems)
        {
            var effects = itemStatusEffects.Entities.ToList();

            // Eating cooked fish
            GetOrCreateItemStatusEffect(effects, typedItems.Shrimp, StatusEffectType.Heal, 0.02f, 2);

            // Consuming Potions

            // Potions
            int regularPotionDuration = 120;  // seconds
            int greatPotionDuration = 600;   // seconds

            // Regular Potions
            GetOrCreateItemStatusEffect(effects, typedItems.DefensePotion, StatusEffectType.IncreasedDefense, regularPotionDuration, 0.20f, 5);
            GetOrCreateItemStatusEffect(effects, typedItems.StrengthPotion, StatusEffectType.IncreasedStrength, regularPotionDuration, 0.20f, 5);
            GetOrCreateItemStatusEffect(effects, typedItems.MagicPotion, StatusEffectType.IncreasedMagicPower, regularPotionDuration, 0.20f, 5);
            GetOrCreateItemStatusEffect(effects, typedItems.RangedPotion, StatusEffectType.IncreasedRangedPower, regularPotionDuration, 0.20f, 5);
            GetOrCreateItemStatusEffect(effects, typedItems.HealingPotion, StatusEffectType.IncreasedHealingPower, regularPotionDuration, 0.20f, 5);

            // Great Potions
            GetOrCreateItemStatusEffect(effects, typedItems.GreatDefensePotion, StatusEffectType.IncreasedDefense, greatPotionDuration, 0.40f, 10);
            GetOrCreateItemStatusEffect(effects, typedItems.GreatStrengthPotion, StatusEffectType.IncreasedStrength, greatPotionDuration, 0.40f, 10);
            GetOrCreateItemStatusEffect(effects, typedItems.GreatMagicPotion, StatusEffectType.IncreasedMagicPower, greatPotionDuration, 0.40f, 10);
            GetOrCreateItemStatusEffect(effects, typedItems.GreatRangedPotion, StatusEffectType.IncreasedRangedPower, greatPotionDuration, 0.40f, 10);
            GetOrCreateItemStatusEffect(effects, typedItems.GreatHealingPotion, StatusEffectType.IncreasedHealingPower, greatPotionDuration, 0.40f, 10);

            GetOrCreateItemStatusEffect(effects, typedItems.HealthPotion, StatusEffectType.Heal, 0.15f, 10); // will heal 15% of max health or 10 hp minimum.
            GetOrCreateItemStatusEffect(effects, typedItems.GreatHealthPotion, StatusEffectType.Heal, 0.40f, 50); // will heal 40% of max health or 50 hp minimum.
            GetOrCreateItemStatusEffect(effects, typedItems.RegenPotion, StatusEffectType.HealOverTime, 15, 0.25f, 50); // will heal total of 25% of max health or minimum 25 hp over the duration of 15 seconds.

            // Fish dishes
            EnsureItemStatusEffects(typedItems.Sprat, Effect(StatusEffectType.Heal, 0.03f, 3));
            EnsureItemStatusEffects(typedItems.Shrimp, Effect(StatusEffectType.Heal, 0.04f, 4));
            EnsureItemStatusEffects(typedItems.RedSeaBass, Effect(StatusEffectType.Heal, 0.06f, 10));
            EnsureItemStatusEffects(typedItems.Bass, Effect(StatusEffectType.Heal, 0.08f, 12));
            EnsureItemStatusEffects(typedItems.Perch, Effect(StatusEffectType.Heal, 0.10f, 15));
            EnsureItemStatusEffects(typedItems.Salmon, Effect(StatusEffectType.Heal, 0.12f, 20), Effect(StatusEffectType.IncreasedStrength, 90, 0.05f, 2));
            EnsureItemStatusEffects(typedItems.Crab, Effect(StatusEffectType.Heal, 0.15f, 25), Effect(StatusEffectType.IncreasedDefense, 90, 0.05f, 2));
            EnsureItemStatusEffects(typedItems.Lobster, Effect(StatusEffectType.Heal, 0.20f, 25), Effect(StatusEffectType.IncreasedAttackPower, 120, 0.07f, 3));
            EnsureItemStatusEffects(typedItems.BlueLobster, Effect(StatusEffectType.Heal, 0.25f, 30), Effect(StatusEffectType.IncreasedMagicPower, 120, 0.08f, 3));
            EnsureItemStatusEffects(typedItems.Swordfish, Effect(StatusEffectType.Heal, 0.25f, 30), Effect(StatusEffectType.IncreasedAttackSpeed, 180, 0.10f, 3));
            EnsureItemStatusEffects(typedItems.PufferFish, Effect(StatusEffectType.Heal, 0.30f, 35), Effect(StatusEffectType.IncreasedDodge, 150, 0.10f, 0));
            EnsureItemStatusEffects(typedItems.Octopus, Effect(StatusEffectType.Heal, 0.35f, 40), Effect(StatusEffectType.IncreasedMagicPower, 240, 0.12f, 4));
            EnsureItemStatusEffects(typedItems.MantaRay, Effect(StatusEffectType.Heal, 0.40f, 50), Effect(StatusEffectType.IncreasedMovementSpeed, 240, 0.10f, 0));
            EnsureItemStatusEffects(typedItems.Kraken, Effect(StatusEffectType.Heal, 0.45f, 60), Effect(StatusEffectType.IncreasedExperienceGain, 300, 0.12f, 0));

            // other dishes
            EnsureItemStatusEffects(typedItems.RedWine, Effect(StatusEffectType.Heal, 0.05f, 5), Effect(StatusEffectType.ReducedHitChance, 180, 0.05f, 0), Effect(StatusEffectType.IncreasedStrength, 180, 0.05f, 2));
            EnsureItemStatusEffects(typedItems.HamSandwich, Effect(StatusEffectType.Heal, 0.08f, 10));
            EnsureItemStatusEffects(typedItems.RoastedChicken, Effect(StatusEffectType.Heal, 0.10f, 15));
            EnsureItemStatusEffects(typedItems.RoastBeef, Effect(StatusEffectType.Heal, 0.15f, 25), Effect(StatusEffectType.IncreasedStrength, 180, 0.07f, 3));
            EnsureItemStatusEffects(typedItems.RoastedPork, Effect(StatusEffectType.Heal, 0.12f, 20), Effect(StatusEffectType.IncreasedDefense, 150, 0.06f, 3));
            EnsureItemStatusEffects(typedItems.CookedChickenLeg, Effect(StatusEffectType.Heal, 0.10f, 15));
            EnsureItemStatusEffects(typedItems.Steak, Effect(StatusEffectType.Heal, 0.18f, 28), Effect(StatusEffectType.IncreasedStrength, 200, 0.08f, 3));
            EnsureItemStatusEffects(typedItems.GrilledCheese, Effect(StatusEffectType.Heal, 0.09f, 12), Effect(StatusEffectType.IncreasedDefense, 100, 0.05f, 2));
            EnsureItemStatusEffects(typedItems.ApplePie, Effect(StatusEffectType.Heal, 0.14f, 22), Effect(StatusEffectType.IncreasedMagicPower, 150, 0.05f, 3));
            EnsureItemStatusEffects(typedItems.Bread, Effect(StatusEffectType.Heal, 0.06f, 8));
            EnsureItemStatusEffects(typedItems.Skewers, Effect(StatusEffectType.Heal, 0.11f, 18), Effect(StatusEffectType.IncreasedAttackSpeed, 140, 0.06f, 2));
            EnsureItemStatusEffects(typedItems.ChocolateChipCookies, Effect(StatusEffectType.Heal, 0.07f, 10));

            // Burnt :o
            EnsureItemStatusEffects(typedItems.BurnedGrilledCheese, Effect(StatusEffectType.Heal, 0.02f, 2));
            EnsureItemStatusEffects(typedItems.BurnedChicken, Effect(StatusEffectType.Heal, 0.02f, 2));
            EnsureItemStatusEffects(typedItems.BurnedBeef, Effect(StatusEffectType.Heal, 0.02f, 2));
            EnsureItemStatusEffects(typedItems.BurnedPork, Effect(StatusEffectType.Heal, 0.02f, 2));
            EnsureItemStatusEffects(typedItems.BurnedChickenLeg, Effect(StatusEffectType.Heal, 0.02f, 2));
            EnsureItemStatusEffects(typedItems.BurnedSteak, Effect(StatusEffectType.Heal, 0.02f, 2));
            EnsureItemStatusEffects(typedItems.BurnedApplePie, Effect(StatusEffectType.Heal, 0.02f, 2));
            EnsureItemStatusEffects(typedItems.BurnedBread, Effect(StatusEffectType.Heal, 0.02f, 2));
            EnsureItemStatusEffects(typedItems.BurnedSkewers, Effect(StatusEffectType.Heal, 0.02f, 2));
            EnsureItemStatusEffects(typedItems.BurnedChocolateChipCookies, Effect(StatusEffectType.Heal, 0.02f, 2));

            // Failed fish dishes based on 20% of original dish's healing values
            EnsureItemStatusEffects(typedItems.BurnedSprat, Effect(StatusEffectType.Heal, 0.006f, 1));
            EnsureItemStatusEffects(typedItems.BurnedShrimp, Effect(StatusEffectType.Heal, 0.008f, 1));
            EnsureItemStatusEffects(typedItems.BurnedRedSeaBass, Effect(StatusEffectType.Heal, 0.012f, 2));
            EnsureItemStatusEffects(typedItems.BurnedBass, Effect(StatusEffectType.Heal, 0.016f, 2));
            EnsureItemStatusEffects(typedItems.BurnedPerch, Effect(StatusEffectType.Heal, 0.020f, 3));
            EnsureItemStatusEffects(typedItems.BurnedSalmon, Effect(StatusEffectType.Heal, 0.024f, 4));
            EnsureItemStatusEffects(typedItems.BurnedCrab, Effect(StatusEffectType.Heal, 0.030f, 5));
            EnsureItemStatusEffects(typedItems.BurnedLobster, Effect(StatusEffectType.Heal, 0.040f, 5));
            EnsureItemStatusEffects(typedItems.BurnedBlueLobster, Effect(StatusEffectType.Heal, 0.050f, 6));
            EnsureItemStatusEffects(typedItems.BurnedSwordfish, Effect(StatusEffectType.Heal, 0.050f, 6));
            EnsureItemStatusEffects(typedItems.BurnedPufferFish, Effect(StatusEffectType.Heal, 0.060f, 7));
            EnsureItemStatusEffects(typedItems.BurnedOctopus, Effect(StatusEffectType.Heal, 0.070f, 8));
            EnsureItemStatusEffects(typedItems.BurnedMantaRay, Effect(StatusEffectType.Heal, 0.080f, 10));
            EnsureItemStatusEffects(typedItems.BurnedKraken, Effect(StatusEffectType.Heal, 0.090f, 12));

            // Failed special dishes
            // I'm basing these values on the CookedKraken for now, but these dishes may be considered special enough to have a bit more.
            EnsureItemStatusEffects(typedItems.MuddledLeviathanBroth, Effect(StatusEffectType.Heal, 0.09f, 12));
            EnsureItemStatusEffects(typedItems.RuinedGuardianDelight, Effect(StatusEffectType.Heal, 0.09f, 12));

            // Special dishes
            EnsureItemStatusEffects(typedItems.LeviathansRoyalStew, Effect(StatusEffectType.Heal, 0.50f, 80), Effect(StatusEffectType.IncreasedDefense, 360, 0.15f, 5), Effect(StatusEffectType.IncreasedStrength, 360, 0.15f, 5));
            EnsureItemStatusEffects(typedItems.PoseidonsGuardianFeast, Effect(StatusEffectType.Heal, 0.65f, 100), Effect(StatusEffectType.IncreasedMagicPower, 600, 0.20f, 8), Effect(StatusEffectType.IncreasedAttackPower, 600, 0.20f, 8), Effect(StatusEffectType.IncreasedHealingPower, 600, 0.20f, 8));

            // Teleportation
            GetOrCreateItemStatusEffect(effects, typedItems.TomeOfHome, StatusEffectType.TeleportToIsland, Island.Home);
            GetOrCreateItemStatusEffect(effects, typedItems.TomeOfAway, StatusEffectType.TeleportToIsland, Island.Away);
            GetOrCreateItemStatusEffect(effects, typedItems.TomeOfIronhill, StatusEffectType.TeleportToIsland, Island.Ironhill);
            GetOrCreateItemStatusEffect(effects, typedItems.TomeOfKyo, StatusEffectType.TeleportToIsland, Island.Kyo);
            GetOrCreateItemStatusEffect(effects, typedItems.TomeOfHeim, StatusEffectType.TeleportToIsland, Island.Heim);
            GetOrCreateItemStatusEffect(effects, typedItems.TomeOfAtria, StatusEffectType.TeleportToIsland, Island.Atria);
            GetOrCreateItemStatusEffect(effects, typedItems.TomeOfEldara, StatusEffectType.TeleportToIsland, Island.Eldara);
            GetOrCreateItemStatusEffect(effects, typedItems.TomeOfTeleportation, StatusEffectType.TeleportToIsland, Island.Any);
        }

        public struct StatusEffect
        {
            public float Amount;
            public float MinAmount;
            public float Duration;
            public StatusEffectType Type;
        }
    }

    public static class ItemExtensions
    {

        public static Item LevelRequirement(this Item item, int levelRequirement)
        {
            if (levelRequirement == 0) return item;

            var category = (ItemCategory)item.Category;
            var type = (ItemType)item.Type;

            if (type == ItemType.TwoHandedStaff)
                item.RequiredMagicLevel = levelRequirement;
            else if (type == ItemType.TwoHandedBow)
                item.RequiredRangedLevel = levelRequirement;
            else if (category == ItemCategory.Weapon)
                item.RequiredAttackLevel = levelRequirement;
            else if (category == ItemCategory.Armor)
                item.RequiredDefenseLevel = levelRequirement;

            return item;
        }
        public static Item GenericPrefab(this Item item, string path, bool overwrite = true)
        {
            if (item == null) return null;
            if (overwrite || string.IsNullOrEmpty(item.GenericPrefab))
            {
                item.GenericPrefab = path;
                item.IsGenericModel = string.IsNullOrEmpty(item.GenericPrefab);
            }
            return item;
        }
    }
}
