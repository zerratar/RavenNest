// TODO (zerratar): calculate this on the server instead
//                  so we don't have to share the logic on the client
export default class GameMath {
    static levelToExp(level) {
        GameMath.calculateExpTable();
        return level - 2 < 0 ? 0 : GameMath.expTable[level - 2];
    }
    static expTolevel(exp) {
        GameMath.calculateExpTable();
        for (let level = 0; level < GameMath.maxLevel - 1; level++) {
            if (exp >= GameMath.expTable[level])
                continue;
            return (level + 1);
        }
        return GameMath.maxLevel;
    }
    static calculateExpTable() {
        if (GameMath.expTable.length > 0) {
            return;
        }
        let totalExp = 0;
        for (let levelIndex = 0; levelIndex < GameMath.maxLevel; levelIndex++) {
            let level = levelIndex + 1;
            let levelExp = (level + (300 * Math.pow(2, (level / 7))));
            totalExp += levelExp;
            GameMath.expTable[levelIndex] = ((totalExp & 0xffffffffc) / 4);
        }
    }
}
GameMath.expTable = [];
GameMath.maxLevel = 170;
//# sourceMappingURL=game-math.js.map