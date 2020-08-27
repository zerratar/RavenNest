
  // TODO (zerratar): calculate this on the server instead
  //                  so we don't have to share the logic on the client
  export default class GameMath {
    private static expTable: number[] = [];
    public static maxLevel: number = 170;

    public static levelToExp(level: number): number {
      GameMath.calculateExpTable();
      return level - 2 < 0 ? 0 : GameMath.expTable[level - 2];
    }

    public static expTolevel(exp: number): number {
      GameMath.calculateExpTable();
      for (let level = 0; level < GameMath.maxLevel - 1; level++) {
          if (exp >= GameMath.expTable[level])
              continue;
          return (level + 1);
        }
      return GameMath.maxLevel;
    }

    private static calculateExpTable(): void {
      if (GameMath.expTable.length > 0) {
        return;
      }
      let totalExp = 0;
      for (let levelIndex = 0; levelIndex < GameMath.maxLevel; levelIndex++) {
          const level = levelIndex + 1;
          const levelExp = (level + (300 * Math.pow(2, (level / 7))));
          totalExp += levelExp;
          GameMath.expTable[levelIndex] = ((totalExp & 0xffffffffc) / 4);
      }
    }
  }
