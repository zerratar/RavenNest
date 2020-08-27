import { CharacterSkill, InventoryItem, Player } from './models';
import Requests from '../requests';
import { PlayerInfo} from './player-info';

export default class MyPlayer {
    public static isLoaded: boolean = false;
    public static isLoading: boolean = false;
    private static player: PlayerInfo | null;

    public static get playerName(): string {
        if (!MyPlayer.player) return '';
        return MyPlayer.player.playerName;
    }

    public static getEquippedItems(): InventoryItem[] {
      if (!MyPlayer.player) return [];
      return MyPlayer.player.getEquippedItems();
    }

    public static getInventoryItems(): InventoryItem[] {
      if (!MyPlayer.player) return [];
      return MyPlayer.player.getInventoryItems();
    }

    public static getCombatLevel(): number {
      if (!MyPlayer.player) return 3;
      return MyPlayer.player.getCombatLevel();
    }

    public static getSkill(name: string): CharacterSkill { // CharacterSkill
          if (!MyPlayer.player) return new CharacterSkill('', 0);
          return MyPlayer.player.getSkill(name);
      }

    public static getSkills(): CharacterSkill[] {
      if (!MyPlayer.player) return [];
      return MyPlayer.player.getSkills();
    }

    public static async getPlayerDataAsync() {
      MyPlayer.isLoading = true;
      const url = `api/players`;
      const result = await Requests.sendAsync(url);
      if (result.ok) {
          MyPlayer.player = new PlayerInfo((await result.json()) as Player);
      }
      MyPlayer.isLoading = false;
    }
}
