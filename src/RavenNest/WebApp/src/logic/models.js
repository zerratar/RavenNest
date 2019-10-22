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
export class Item {
}
//# sourceMappingURL=models.js.map