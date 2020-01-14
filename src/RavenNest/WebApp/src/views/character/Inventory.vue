<template>
    <div class="player-inventory">     
        <div>
            <h2>Equipped</h2>
            <div class="inventory-items">
                <div class="inventory-item equipped" v-for="item in getEquippedItems()" :key="item.id">
                    <div class="item-image" :data-item="item.id" @mouseover="mouseOverItem(item)" @mouseleave="mouseExitItem(item)"><img :src="getItemImage(item.itemId)" /></div>
                    <div class="item-tooltip" :class="{visible: getTooltipVisible(item)}" :data-item="item.id">
                        <div class="item-name" :data-tier="getItemTier(item)">{{getItemName(item)}}</div>
                        <div class="item-type">{{getItemType(item)}}</div>
                        
                        <div class="item-stat" v-for="stat in getItemStats(item)" :key="stat.name">
                            <div class="item-stat-name">{{stat.name}}</div>
                            <div class="item-stat-value">{{stat.value}}</div>                            
                        </div>

                        <div class="item-requirement">
                            <div>Requires {{getItemRequirementSkill(item)}} level</div>
                            <div>{{getItemRequirementLevel(item)}}</div>
                        </div>
                    </div> 
                </div>                
            </div>
        </div>

        <div>
            <h2>Inventory</h2>
            <div class="inventory-items inventory">
                <div class="inventory-item" v-for="item in getInventoryItems()" :key="item.id">
                    <div class="item-image"  :data-item="item.id" @mouseover="mouseOverItem(item)" @mouseleave="mouseExitItem(item)"><img :src="getItemImage(item.itemId)" /></div>
                    <div class="item-amount">{{getItemAmount(item)}}</div>
                    <div class="item-tooltip" :class="{visible: getTooltipVisible(item)}" :data-item="item.id">
                        <div class="item-name" :data-tier="getItemTier(item)">{{getItemName(item)}}</div>
                        <div class="item-type">{{getItemType(item)}}</div>
                        
                        <div class="item-stat" v-for="stat in getItemStats(item)" :key="stat.name">
                            <div class="item-stat-name">{{stat.name}}</div>
                            <div class="item-stat-value">{{stat.value}}</div>     
                        </div>

                        <div class="item-requirement">
                            <div>Requires {{getItemRequirementSkill(item)}} level</div>
                            <div>{{getItemRequirementLevel(item)}}</div>
                        </div>
                    </div>                    
                </div>
            </div>
        </div>
    </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
  } from 'vue-property-decorator';
  
  import { SessionState } from '@/App.vue';
  import GameMath from '../../logic/game-math';
  import { CharacterSkill, InventoryItem, ItemStat } from '../../logic/models';
  import ItemRepository from '../../logic/item-repository';
  import Requests from '../../requests';
  import router from 'vue-router';
