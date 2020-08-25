<template>
    <div class="server-management">
      Not implemented.
    </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
  } from 'vue-property-decorator';
  
  import { SessionState } from '@/App.vue';
  import GameMath from '@/logic/game-math';
  import { CharacterSkill } from '@/logic/models';
  import ItemRepository from '@/logic/item-repository';
  import MyPlayer from '@/logic/my-player';
  import Requests from '@/requests';
  import router from 'vue-router';
 

  @Component({})
  export default class Server extends Vue {

    private loadCounter: number = 0;

    private mounted() {
      const sessionState = SessionState.get();

      if (sessionState !== null && !sessionState.authenticated && !sessionState.administrator) {
        this.$router.push('/login');
        return;
      }
    }

    public get isLoading(): boolean {
      return this.loadCounter > 0;
    }
  }
</script>

<style scoped>


.stats-row {
  display: -webkit-box;
  display: -ms-flexbox;
  display: flex;
  padding: 10px 25px;
  -webkit-box-pack: space-evenly;
  -ms-flex-pack: space-evenly;
  justify-content: space-evenly;
  max-width: 100%;
  width: 800px;
  BOX-SIZING: border-box;
  margin-left: auto;
  margin-right: auto;
  border-bottom: 1px solid #f3f3f3;
}

.stats-name { 

}

.stats-combat-level { 
    font-size: 14pt;
    margin-bottom: 10px;
    padding-bottom: 25px;
    border-bottom: 1px solid #e6e6e6;
    display: block;
    width: 100%;
}
.stats-row:nth-of-type(2n+2) {
    background-color: #fafafa;
}
.stats-label {
    width: 190px;
    text-align: left;
    text-transform: uppercase;
}
.stats-progress {
  text-align: right;
  min-width: 120px;
  text-align: center;
  background-color: #f4f4f4;
}
.stats-value {
    width: 190px;
    text-align: right;
}

.stats-progress-value {
    height: 23px;
    position: absolute;
    background-color: #81a1f829;
}
</style>