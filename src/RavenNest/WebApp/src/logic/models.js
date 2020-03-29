import GameMath from './game-math';
export class CharacterSkill {
    constructor(name, experience) {
        this.name = name;
        this.experience = experience;
        this.level = GameMath.expTolevel(experience);
        const min = GameMath.levelToExp(this.level);
        this.totalExpForNextLevel = GameMath.levelToExp(this.level + 1);
        const currentExp = experience - min;
        this.expForNextLevel = this.totalExpForNextLevel - min;
        this.percent = currentExp / this.expForNextLevel;
    }
}
export class GameSession {
    constructor(id, UserId, twitchUserId, userName, adminPrivileges, modPrivileges, started, updated, status) {
        this.id = id;
        this.UserId = UserId;
        this.twitchUserId = twitchUserId;
        this.userName = userName;
        this.adminPrivileges = adminPrivileges;
        this.modPrivileges = modPrivileges;
        this.started = started;
        this.updated = updated;
        this.status = status;
    }
}
export class Player {
    constructor(userId, userName, name, statistics, appearance, resources, skills, state, inventoryItems, local, isAdmin, isModerator, originUserId, revision) {
        this.userId = userId;
        this.userName = userName;
        this.name = name;
        this.statistics = statistics;
        this.appearance = appearance;
        this.resources = resources;
        this.skills = skills;
        this.state = state;
        this.inventoryItems = inventoryItems;
        this.local = local;
        this.isAdmin = isAdmin;
        this.isModerator = isModerator;
        this.originUserId = originUserId;
        this.revision = revision;
    }
}
export class Appearance {
    constructor(id, gender, hair, head, eyebrows, facialHair, skinColor, hairColor, beardColor, eyeColor, helmetVisible, stubbleColor, warPaintColor) {
        this.id = id;
        this.gender = gender;
        this.hair = hair;
        this.head = head;
        this.eyebrows = eyebrows;
        this.facialHair = facialHair;
        this.skinColor = skinColor;
        this.hairColor = hairColor;
        this.beardColor = beardColor;
        this.eyeColor = eyeColor;
        this.helmetVisible = helmetVisible;
        this.stubbleColor = stubbleColor;
        this.warPaintColor = warPaintColor;
    }
}
export class Resources {
    constructor(id, wood, ore, fish, wheat, magic, arrows, coins, revision) {
        this.id = id;
        this.wood = wood;
        this.ore = ore;
        this.fish = fish;
        this.wheat = wheat;
        this.magic = magic;
        this.arrows = arrows;
        this.coins = coins;
        this.revision = revision;
    }
}
export class Skills {
    constructor(id, attack, defense, strength, health, magic, ranged, woodcutting, fishing, mining, crafting, cooking, farming, slayer, sailing, revision) {
        this.id = id;
        this.attack = attack;
        this.defense = defense;
        this.strength = strength;
        this.health = health;
        this.magic = magic;
        this.ranged = ranged;
        this.woodcutting = woodcutting;
        this.fishing = fishing;
        this.mining = mining;
        this.crafting = crafting;
        this.cooking = cooking;
        this.farming = farming;
        this.slayer = slayer;
        this.sailing = sailing;
        this.revision = revision;
    }
}
export class Statistics {
    constructor(id, raidsWon, raidsLost, raidsJoined, duelsWon, duelsLost, playersKilled, enemiesKilled, arenaFightsJoined, arenaFightsWon, totalDamageDone, totalDamageTaken, deathCount, totalWoodCollected, totalOreCollected, totalFishCollected, totalWheatCollected, craftedWeapons, craftedArmors, craftedPotions, craftedRings, craftedAmulets, cookedFood, consumedPotions, consumedFood, totalTreesCutDown) {
        this.id = id;
        this.raidsWon = raidsWon;
        this.raidsLost = raidsLost;
        this.raidsJoined = raidsJoined;
        this.duelsWon = duelsWon;
        this.duelsLost = duelsLost;
        this.playersKilled = playersKilled;
        this.enemiesKilled = enemiesKilled;
        this.arenaFightsJoined = arenaFightsJoined;
        this.arenaFightsWon = arenaFightsWon;
        this.totalDamageDone = totalDamageDone;
        this.totalDamageTaken = totalDamageTaken;
        this.deathCount = deathCount;
        this.totalWoodCollected = totalWoodCollected;
        this.totalOreCollected = totalOreCollected;
        this.totalFishCollected = totalFishCollected;
        this.totalWheatCollected = totalWheatCollected;
        this.craftedWeapons = craftedWeapons;
        this.craftedArmors = craftedArmors;
        this.craftedPotions = craftedPotions;
        this.craftedRings = craftedRings;
        this.craftedAmulets = craftedAmulets;
        this.cookedFood = cookedFood;
        this.consumedPotions = consumedPotions;
        this.consumedFood = consumedFood;
        this.totalTreesCutDown = totalTreesCutDown;
    }
}
export class PlayerState {
    constructor(id, health, inRaid, inArena, task, taskArgument, island, x, y, z) {
        this.id = id;
        this.health = health;
        this.inRaid = inRaid;
        this.inArena = inArena;
        this.task = task;
        this.taskArgument = taskArgument;
        this.island = island;
        this.x = x;
        this.y = y;
        this.z = z;
    }
}
export class InventoryItem {
    constructor(id, itemId, equipped, amount) {
        this.id = id;
        this.itemId = itemId;
        this.equipped = equipped;
        this.amount = amount;
        this.item = null;
    }
}
export class ItemStat {
    constructor(name, value) {
        this.name = name;
        this.value = value;
    }
}
export class Item {
    constructor(id, name, level, weaponAim, weaponPower, armorPower, requiredAttackLevel, requiredDefenseLevel, category, type, material, craftable, requiredCraftingLevel, woodCost, oreCost) {
        this.id = id;
        this.name = name;
        this.level = level;
        this.weaponAim = weaponAim;
        this.weaponPower = weaponPower;
        this.armorPower = armorPower;
        this.requiredAttackLevel = requiredAttackLevel;
        this.requiredDefenseLevel = requiredDefenseLevel;
        this.category = category;
        this.type = type;
        this.material = material;
        this.craftable = craftable;
        this.requiredCraftingLevel = requiredCraftingLevel;
        this.woodCost = woodCost;
        this.oreCost = oreCost;
    }
}
//# sourceMappingURL=models.js.map