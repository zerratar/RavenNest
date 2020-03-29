import { CharacterSkill, InventoryItem, PlayerState } from './models';
import ItemRepository from './item-repository';
export class PlayerInfo {
    constructor(srcPlayer) {
        this.srcPlayer = srcPlayer;
        this.isLoaded = false;
        this.isLoading = false;
        this.skills = {}; //any[] = [];
        this.inventoryItems = [];
        this.state = null;
        this.getPlayerDataAsync();
    }
    get playerName() {
        if (!this.srcPlayer)
            return '';
        return this.srcPlayer.name;
    }
    getEquippedItems() {
        const items = [...this.inventoryItems.filter(x => x.equipped === true)];
        items.forEach(x => {
            const targetItem = ItemRepository.items.find(y => y.id == x.itemId);
            if (targetItem) {
                x.item = targetItem;
            }
        });
        return items;
    }
    getInventoryItems() {
        const items = [...this.inventoryItems.filter(x => x.equipped === false)];
        items.forEach(x => {
            const targetItem = ItemRepository.items.find(y => y.id == x.itemId);
            if (targetItem) {
                x.item = targetItem;
            }
        });
        return items;
    }
    getCombatLevel() {
        if (!('attack' in this.skills))
            return 3;
        const attack = this.getSkill("attack").level;
        const defense = this.getSkill("defense").level;
        const strength = this.getSkill("strength").level;
        const health = this.getSkill("health").level;
        const magic = this.getSkill("magic").level;
        const ranged = this.getSkill("ranged").level;
        return Math.floor(((attack + defense + strength + health) / 4)
            + (magic / 8) + (ranged / 8));
    }
    getSkill(name) {
        // return this.skills.find(x => x.name.toLowerCase() === name.toLowerCase());
        return this.skills[name];
    }
    getSkills() {
        return [...Object.getOwnPropertyNames(this.skills)
                .map(x => this.skills[x])
                .filter(x => typeof x !== "undefined" && x.name != null && x.name.length > 0)
        ];
    }
    getPlayerDataAsync() {
        this.isLoading = true;
        this.parsePlayerData(this.srcPlayer);
        this.isLoading = false;
    }
    parsePlayerData(data) {
        if (!data || data == null)
            return;
        for (let propName in data.skills) {
            if (propName == "id" || propName == "revision") {
                continue;
            }
            this.skills[propName.toLowerCase()] = new CharacterSkill(propName, data.skills[propName]);
        }
        this.inventoryItems = [];
        for (let val of data.inventoryItems) {
            const item = val;
            const invItem = new InventoryItem(item.id, item.itemId, item.equipped, item.amount);
            this.inventoryItems.push(invItem);
        }
        this.state = new PlayerState(data.state.id, data.state.health, data.state.inRaid, data.state.inArena, data.state.task, data.state.taskArgument, data.state.island, data.state.x, data.state.y, data.state.z);
    }
}
//# sourceMappingURL=player-info.js.map