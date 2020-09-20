<template>
  <div class="items">
    <h1>Ravenfall Items</h1>
    <br />

    <table class="items-list">
      <thead>
        <tr>
          <th></th>
          <th>Name</th>
          <th>Weapon Aim</th>
          <th>Weapon Power</th>
          <th>Armor Power</th>
          <th>Attack Level</th>
          <th>Defense Level</th>
          <th>Category</th>
          <th>Item Type</th>
          <th>Material Type</th>
          <th>Crafting Level</th>
          <th>Vendor Price</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in items" :key="item.name">
          <td><img v-bind:src="item.imgPath" style="width: 40px"/></td>
          <td class='item'>{{item.name}}</td>
          <td class='item'>{{item.weaponAim}}</td>
          <td class='item'>{{item.weaponPower}}</td>
          <td class='item'>{{item.armorPower}}</td>
          <td class='item'>{{item.requiredAttackLevel}}</td>
          <td class='item'>{{item.requiredDefenseLevel}}</td>
          <td class='item'>{{item.category}}</td>
          <td class='item'>{{item.type}}</td>
          <td class='item'>{{item.material}}</td>
          <td class='item'>{{item.requiredCraftingLevel}}</td>
          <td class='item'><img class="ravenCoins" src="/favicon.ico" />{{item.shopSellPrice}}</td>
        </tr>
      </tbody>
    </table>
    <div v-if="dataLoading" class="loader">
      <div class="lds-ripple">
        <div></div>
        <div></div>
      </div>
    </div>
  </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
  } from 'vue-property-decorator';
  import router from 'vue-router';
  import Skill from '../skill';
  import Requests from '../requests';
  import { SessionState } from '@/App.vue';
  import Inventory from './character/Inventory.vue';

  @Component({})
  export default class ItemList extends Vue {
    private dataLoading: boolean = true;
    private items: any[] = [];

    private async loadItems() {
      this.dataLoading = true;
      const url = `api/items`;
      const result = await Requests.sendAsync(url);
      if (result.ok) {
        this.items = this.sortByName(await result.json());
        this.items.forEach((item) => {
          item.imgPath = ItemList.getItemImage(item.id);
          item.shopSellPrice = Number(item.shopSellPrice).toLocaleString();
          item.requiredCraftingLevel = (item.requiredCraftingLevel > 999) ? 'Can\'t be crafted' : item.requiredCraftingLevel;
          item.type = Inventory.getItemTypeByIndex(item.type);
          item.category = ItemList.getItemCategoryByIndex(item.category);
          item.material = ItemList.getItemMaterialByIndex(item.material);
        });
      }

      this.dataLoading = false;
    }

    private mounted() {
      this.loadItems();
    }

    private sortByName(items: any[]): any[] {
      return items.sort((i1, i2) => ItemList.sortByCompaeFunc(i1.name, i2.name));
    }

    private static sortByCompaeFunc(i1: any, i2: any): number {
      if (i1 > i2) {
        return 1;
      }

      if (i1 < i2) {
        return -1;
      }

      return 0;
    }

    public static getItemImage(itemId: string): string {
        return `/assets/imgs/items/${itemId}.png`;
    }

    public static getItemCategoryByIndex(categoryIndex: number): string {
      switch (categoryIndex) {
          case 0: return 'Weapon';
          case 1: return 'Armor';
          case 2: return 'Ring';
          case 3: return 'Amulet';
          case 4: return 'Food';
          case 5: return 'Potion';
          case 6: return 'Pet';
          case 7: return 'Resource';
          default: return 'N/A';
      }
    }

    public static getItemMaterialByIndex(materialIndex: number): string {
      switch (materialIndex) {
          case 0: return 'None';
          case 1: return 'Bronze';
          case 2: return 'Iron';
          case 3: return 'Steel';
          case 4: return 'Black';
          case 5: return 'Mithril';
          case 6: return 'Adamantite';
          case 7: return 'Rune';
          case 8: return 'Dragon';
          case 9: return 'Abraxas';
          case 10: return 'Phantom';
          default: return 'N/A' + materialIndex;
      }
    }
  }
</script>

<style scoped>

  table.items-list {
    width: 100%;
    border-collapse: collapse;
  }

  th, td {
    text-align: right;
    padding: 10px 15px;
    border: 1px solid #eeeeee;
  }

  table.items-list tr {
    border: none;
  }

  table.items-list th:nth-of-type(2),
  table.items-list td:nth-of-type(2) {
    text-align: left;
  }

  table.items-list tr:nth-of-type(even) {
    background-color: #f6f6f6;
  }

  table.items-list tr.isMe {
    background-color: #6acc91;
    color: white;
  }

  .items {
      margin-top: 140px;
      flex-grow:1;
  }

  .ravenCoins {
    margin-bottom: -1.5px;
    height: 100%;
    margin-right: 1px;
  }

</style>
