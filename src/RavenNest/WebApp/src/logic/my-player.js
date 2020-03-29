import { CharacterSkill } from './models';
import Requests from '../requests';
import { PlayerInfo } from './player-info';
export default class MyPlayer {
    static get playerName() {
        if (!MyPlayer.player)
            return '';
        return MyPlayer.player.playerName;
    }
    static getEquippedItems() {
        if (!MyPlayer.player)
            return [];
        return MyPlayer.player.getEquippedItems();
    }
    static getInventoryItems() {
        if (!MyPlayer.player)
            return [];
        return MyPlayer.player.getInventoryItems();
    }
    static getCombatLevel() {
        if (!MyPlayer.player)
            return 3;
        return MyPlayer.player.getCombatLevel();
    }
    static getSkill(name) {
        if (!MyPlayer.player)
            return new CharacterSkill("", 0);
        return MyPlayer.player.getSkill(name);
    }
    static getSkills() {
        if (!MyPlayer.player)
            return [];
        return MyPlayer.player.getSkills();
    }
    static async getPlayerDataAsync() {
        MyPlayer.isLoading = true;
        const url = `api/players`;
        const result = await Requests.sendAsync(url);
        if (result.ok) {
            MyPlayer.player = new PlayerInfo((await result.json()));
        }
        MyPlayer.isLoading = false;
    }
}
MyPlayer.isLoaded = false;
MyPlayer.isLoading = false;
//# sourceMappingURL=my-player.js.map