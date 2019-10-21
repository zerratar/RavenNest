<template>
    <div class="character">
      <h1>My character</h1>
      

      <div class="stats-row">
        <div class="stats-label">{{playerName}}</div>
        <div class="stats-value">{{combatLevel}}</div>
      </div>


      <h2></h2>
      


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
  import Requests from '../requests';
  import router from 'vue-router';

  export class GameMath {
    private static expTable: number[] = [];
    public static maxLevel: number = 170;

    public static levelToExp(level: number): number {
      return level - 2 < 0 ? 0 : GameMath.expTable[level - 2];
    }

    public static expTolevel(exp: number): number {
        for (let level = 0; level < GameMath.maxLevel - 1; level++) {
          if (exp >= GameMath.expTable[level])
              continue;
          return (level + 1);
        }
        return GameMath.maxLevel;
    }

    private static calculateExpTable(): void {
      if (GameMath.expTable.length > 0) {
        return;
      }
      let totalExp = 0;
      for (let levelIndex = 0; levelIndex < GameMath.maxLevel; levelIndex++) {
          let level = levelIndex + 1;
          let levelExp = (level + (300 * Math.pow(2, (level / 7))));
          totalExp += levelExp;
          GameMath.expTable[levelIndex] = ((totalExp & 0xffffffffc) / 4);
      }
    }
  }

  export class CharacterSkill {
    public level: number;
    public expForNextLevel: number;
    public nextLevel: number;
    public expPercent: number;    
    
    constructor(
      public readonly name: string,
      public readonly experience:number) {      
        this.level = GameMath.expTolevel(experience);
        const min = GameMath.levelToExp(this.level);
        this.expForNextLevel = GameMath.levelToExp(this.level + 1);
        const currentExp = experience - min;
        this.nextLevel = this.expForNextLevel - min;
        this.expPercent = currentExp / this.nextLevel;
      }
  }

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

      this.getPlayerDataAsync();
    }

    get playerName(): string {
      if (!this.playerData) return '';
      return this.playerData.name;
    }
    get combatLevel(): number {
      const attack = this.getSkill("attack").level;
      const defense = this.getSkill("defense").level;
      const strength = this.getSkill("strength").level;
      const health = this.getSkill("health").level;
      const magic = this.getSkill("magic").level;
      const ranged = this.getSkill("ranged").level;

      return ((attack + defense + strength + health) / 4) 
        + (magic / 8) + (ranged / 8);
    }

    getSkill(name: string): any { // CharacterSkill
        // return this.skills.find(x => x.name.toLowerCase() === name.toLowerCase());
        return this.skills[name];
    }

    async getPlayerDataAsync() {
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
  }
</script>

<style scoped>
.character {
    margin-top: 92px;
}
</style>