<template>
  <div class="marketplace">
    <h1>Ravenfall Marketplace</h1>
    <br />

    <table class="marketplace-list">
      <thead>
        <tr>
          <th></th>
          <th>Name</th>
          <!-- <th>Seller</th> -->
          <th>Weapon Aim</th>
          <th>Weapon Power</th>
          <th>Armor Power</th>
          <th>Attack Level</th>
          <th>Defense Level</th>
          <th>Category</th>
          <th>Item Type</th>
          <th>Material Type</th>
          <th>Available Amount</th>
          <th>Asking Price</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="entry in marketEntries" :key="entry.id">
          <td><img v-bind:src="entry.item.imgPath" style="width: 40px"/></td>
          <td class='marketplace'>{{entry.item.name}}</td>
          <!-- <td class='marketplace'></td> -->
          <td class='marketplace'>{{entry.item.weaponAim}}</td>
          <td class='marketplace'>{{entry.item.weaponPower}}</td>
          <td class='marketplace'>{{entry.item.armorPower}}</td>
          <td class='marketplace'>{{entry.item.requiredAttackLevel}}</td>
          <td class='marketplace'>{{entry.item.requiredDefenseLevel}}</td>
          <td class='marketplace'>{{entry.item.category}}</td>
          <td class='marketplace'>{{entry.item.type}}</td>
          <td class='marketplace'>{{entry.item.material}}</td>
          <td class='marketplace'>{{entry.amount}}</td>
          <td class='marketplace'><img class="ravenCoins" src="/favicon.ico" />{{entry.marketplacePrice}}</td>
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
  import ItemList from './ItemList.vue';

  @Component({})
  export default class Marketplace extends Vue {
    private dataLoading: boolean = true;
    private items: any[] = [];
    private resultEntries: any[] = [];
    private marketEntries: any[] = [];

    private mounted() {
      this.loadMarketplace();
    }

    private async loadItems() {
      const url = `api/items`;
      const result = await Requests.sendAsync(url);
      if (result.ok) {
        this.items = await result.json();
        this.items.forEach((item) => {
          item.imgPath = ItemList.getItemImage(item.id, item.tag);
          item.type = Inventory.getItemTypeByIndex(item.type);
          item.category = ItemList.getItemCategoryByIndex(item.category);
          item.material = ItemList.getItemMaterialByIndex(item.material);
        });
      }
    }

    private async loadMarketplace() {
      await this.loadItems();

      const url = `api/marketplace/`;
      const step = 100;
      let start: number = 0;
      let result: any = new Array(step);


      while (result.length >= step) {
        result = new Array(step);
        const requestUrl = url + start + '/' + step;
        const response = await Requests.sendAsync(requestUrl);
        if (response.ok) {
          result = await response.json();
          this.handleMarketplaceResult(result);
        }

        start += step;
      }

      this.marketEntries = this.sortItems(this.resultEntries);
      this.dataLoading = false;
    }

    private handleMarketplaceResult(result: any) {
      result.forEach((entry: any) => {
        entry.item = this.items.filter((e) => e.id === entry.itemId)[0];
        entry.marketplacePrice = Number(entry.pricePerItem).toLocaleString();
      });

      this.resultEntries = this.resultEntries.concat(result);
    }

    private sortItems(items: any[]): any[] {
      return items.sort((i1, i2) => Marketplace.sortByCompaeFunc(i1.pricePerItem, i2.pricePerItem));
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
  }
</script>

<style scoped>

  table.marketplace-list {
    width: 100%;
    border-collapse: collapse;
  }

  th, td {
    text-align: right;
    padding: 10px 15px;
    border: 1px solid #eeeeee;
  }

  table.marketplace-list tr {
    border: none;
  }

  table.marketplace-list th:nth-of-type(2),
  table.marketplace-list td:nth-of-type(2) {
    text-align: left;
  }

  table.marketplace-list tr:nth-of-type(even) {
    background-color: #f6f6f6;
  }

  table.marketplace-list tr.isMe {
    background-color: #6acc91;
    color: white;
  }

  .marketplace {
      margin-top: 140px;
      flex-grow:1;
  }

  .ravenCoins {
    margin-bottom: -1.5px;
    height: 100%;
    margin-right: 1px;
  }

</style>
