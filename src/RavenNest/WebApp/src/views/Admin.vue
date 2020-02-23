<template>
    <div class="admin">
      
      <h1 class="stats-name">{{getPlayerName()}}</h1>

      <div class="stats-row">
        <div class="stats-combat-level">LV : {{getCombatLevel()}}</div>
      </div>

      <nav class="admin-navigation">
        <router-link to="/admin/Players" class="item">Players</router-link>
        <router-link to="/admin/Items" class="item">Items</router-link>
      </nav>

      <router-view></router-view>

    <div v-if="isLoading" class="loader">
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
  
  import { SessionState } from '@/App.vue';
  import GameMath from '../logic/game-math';
  import { Player } from '../logic/models';
  import PlayerRepository from '../logic/player-repository';
  import Requests from '../requests';
  import router from 'vue-router';


  @Component({})
  export default class Admin extends Vue {
    private loadCounter: number = 0;
    private currentPage: number = 0;

    mounted() {      
      const sessionState = SessionState.get();                  

      if (sessionState !== null && !sessionState.authenticated && !sessionState.administrator) {
        this.$router.push("/login");
        return;
      }

      this.loadPlayerPageAsync(this.currentPage);
    }

    private async loadPlayerPageAsync(pageIndex: number) {
      ++this.loadCounter;
      PlayerRepository.loadPlayersAsync(pageIndex).then(() => {
        --this.loadCounter;  
        this.$forceUpdate();
      });
    }

    public get isLoading(): boolean {
      return this.loadCounter > 0;
    }
  }
</script>

<style scoped>
.admin {
    margin-top: 92px;
    font-family: Heebo,sans-serif;
}
h2 {
    margin-bottom: 10px;
}
a.item.router-link-exact-active.router-link-active {
    background-color: #0e0e0f;
    color: white;
}
a.item {
    padding: 5px 15px;
    margin-bottom: 15px;
    margin-top: 10px;
    display: inline-block;
    text-decoration: none;
    cursor: pointer;
    color: #333;
    -webkit-transition: all .15s ease-in-out;
    transition: all .15s ease-in-out;
    border-bottom: 1px solid #fff;
}

</style>