import MyPlayer from '@/logic/my-player';


  @Component({})
  export default class Inventory extends Vue {

    private readonly tooltipVisibility: Map<string,boolean> = new Map<string, boolean>();

    public mouseOverItem(invItem: InventoryItem): void {
        // console.log("mouseOverItem: " + invItem.id);
        this.tooltipVisibility.set(invItem.id, true);
        this.$forceUpdate();
    }

    public mouseExitItem(invItem: InventoryItem): void {
        // console.log("mouseExitItem: " + invItem.id);
        this.tooltipVisibility.set(invItem.id, false);
        this.$forceUpdate();        
    }

    public getTooltipVisible(invItem: InventoryItem): boolean {
        return this.tooltipVisibility.get(invItem.id) === true;
    }

    public getItemStats(invItem: InventoryItem): ItemStat[] {
        const itemStats: ItemStat[] = [];
        const item = invItem.item;
        if (!item) return itemStats;
        if (item.weaponAim > 0) itemStats.push(new ItemStat("Aim", item.weaponAim));
        if (item.weaponPower > 0) itemStats.push(new ItemStat("Power", item.weaponPower));
        if (item.armorPower > 0) itemStats.push(new ItemStat("Armor", item.armorPower));
        return itemStats;
    }

    public getItemTier(item: InventoryItem): string {
        if (!item.item) return '0';
        if (item.item.type === 20) return 'pet';
        if (item.item.requiredAttackLevel === 100 || item.item.requiredDefenseLevel === 100) return '8';
        if (item.item.requiredAttackLevel === 120 || item.item.requiredDefenseLevel === 120) return '9';
        return item.item.material.toString();
    }

    public getItemAmount(item: InventoryItem): string {
        const value = item.amount;
        if (value >= 1000_000) {
			var mils = value / 1000000.0;
			return Math.round(mils) + "M";
		}
		else if (value > 1000) {
			var ks = value / 1000;
			return Math.round(ks) + "K";
		}		
        return item.amount.toString();
    }

    public getItemType(item: InventoryItem): string {
        if(!item.item) return "";
        switch(item.item.type) {
            case 1: return "Two Handed Sword";
            case 2: return "One Handed Sword";
            case 3: return "Two Handed Axe";
            case 4: return "One Handed Axe";
            case 5: return "Two Handed Staff";
            case 6: return "Two Handed Bow";
            case 7: return "One Handed Mace";
            case 8: return "Helm";
            case 9: return "Chest";
            case 10: return "Gloves";
            case 11: return "Boots";
            case 12: return "Leggings";
            case 13: return "Shield";
            case 14: return "Left Shoulder Piece";
            case 15: return "Right Shoulder Piece";
            case 16: return "Ring";
            case 17: return "Amulet";
            case 18: return "Food";
            case 19: return "Potion";
            case 20: return "Pet";
            case 21: return "Coins";
            case 22: return "Wood";
            case 23: return "Ore";
            case 24: return "Fish";
            case 25: return "Wheat";
            case 26: return "Arrows";
            case 27: return "Magic";
            default: return "";
        }
    }

    public getItemRequirementLevel(item: InventoryItem): number {
        if (!item.item) return 0;
        if (item.item.requiredAttackLevel > 0)
            return item.item.requiredAttackLevel;
        return item.item.requiredDefenseLevel;
    }

    public getItemRequirementSkill(item: InventoryItem): string {
        if(!item.item) return "";
        if (item.item.requiredAttackLevel > 0)
            return "attack";
        return "defense";
    }

    public getEquippedItems(): InventoryItem[] {
        return MyPlayer.getEquippedItems();
    }

    public getInventoryItems(): InventoryItem[] {
        return MyPlayer.getInventoryItems();
    }

    public getItemImage(itemId:string): string {
        return `/assets/imgs/items/${itemId}.png`;
    }

    public getItemName(item:InventoryItem): string {
        if (!item.item) return item.itemId;
        return item.item.name;
    }
  }
</script>

<style scoped>
.player-inventory {
    background-color: #ececec;
    padding-top: 25px;
    border-top: 1px solid #dfdfdf;
    margin-top: 25px;
    padding-bottom: 25px;
}


.item-tooltip {
    position: absolute;
    display: none;
    z-index: -10;
}
.inventory .item-tooltip {
    margin-top: -25px;
}

.item-tooltip.visible {
    display: flex;
    z-index: 1;
    width: 200px;
    background-color: #000000e6;
    color: white;

    padding: 20px;
    border-radius: 8px;
    flex-flow: column;
    text-align: left;
}



.item-stat {
    display: flex;
    justify-content: space-between;
}

.item-name {
    font-size: 15pt;
    margin-bottom: -5px;
}

.item-type {
    font-size: 10pt;
    margin-bottom: 10px;
}

.item-requirement {
    display: flex;
    justify-content: space-between;
    font-size: 10pt;
    margin-top: 5px;
}

/* ultima */
.item-name[data-tier='pet'] {
    COLOR: #ffae00;
}

/* ultima */
.item-name[data-tier='9'] {
    COLOR: #e34ff1;
}

/* dragon */
.item-name[data-tier='8'] {
    COLOR: #f14f4f;
}

/* rune */
.item-name[data-tier='7'] {
    COLOR: #4ff1c7;
}

/* adamantite */ 
.item-name[data-tier='6'] {
    color: #70ac5e;
}

/* mithril */ 
.item-name[data-tier='5'] {
    color: #5a85ff;
}

/* black */
.item-name[data-tier='4'] {
    color: #9191cd;
}

.inventory-items {
    display: flex;
    flex-flow: row;
    flex-wrap: wrap;
    justify-content: center;
}

.inventory-item {
    margin: 5px;
}

.inventory .inventory-item {
    margin-bottom: -25px;
}

.item-amount {
    position: relative;
    top: -31px;
    right: -61px;
    background-color: white;
    width: 30px;
    color: #353535;
    border-radius: 0 0 14px 0;
}

.inventory-item .item-image img {
    width: 75px;
    height: 75px;
    max-width: 100%;
    border: 3px solid #ffffff;
    border-radius: 20px;
    /* cursor: pointer; */
    padding: 5px;
}

.inventory-item.equipped .item-image img {
    border-color: #4e97b3;
}
</style>