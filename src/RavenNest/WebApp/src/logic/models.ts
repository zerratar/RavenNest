import GameMath from './game-math';

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

export class Item {

}