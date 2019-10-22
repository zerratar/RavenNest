import Requests from '../requests';
export default class ItemRepository {
    get items() {
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
        // console.log(JSON.stringify(itemData));
    }
}
ItemRepository.isLoaded = false;
ItemRepository.isLoading = false;
ItemRepository.loadedItems = [];
//# sourceMappingURL=item-repository.js.map