<template>
    <div class="character">
      
      <h1 class="stats-name">{{getPlayerName()}}</h1>
      <div><span @click="nextIndex()" class='player-character-index' alt='Character Number'>#{{identifier}}/3 (Click to change shown character.)</span></div>
      <div class="stats-row">
        <div class="stats-combat-level">LV : {{getCombatLevel()}}</div>
      </div>

      <nav class="character-navigation">
        <router-link to="/character/skills" class="item">Skills</router-link>
        <router-link to="/character/inventory" class="item">Inventory</router-link>
      </nav>

      <router-view :key="revision"></router-view>

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
  import { CharacterSkill } from '../logic/models';
  import ItemRepository from '../logic/item-repository';
  import MyPlayer from '../logic/my-player';
  import Requests from '../requests';
  import router from 'vue-router';

  @Component({})
  export default class Character extends Vue {
    private loadCounter: number = 0;
    private identifier: string = '1';
    private revision: number = 0;
    private index: number = 0;

    public nextIndex(): void {
      this.index = (++this.index) % 3;
      this.identifier = (this.index + 1).toString();
      this.loadPlayerDataAsync(false);
    }

    public getCombatLevel(): number {
      return MyPlayer.getCombatLevel();
    }

    public getPlayerName(): string {
      return MyPlayer.playerName;
    }

    public getPlayerCharacterIndex(): number {
      return MyPlayer.characterIndex || 1;
    }

    private mounted() {
      const sessionState = SessionState.get();

      if (sessionState !== null && !sessionState.authenticated) {
        this.$router.push('/login');
        return;
      }

      ++this.loadCounter;
      ItemRepository.loadItemsAsync().then(() => {
        --this.loadCounter;
        ++this.revision;
        this.$forceUpdate();
      });

      this.loadPlayerDataAsync(true);
    }

    private loadPlayerDataAsync(reloadRoute: boolean) {
      ++this.loadCounter;
      MyPlayer.getPlayerDataAsync(this.identifier).then(() => {
        --this.loadCounter;
        ++this.revision;
        this.$forceUpdate();
        if (reloadRoute) {
          this.$router.push('/character/skills');
        }
      });
    }

    public get isLoading(): boolean {
      return this.loadCounter > 0;
    }
  }
</script>

<style scoped>
  .player-character-index {
    margin-left: 5px; 
    display: inline-block;
    color: #777777;
    font-size: 11pt;
    cursor: pointer;
    margin-bottom: 15px;
    border-bottom: 1px solid transparent;
  }
  .player-character-index:hover {
    border-bottom: 1px solid #a2a2a2;
  }

.character {
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
