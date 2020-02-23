import GameMath from './game-math';

export class Player {
  
}

export class CharacterSkill {
  public level: number;
  public percent: number;

  public totalExpForNextLevel: number;
  public expForNextLevel: number;

  constructor(
    public readonly name: string,
    public readonly experience: number) {
    this.level = GameMath.expTolevel(experience);
    const min = GameMath.levelToExp(this.level);
    this.totalExpForNextLevel = GameMath.levelToExp(this.level + 1);
    const currentExp = experience - min;
    this.expForNextLevel = this.totalExpForNextLevel - min;
    this.percent = currentExp / this.expForNextLevel;
  }
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
      public readonly z: number)  {}
}

export class InventoryItem {
  public item: Item|null = null;
  constructor(
    public readonly id: string,
    public readonly itemId: string,
    public readonly equipped: boolean,
    public readonly amount: number) {}
}

export class ItemStat {
  constructor(
    public readonly name: string,
    public readonly value: number) {}
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
    public readonly craftable: boolean|null,
    public readonly requiredCraftingLevel: number,
    public readonly woodCost: number,
    public readonly oreCost: number
  ) { }
}