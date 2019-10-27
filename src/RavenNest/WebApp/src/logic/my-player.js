import { CharacterSkill, InventoryItem, PlayerState } from './models';
import Requests from '../requests';
import ItemRepository from './item-repository';
export default class MyPlayer {
    static get playerName() {
        if (!MyPlayer.playerData)
            return '';
        return MyPlayer.playerData.name;
    }
    static getEquippedItems() {
        const items = [...MyPlayer.inventoryItems.filter(x => x.equipped === true)];
        items.forEach(x => {
            const targetItem = ItemRepository.items.find(y => y.id == x.itemId);
            if (targetItem) {
                x.item = targetItem;
            }
        });
        return items;
    }
    static getInventoryItems() {
        const items = [...MyPlayer.inventoryItems.filter(x => x.equipped === false)];
        items.forEach(x => {
            const targetItem = ItemRepository.items.find(y => y.id == x.itemId);
            if (targetItem) {
                x.item = targetItem;
            }
        });
        return items;
    }
    static getCombatLevel() {
        if (!('attack' in MyPlayer.skills))
            return 3;
        const attack = MyPlayer.getSkill("attack").level;
        const defense = MyPlayer.getSkill("defense").level;
        const strength = MyPlayer.getSkill("strength").level;
        const health = MyPlayer.getSkill("health").level;
        const magic = MyPlayer.getSkill("magic").level;
        const ranged = MyPlayer.getSkill("ranged").level;
        return Math.floor(((attack + defense + strength + health) / 4)
            + (magic / 8) + (ranged / 8));
    }
    static getSkill(name) {
        // return this.skills.find(x => x.name.toLowerCase() === name.toLowerCase());
        return MyPlayer.skills[name];
    }
    static getSkills() {
        return [...Object.getOwnPropertyNames(MyPlayer.skills)
                .map(x => MyPlayer.skills[x])
                .filter(x => typeof x !== "undefined" && x.name != null && x.name.length > 0)
        ];
    }
    static async getPlayerDataAsync() {
        MyPlayer.isLoading = true;
        const url = `api/players`;
        const result = await Requests.sendAsync(url);
        if (result.ok) {
            MyPlayer.playerData = (await result.json());
            MyPlayer.parsePlayerData(MyPlayer.playerData);
        }
        MyPlayer.isLoading = false;
    }
    static parsePlayerData(data) {
        for (let propName in data.skills) {
            if (propName == "id" || propName == "revision") {
                continue;
            }
            MyPlayer.skills[propName.toLowerCase()] = new CharacterSkill(propName, data.skills[propName]);
        }
        MyPlayer.inventoryItems = [];
        for (let val of data.inventoryItems) {
            const item = val;
            const invItem = new InventoryItem(item.id, item.itemId, item.equipped, item.amount);
            MyPlayer.inventoryItems.push(invItem);
        }
        // WTH? y u no work?
        // MyPlayer.inventoryItems = data.inventoryItems.map((x:any) =>);        
        MyPlayer.state = new PlayerState(data.state.id, data.state.health, data.state.inRaid, data.state.inArena, data.state.task, data.state.taskArgument, data.state.island, data.state.x, data.state.y, data.state.z);
    }
}
MyPlayer.isLoaded = false;
MyPlayer.isLoading = false;
MyPlayer.playerData = null;
MyPlayer.skills = {}; //any[] = [];
MyPlayer.inventoryItems = [];
//# sourceMappingURL=my-player.js.map