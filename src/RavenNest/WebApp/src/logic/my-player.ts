import { Item, CharacterSkill, InventoryItem, PlayerState } from './models';
import Requests from '../requests';
import ItemRepository from './item-repository';

export default class MyPlayer {
    public static isLoaded: boolean = false;    
    public static isLoading: boolean = false;

    private static playerData: any = null;
    private static skills: any = {};//any[] = [];
    private static inventoryItems: InventoryItem[] = [];
    private static state: PlayerState;

    public static get playerName(): string {
        if (!MyPlayer.playerData) return '';
        return MyPlayer.playerData.name;
    }

    public static getEquippedItems(): InventoryItem[] { 
      const items = [...MyPlayer.inventoryItems.filter(x => x.equipped === true)];
      items.forEach(x=> { 
        const targetItem = ItemRepository.items.find(y => y.id == x.itemId);
        if (targetItem) {
          x.item = targetItem;
        }        
      });
      return items;
    }

    public static getInventoryItems(): InventoryItem[] {
      const items = [...MyPlayer.inventoryItems.filter(x => x.equipped === false)];
      items.forEach(x=> { 
        const targetItem = ItemRepository.items.find(y => y.id == x.itemId);
        if (targetItem) {
          x.item = targetItem;
        }        
      });
      return items;
    }
  
    public static getCombatLevel(): number {            
        if(!('attack' in MyPlayer.skills)) return 3;
            const attack = MyPlayer.getSkill("attack").level;
            const defense = MyPlayer.getSkill("defense").level;
            const strength = MyPlayer.getSkill("strength").level;
            const health = MyPlayer.getSkill("health").level;
            const magic = MyPlayer.getSkill("magic").level;
            const ranged = MyPlayer.getSkill("ranged").level;

            return Math.floor(((attack + defense + strength + health) / 4) 
                + (magic / 8) + (ranged / 8));
        }
  
    public static getSkill(name: string): CharacterSkill { // CharacterSkill
          // return this.skills.find(x => x.name.toLowerCase() === name.toLowerCase());
          return <CharacterSkill>MyPlayer.skills[name];
      }
  
      public static getSkills(): CharacterSkill[] {
        return [...Object.getOwnPropertyNames(MyPlayer.skills)
          .map(x => <CharacterSkill>MyPlayer.skills[x])
          .filter(x => typeof x !== "undefined" && x.name != null && x.name.length > 0)
        ];
      }
  
      public static async getPlayerDataAsync() {
        MyPlayer.isLoading = true; 

        const url = `api/players`;
        const result = await Requests.sendAsync(url);
        if (result.ok) {        
            MyPlayer.playerData = (await result.json());
            MyPlayer.parsePlayerData(MyPlayer.playerData);  
        }
        MyPlayer.isLoading = false;
      }
  
      private static parsePlayerData(data: any): void {
        for (let propName in data.skills) {
          if (propName == "id" || propName == "revision") {
            continue;
          }
          MyPlayer.skills[propName.toLowerCase()] = new CharacterSkill(propName, data.skills[propName]);
        }

        MyPlayer.inventoryItems = [];
        for(let val of data.inventoryItems) {
            const item:any = val;
            const invItem = new InventoryItem(item.id, item.itemId, item.equipped, item.amount);
            MyPlayer.inventoryItems.push(invItem);
        }
        // WTH? y u no work?
        // MyPlayer.inventoryItems = data.inventoryItems.map((x:any) =>);        

        MyPlayer.state = new PlayerState(
            data.state.id,
            data.state.health,
            data.state.inRaid,
            data.state.inArena,
            data.state.task,
            data.state.taskArgument,
            data.state.island,
            data.state.x,
            data.state.y,
            data.state.z
        );
      }
}