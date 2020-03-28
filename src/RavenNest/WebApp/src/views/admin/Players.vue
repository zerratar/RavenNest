<template>
    <div class="player-list">
     
      <div class="pagenation">    
        <button v-on:click="previousPage()"><i class="fas fa-chevron-left"></i></button>
        <div class="current-page">{{pageNumber()}}</div>&nbsp;/&nbsp;
        <div class="total-pages">{{pageCount()}}</div>
        <button v-on:click="nextPage()"><i class="fas fa-chevron-right"></i></button>
        <div class="total-players"><span>{{offset()}}</span>/<span>{{playerCount()}}</span></div>
      </div>

      <table class="player-table">
        <thead>
          <tr>
            <th>Id</th>
            <th>UserName</th>
            <th>Name</th>
            <th>Admin</th>
            <th>Moderator</th>
            <th></th>
            <th></th>
            <th></th>
            <th></th>
            <th></th>
            <th></th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr class="player-row" v-for="player in getPlayers()" :key="player.userId">
            <td>{{player.userId}}</td>
            <td>{{player.userName}}</td>
            <td>

              <span v-if="editingName(player.userId)">
                <input :value="player.name"/>
                <a href="#" v-on:click="applyEditName(player.userId)"><i class="fas fa-check"></i></a>
                <a href="#" v-on:click="cancelEditName(player.userId)"><i class="fas fa-times"></i></a>
              </span>

              <span v-if="!editingName(player.userId)">
                {{player.name}}
                <a href="#" v-on:click="editName(player.userId)" ><i class="fas fa-pencil-alt"></i></a>
              </span>
              
            </td>
            <td>{{player.isAdmin}}</td>            
            <td>{{player.isModerator}}</td>
            <td><button class="link-button" v-on:click="statistics(player.userId)">statistics</button></td>
            <td><button class="link-button" v-on:click="resources(player.userId)">resources</button></td>
            <td><button class="link-button" v-on:click="skills(player.userId)">skills</button></td>
            <td><button class="link-button" v-on:click="state(player.userId)">state</button></td>
            <td><button class="link-button" v-on:click="inventory(player.userId)">inventory</button></td>
            <td><button class="link-button" v-on:click="kick(player.userId)">kick</button></td>
            <td><button class="link-button" v-on:click="suspend(player.userId)">suspend</button></td>
          </tr>
        </tbody>
      </table>

    
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
  import GameMath from '@/logic/game-math';
  import { CharacterSkill, Player } from '@/logic/models';
  import ItemRepository from '@/logic/item-repository';
  import MyPlayer from '@/logic/my-player';
  import Requests from '@/requests';
  import router from 'vue-router';
import PlayerRepository from '../../logic/player-repository';
 

  @Component({})
  export default class Players extends Vue {

    private currentPage: number = 0;
    private loadCounter: number = 0;
    private playerEdit: Map<string,PlayerEdit> = new Map<string, PlayerEdit>();

    mounted() {      
      const sessionState = SessionState.get();                  

      if (sessionState !== null && !sessionState.authenticated && !sessionState.administrator) {
        this.$router.push("/login");
        return;
      }

      this.loadPlayerPageAsync(0).then(()=>{
        this.$forceUpdate();
      });
    }

    editingName(userId: string): boolean {
      const edit = this.playerEdit.get(userId);
      return !!edit && edit.isEditing;
    }

    applyEditName(userId: string) {
      const edit = this.playerEdit.get(userId);
      if (!edit) return;
      edit.isEditing = false;
    }

    cancelEditName(userId: string) {
      const edit = this.playerEdit.get(userId);
      if (!edit) return;
      const player = PlayerRepository.getPlayer(userId);
      player.name = edit.name;
      edit.isEditing = false;
    }

    editName(userId: string) {

    }

    statistics(userId: string) {
      console.log(`statistics for user: ${userId}`);
    }

    skills(userId: string) {
      console.log(`skills for user: ${userId}`);
    }

    state(userId: string) {
      console.log(`state for user: ${userId}`);
    }

    resources(userId: string) {
      console.log(`resources for user: ${userId}`);
    }

    inventory(userId: string) {
      console.log(`inventory for user: ${userId}`);
    }

    kick(userId: string) {
      console.log(`kick user: ${userId}`);
    }

    suspend(userId: string) {
      console.log(`suspend user: ${userId}`);
    }

    previousPage() {
      if (!PlayerRepository) return;
      this.currentPage = Math.max(this.currentPage - 1, 0);
      this.loadPlayerPageAsync(this.currentPage).then(()=>{
        this.$forceUpdate();
      });
    }

    nextPage() {
      if (!PlayerRepository) return;
      this.currentPage = Math.min(this.currentPage + 1, PlayerRepository.getPageCount() - 1);
      this.loadPlayerPageAsync(this.currentPage).then(()=>{
        this.$forceUpdate();
      });
    }

    getPlayers(): Player[] {
      if (!PlayerRepository) return [];
      return PlayerRepository.getPlayers(this.currentPage);
    }

    playerCount(): number {
      if (!PlayerRepository) return 0;
      return PlayerRepository.getTotalCount();
    }
    
    offset(): number {
      if (!PlayerRepository) return 0;
      return PlayerRepository.getOffset(this.currentPage);
    }

    pageCount(): number {
      if (!PlayerRepository) return 0;
      return PlayerRepository.getPageCount();
    }

    pageNumber(): number {
      return this.currentPage + 1;
    }

    private async loadPlayerPageAsync(pageIndex: number) {
      ++this.loadCounter;
      await PlayerRepository.loadPlayersAsync(pageIndex);
      --this.loadCounter;  
      this.$forceUpdate();
    }

    public get isLoading(): boolean {
      return this.loadCounter > 0;
    }
  }

class PlayerEdit {
  public userId: string = "";
  public name: string = "";
  public isEditing: boolean = false;
}

</script>

<style scoped>

.link-button {

}


.pagenation {
  display: flex;
  flex-flow: row;
}

table.player-table {
    width: 100%;
    border-collapse: collapse;
}

  th,
  td {
    text-align: right;
    padding: 10px 15px;
    border: 1px solid #eeeeee;
  }

  table.player-table tr {
    border: none;
  }

  table.player-table th:nth-of-type(2),
  table.player-table td:nth-of-type(2) {
    text-align: left;
  }

  table.player-table tr:nth-of-type(even) {
    background-color: #f6f6f6;
  }

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