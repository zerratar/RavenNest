<template>
<div class="modal-background" @click="close" v-show="visible">
  <div class="modal-players" @click.stop>
      <h1>Player list</h1>   

    <div class="rows">
      <div v-for="player in getAllPlayers()" class="row" :key="player.userName">
          <div class="label">{{player.userName}}</div>  
      </div>
    </div>
              
  </div>
 </div>
</template>

<script lang="ts">
  import { Component, Vue, Prop } from 'vue-property-decorator';
  import router from 'vue-router';
  import { Player, PlayerState, Statistics, PlayerCollection, GameSessionPlayer } from '@/logic/models';

  @Component({
    name: 'PlayerList',
    components: { },
  })
  export default class PlayerList extends Vue {

    @Prop(PlayerCollection) public players!: PlayerCollection;
    @Prop(Boolean) public visible!: boolean;

    private isVisible: boolean = false;

    public getAllPlayers():GameSessionPlayer[] {
      return this.players.players;
    }

    public close() {
      this.visible = false;
      this.$emit('closed');
    }

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

  .modal-players {
    position: fixed;
    max-width: 100%;
    max-height: 100%;
    width: 560px;
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
