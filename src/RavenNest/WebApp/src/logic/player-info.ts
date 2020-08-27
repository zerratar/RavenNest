import { CharacterSkill, InventoryItem, PlayerState, Player } from './models';
import ItemRepository from './item-repository';

export class PlayerInfo {
  public isLoaded: boolean = false;
  public isLoading: boolean = false;
  private skills: any = {}; // any[] = [];
  private inventoryItems: InventoryItem[] = [];
  private state: PlayerState | null = null;

  constructor(
      private readonly srcPlayer: Player) {
        this.getPlayerDataAsync();
  }

  public get playerName(): string {
    if (!this.srcPlayer)
      return '';
    return this.srcPlayer.name;
  }

  public getEquippedItems(): InventoryItem[] {
    const items = [...this.inventoryItems.filter((x) => x.equipped === true)];
    items.forEach((x) => {
      const targetItem = ItemRepository.items.find((y) => y.id === x.itemId);
      if (targetItem) {
        x.item = targetItem;
      }
    });
    return items;
  }

  public getInventoryItems(): InventoryItem[] {
    const items = [...this.inventoryItems.filter((x) => x.equipped === false)];
    items.forEach((x) => {
      const targetItem = ItemRepository.items.find((y) => y.id === x.itemId);
      if (targetItem) {
        x.item = targetItem;
      }
    });
    return items;
  }

  public getCombatLevel(): number {
    if (!('attack' in this.skills))
      return 3;
    const attack = this.getSkill('attack').level;
    const defense = this.getSkill('defense').level;
    const strength = this.getSkill('strength').level;
    const health = this.getSkill('health').level;
    const magic = this.getSkill('magic').level;
    const ranged = this.getSkill('ranged').level;
    return Math.floor(((attack + defense + strength + health) / 4)
      + (magic / 8) + (ranged / 8));
  }

  public getSkill(name: string): CharacterSkill {
    // return this.skills.find(x => x.name.toLowerCase() === name.toLowerCase());
    return this.skills[name] as CharacterSkill;
  }

  public getSkills(): CharacterSkill[] {
    return [...Object.getOwnPropertyNames(this.skills)
      .map((x) => this.skills[x] as CharacterSkill)
      .filter((x) => typeof x !== 'undefined' && x.name != null && x.name.length > 0),
    ];
  }

  public getPlayerDataAsync() {
    this.isLoading = true;
    this.parsePlayerData(this.srcPlayer);
    this.isLoading = false;
  }

  private parsePlayerData(data: any): void {
    if (!data || data == null) return;
    for (const propName in data.skills) {
      if (propName === 'id' || propName === 'revision') {
        continue;
      }
      this.skills[propName.toLowerCase()] = new CharacterSkill(propName, data.skills[propName]);
    }
    this.inventoryItems = [];
    for (const val of data.inventoryItems) {
      const item: any = val;
      const invItem = new InventoryItem(item.id, item.itemId, item.equipped, item.amount);
      this.inventoryItems.push(invItem);
    }
    this.state = new PlayerState(data.state.id, data.state.health, data.state.inRaid, data.state.inArena,
      data.state.task, data.state.taskArgument, data.state.island, data.state.x, data.state.y, data.state.z);
  }
}
