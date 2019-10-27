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