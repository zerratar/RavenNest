import GameMath from './game-math';

export class CharacterSkill {
  constructor(
    public readonly name: string,
    public readonly experience: number,
    public readonly level: number,
    public readonly procent: number) {
  }
}

export class GameSession {
  constructor(
    public readonly id: string,

    public readonly UserId: string,
    public readonly twitchUserId: string,
    public readonly userName: string,
    public readonly adminPrivileges: boolean,
    public readonly modPrivileges: boolean,
    public readonly started: string,
    public readonly updated: string,
    public readonly status: number,
  ) { }
}

export class Player {
  constructor(
    public readonly userId: string,
    public readonly userName: string,
    public name: string,
    public readonly statistics: Statistics,
    public readonly appearance: Appearance,
    public readonly resources: Resources,
    public readonly skills: Skills,
    public readonly state: PlayerState,
    public readonly inventoryItems: InventoryItem[],
    public readonly local: boolean,
    public isAdmin: boolean,
    public isModerator: boolean,
    public readonly originUserId: string,
    public readonly revision: number,
    public readonly identity: string,
    public readonly characterIndex: number,
  ) { }
}

export class Appearance {
  constructor(
    public readonly id: string,
    public readonly gender: number,
    public readonly hair: number,
    public readonly head: number,
    public readonly eyebrows: number,
    public readonly facialHair: number,
    public readonly skinColor: string,
    public readonly hairColor: string,
    public readonly beardColor: string,
    public readonly eyeColor: string,
    public readonly helmetVisible: boolean,
    public readonly stubbleColor: string,
    public readonly warPaintColor: string,
  ) { }
}


export class Resources {
  constructor(
    public readonly id: string,
    public readonly wood: number,
    public readonly ore: number,
    public readonly fish: number,
    public readonly wheat: number,
    public readonly magic: number,
    public readonly arrows: number,
    public readonly coins: number,
    public readonly revision: number,
  ) { }
}

export class Skills {
  constructor(
    public readonly id: string,
    public readonly attack: number,
    public readonly defense: number,
    public readonly strength: number,
    public readonly health: number,
    public readonly magic: number,
    public readonly ranged: number,
    public readonly woodcutting: number,
    public readonly fishing: number,
    public readonly mining: number,
    public readonly crafting: number,
    public readonly cooking: number,
    public readonly farming: number,
    public readonly slayer: number,
    public readonly sailing: number,
    public readonly revision: number,
    public readonly attackLevel: number,
    public readonly defenseLevel: number,
    public readonly strengthLevel: number,
    public readonly healthLevel: number,
    public readonly magicLevel: number,
    public readonly rangedLevel: number,
    public readonly woodcuttingLevel: number,
    public readonly fishingLevel: number,
    public readonly miningLevel: number,
    public readonly craftingLevel: number,
    public readonly cookingLevel: number,
    public readonly farmingLevel: number,
    public readonly slayerLevel: number,
    public readonly sailingLevel: number,
    public readonly attackProcent: number,
    public readonly defenseProcent: number,
    public readonly strengthProcent: number,
    public readonly healthProcent: number,
    public readonly magicProcent: number,
    public readonly rangedProcent: number,
    public readonly woodcuttingProcent: number,
    public readonly fishingProcent: number,
    public readonly miningProcent: number,
    public readonly craftingProcent: number,
    public readonly cookingProcent: number,
    public readonly farmingProcent: number,
    public readonly slayerProcent: number,
    public readonly sailingProcent: number,
  ) { }
}


export class Statistics {
  constructor(
    public readonly id: string,
    public readonly raidsWon: number,
    public readonly raidsLost: number,
    public readonly raidsJoined: number,
    public readonly duelsWon: number,
    public readonly duelsLost: number,
    public readonly playersKilled: number,
    public readonly enemiesKilled: number,
    public readonly arenaFightsJoined: number,
    public readonly arenaFightsWon: number,
    public readonly totalDamageDone: number,
    public readonly totalDamageTaken: number,
    public readonly deathCount: number,
    public readonly totalWoodCollected: number,
    public readonly totalOreCollected: number,
    public readonly totalFishCollected: number,
    public readonly totalWheatCollected: number,
    public readonly craftedWeapons: number,
    public readonly craftedArmors: number,
    public readonly craftedPotions: number,
    public readonly craftedRings: number,
    public readonly craftedAmulets: number,
    public readonly cookedFood: number,
    public readonly consumedPotions: number,
    public readonly consumedFood: number,
    public readonly totalTreesCutDown: number,
  ) { }
}

export class PlayerState {
  constructor(
    public readonly id: string,
    public readonly health: number,
    public readonly inRaid: boolean,
    public readonly inArena: boolean,
    public readonly task: string,
    public readonly taskArgument: string,
    public readonly island: string,
    public readonly x: number,
    public readonly y: number,
    public readonly z: number) { }
}

export class InventoryItem {
  public item: Item | null = null;
  constructor(
    public readonly id: string,
    public readonly itemId: string,
    public readonly equipped: boolean,
    public readonly amount: number) { }
}

export class ItemStat {
  constructor(
    public readonly name: string,
    public readonly value: number) { }
}

export class Item {
  constructor(
    public readonly id: string,
    public readonly name: string,
    public readonly level: number,
    public readonly weaponAim: number,
    public readonly weaponPower: number,
    public readonly armorPower: number,
    public readonly requiredAttackLevel: number,
    public readonly requiredDefenseLevel: number,
    public readonly category: number,
    public readonly type: number,
    public readonly material: number,
    public readonly craftable: boolean | null,
    public readonly requiredCraftingLevel: number,
    public readonly woodCost: number,
    public readonly oreCost: number,
  ) { }
}
