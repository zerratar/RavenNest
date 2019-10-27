import { Item } from './models';
import Requests from '../requests';
export default class ItemRepository {
    static get items() {
        if (ItemRepository.isLoaded) {
            return ItemRepository.loadedItems;
        }
        if (!ItemRepository.isLoading) {
            ItemRepository.loadItemsAsync();
        }
        return [];
    }
    static async loadItemsAsync() {
        if (ItemRepository.isLoading || ItemRepository.isLoaded) {
            return;
        }
        ItemRepository.isLoading = true;
        const url = `api/items`;
        const result = await Requests.sendAsync(url);
        if (result.ok) {
            this.itemData = (await result.json());
            this.parseItemData(this.itemData);
            ItemRepository.isLoaded = true;
        }
        ItemRepository.isLoading = false;
    }
    static parseItemData(itemData) {
        for (let raw of itemData) {
            ItemRepository.loadedItems.push(new Item(raw.id, raw.name, raw.level, raw.weaponAim, raw.weaponPower, raw.armorPower, raw.requiredAttackLevel, raw.requiredDefenseLevel, raw.category, raw.type, raw.material, raw.craftable, raw.requiredCraftingLevel, raw.woodCost, raw.oreCost));
        }
        // console.log(JSON.stringify(itemData));
    }
}
ItemRepository.isLoaded = false;
ItemRepository.isLoading = false;
ItemRepository.loadedItems = [];
//# sourceMappingURL=item-repository.js.map