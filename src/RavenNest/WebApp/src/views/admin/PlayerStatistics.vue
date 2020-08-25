<template>
<div class="modal-background" @click="close" v-show="visible">
  <div class="modal-statistics" @click.stop>
      <h1>Statistics for {{playerName}}</h1>   

    <div class="rows">
      <div v-for="stat in getAllStats()" class="row" :key="stat.name">       
          <div class="label">{{stat.name}}</div>
          <div class="value">{{stat.value}}</div>                
      </div>
    </div>
              
  </div>
 </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
    Prop
  } from 'vue-property-decorator';
  import router from 'vue-router';
import { Player, Statistics } from '@/logic/models';

  @Component({
    name: 'PlayerStatistics',
    components: { },
  })
  export default class PlayerStatistics extends Vue {

    @Prop(Player) player! : Player;
    @Prop(Boolean) visible! : boolean;

    private isVisible: boolean = false;

    public getAllStats(): StatisticItem[] {
        const stats : StatisticItem[] = []
        for(const stat in this.statistics) {
          if (stat === 'id') continue;
          const statItem = new StatisticItem();
          statItem.name = this.getDisplayName(stat);
          statItem.value = (this.statistics as any)[stat];
          stats.push(statItem);
        }
        return stats;
    }

    public get statistics(): Statistics | null {
      if (!this.player) return null;
      return this.player.statistics;
    }

    public getStat(name:string):number {
      const stats = this.statistics;
      if (!stats) return 0;
      return (stats as any)[name];
    }

    public close() {
      this.visible = false;
      this.$emit('closed');
    }

    public get playerName(): string {
      if (!this.player) return '';
      return this.player.name;
    }

    private getDisplayName(name:string): string {
      let output = name.charAt(0).toUpperCase();
      for(let i = 1; i < name.length; ++i) {
        const letter = name.charAt(i);
        if (letter.toUpperCase() === letter) {
          output += ' ';
        }
        output += letter;
      }
      return output;
    }

  }

  class StatisticItem {
    public name: string = '';
    public value: number = 0;
  }

</script>

<style scoped lang="scss">  

  h1 {
    margin-bottom: 30px;
    margin-top: 35px;
  }
  .modal-background {
    position: fixed;
    left: 0;
    top:0;
    right:0;
    bottom:0;
    background-color: rgba(0, 0, 0, .5);
    transition: opacity .3s ease;
  }

  .modal-statistics {
    position: fixed;
    max-width: 100%;
    max-height: 100%;
    background-color: #fff;
    left: 50%;
    top: 50%;
    -webkit-transform: translate(-50%,-50%);
    transform: translate(-50%,-50%);
    padding: 10px 35px 35px 35px;
    transition: all .3s ease;
  }

.label {
    min-width: 230px;
    text-align: left;
}

.value {
    font-weight: 500;
    margin-left: 10px;
    text-align: left;
    min-width: 130px;
}

.row {
    display: flex;
    flex-flow: row;
    justify-content: space-between;
    padding: 5px;
}

.rows {
    display: flex;
    flex-flow: row;
    flex-wrap: wrap;
    justify-content: space-around;
}

</style>