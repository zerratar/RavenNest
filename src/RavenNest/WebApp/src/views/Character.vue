<template>
    <div class="character">
      <h1>My character</h1>
      

      <div class="stats-row">
        <div class="stats-label">{{playerName}}</div>
        <div class="stats-value">{{combatLevel}}</div>
      </div>

      <div class="stats-row" v-for="skill in getSkills()" :key="skill.name">
        <div class="stats-label">{{skill.name}}</div>
        <div class="stats-progress">{{Math.round(skill.percent*100,2)}}%</div>
        <div class="stats-value">{{skill.level}}</div>
      </div>


      <h3>{{errorMessage}}</h3>

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
  import Requests from '../requests';
  import router from 'vue-router';


  @Component({})
  export default class Character extends Vue {

    private isLoading: boolean = false;
    private playerData: any = null;
    private playerDataJson: string = '';
    private errorMessage: string = '';      

    private skills: any = {};//any[] = [];

    mounted() {      
      const sessionState = SessionState.get();            

      if (sessionState !== null && !sessionState.authenticated) {
        this.$router.push("/login");
        return;
      }
      ItemRepository.loadItemsAsync().then(() => {
        this.itemsLoaded();
      });      
      this.getPlayerDataAsync();
    }

    get playerName(): string {
      if (!this.playerData) return '';
      return this.playerData.name;
    }

    get skillsLoaded(): boolean {
      return 'attack' in this.skills;
    }

    get combatLevel(): number {
      if (!this.skillsLoaded) return 3;
      const attack = this.getSkill("attack").level;
      const defense = this.getSkill("defense").level;
      const strength = this.getSkill("strength").level;
      const health = this.getSkill("health").level;
      const magic = this.getSkill("magic").level;
      const ranged = this.getSkill("ranged").level;

      return ((attack + defense + strength + health) / 4) 
        + (magic / 8) + (ranged / 8);
    }

    getSkill(name: string): CharacterSkill { // CharacterSkill
        // return this.skills.find(x => x.name.toLowerCase() === name.toLowerCase());
        return <CharacterSkill>this.skills[name];
    }

    getSkills(): CharacterSkill[] {
      return [...Object.getOwnPropertyNames(this.skills)
        .map(x => <CharacterSkill>this.skills[x])
        .filter(x => typeof x !== "undefined" && x.name != null && x.name.length > 0)
      ];
    }

    private async getPlayerDataAsync() {
      this.isLoading = true; 
      this.errorMessage = '';

      const url = `api/players`;
      const result = await Requests.sendAsync(url);
      if (result.ok) {        
        this.playerData = (await result.json());
        this.playerDataJson = JSON.stringify(this.playerData);

        this.parsePlayerData(this.playerData);

      } else {
        this.errorMessage = 'Unable to load player data at this time.';
      }
      this.isLoading = false;
    }

    private parsePlayerData(data: any): void {
      for (let propName in data.skills) {
        if (propName == "id" || propName == "revision") {
          continue;
        }

        // this.skills.push(new CharacterSkill(propName, data.skills[propName]));
        this.skills[propName] = new CharacterSkill(propName, data.skills[propName]);
      }
    }

    private itemsLoaded(): void {
      // do something?
      console.log("items loaded.");
    }
  }
</script>

<style scoped>
.character {
    margin-top: 92px;
}

.stats-row {
    display: flex;
    justify-content: space-evenly;
    padding: 10px 25px;
}

.stats-label {

}

.stats-value {

}

</style>