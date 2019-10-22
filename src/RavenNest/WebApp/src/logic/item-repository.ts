import { Item } from './models';
import Requests from '../requests';

export default class ItemRepository {
    public static isLoaded: boolean = false;    
    public static isLoading: boolean = false;

    private static itemData: any;
    private static loadedItems: Item[] = [];    

    get items(): Item[] {
        if (ItemRepository.isLoaded) {
            return ItemRepository.loadedItems;
        }

        if (!ItemRepository.isLoading) {
            ItemRepository.loadItemsAsync();
        }

        return [];
    }

    public static async loadItemsAsync() {
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

    private static parseItemData(itemData: any) {
        // console.log(JSON.stringify(itemData));
    }
}