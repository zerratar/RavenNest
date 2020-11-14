<template>
    <div class="player-list">


      <div class="pagenation">    
        <div class="page-navigation">
          <button class="link-button" @click="previousPage()"><i class="fas fa-chevron-left"></i></button>
          <div class="pages">
            <div class="current-page">{{getPageNumber()}}</div>&nbsp;/&nbsp;
            <div class="total-pages">{{getPageCount()}}</div>
          </div>
          <button class="link-button" @click="nextPage()"><i class="fas fa-chevron-right"></i></button>
        </div>
        <div class="search-bar">
          <input class="search-input" v-model="query" @input="filter()" placeholder="Search for id, username or name"/>
        </div>             
        <div class="total-players">Showing <span>{{getOffset()}}</span>&nbsp;of&nbsp;<span>{{getPlayerCount()}}</span> players</div>
      </div>

      <table class="player-table" :key="revision">
        <thead>
          <tr>
            <th @click="orderBy('Id')">Id <i :class="getSortOrderClass('Id')"></i></th>
            <th @click="orderBy('UserName')">UserName <i :class="getSortOrderClass('UserName')"></i></th>
            <th @click="orderBy('Name')">Name <i :class="getSortOrderClass('Name')"></i></th>
            <th @click="orderBy('SessionName')">Session <i :class="getSortOrderClass('SessionName')"></i></th>
            <th @click="orderBy('IsAdmin')">Admin <i :class="getSortOrderClass('IsAdmin')"></i></th>
            <th @click="orderBy('IsModerator')">Moderator <i :class="getSortOrderClass('IsModerator')"></i></th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr class="player-row" v-for="player in getPlayers()" :key="player.id">
            <td>{{player.userId}}</td>
            <td>{{player.userName}}<span class='player-character-index' alt='Character Number'>#{{player.characterIndex}}</span></td>
            <td>

              <span v-if="editingName(player.userId)">
                <input class="table-edit" v-model="player.name"/>
                <button class="link-button" @click="applyEditName(player.id)"><i class="fas fa-check"></i></button>
                <button class="link-button" @click="cancelEditName(player.id)"><i class="fas fa-times"></i></button>
              </span>

              <span v-if="!editingName(player.userId)">
                {{player.name}}
                <button class="link-button" @click="editName(player.id)" ><i class="fas fa-pencil-alt"></i></button>
              </span>
              
            </td>
            <td>{{player.sessionName}}</td>
            <td>{{player.isAdmin}}</td>            
            <td>{{player.isModerator}}</td>
            <td>
              <button class="link-button" @click="showStatistics(player.id)">statistics</button>
              <button class="link-button" @click="showResources(player.id)">resources</button>
              <button class="link-button" @click="showSkills(player.id)">skills</button>
              <button class="link-button" @click="showState(player.id)">state</button>
              <button class="link-button" @click="showInventory(player.id)">inventory</button>
              <button class="link-button" @click="mergePlayer(player.userId)">merge</button>
              <button class="link-button" @click="resetPassword(player.userId)">reset pass</button>
              <button class="link-button" @click="kickPlayer(player.id)">kick</button>
              <button class="link-button" @click="suspend(player.userId)">suspend</button>
            </td>
          </tr>
        </tbody>
      </table>
           
      <div class="pagenation">    
        <div class="page-navigation">
          <button class="link-button" @click="previousPage()"><i class="fas fa-chevron-left"></i></button>
          <div class="pages">
            <div class="current-page">{{getPageNumber()}}</div>&nbsp;/&nbsp;
            <div class="total-pages">{{getPageCount()}}</div>
          </div>
          <button class="link-button" @click="nextPage()"><i class="fas fa-chevron-right"></i></button>
        </div>
        <div class="total-players">Showing <span>{{getOffset()}}</span>&nbsp;of&nbsp;<span>{{getPlayerCount()}}</span> players</div>
      </div>

      <player-statistics :key="revision" v-on:closed="hideModals" :visible="getStatisticsVisible()" :player="getFocusedPlayer()"></player-statistics>
      <player-resources :key="revision" v-on:closed="hideModals" :visible="getResourcesVisible()" :player="getFocusedPlayer()"></player-resources>
      <player-skills :key="revision" v-on:closed="hideModals" :visible="getSkillsVisible()" :player="getFocusedPlayer()"></player-skills>
      <player-state :key="revision" v-on:closed="hideModals" :visible="getStateVisible()" :player="getFocusedPlayer()"></player-state>
      <player-inventory :key="revision" v-on:closed="hideModals" :visible="getInventoryVisible()" :player="getFocusedPlayer()"></player-inventory>

      <div v-if="isLoading" class="loader">
        <div class="lds-ripple">
          <div></div>
          <div></div>
        </div>
      </div>
    </div>

</template>

<script lang="ts">
  import { Component, Vue } from 'vue-property-decorator';
  import { SessionState } from '@/App.vue';
  import GameMath from '@/logic/game-math';
  import { CharacterSkill, Player } from '@/logic/models';
  import ItemRepository from '@/logic/item-repository';
  import MyPlayer from '@/logic/my-player';
  import Requests from '@/requests';
  import router from 'vue-router';
  import PlayerRepository from '../../logic/player-repository';
  import AdminService from '../../logic/admin-service';

  import PlayerStatistics from './PlayerStatistics.vue';
  import PlayerInventory from './PlayerInventory.vue';
  import PlayerState from './PlayerState.vue';
  import PlayerResources from './PlayerResources.vue';
  import PlayerSkills from './PlayerSkills.vue';


  @Component({
    components: {
      PlayerStatistics, PlayerResources, PlayerSkills,
      PlayerState, PlayerInventory,
    },
  })
  export default class Players extends Vue {

    private filterTimeout: number = 0;
    private currentPage: number = 0;
    private loadCounter: number = 0;
    private playerEdit: Map<string, PlayerEdit> = new Map<string, PlayerEdit>();
    private sortOrder: string = '';
    private query: string = '';
    private revision: number = 0;

    private isStatisticsVisible: boolean = false;
    private isResourcesVisible: boolean  = false;
    private isSkillsVisible: boolean  = false;
    private isInventoryVisible: boolean  = false;
    private isStateVisible: boolean  = false;

    private playerInFocus: Player | null = null;

    private changePasswordUserId: string = '';
    private newPassword: string = '';

    public getStatisticsVisible(): boolean { return this.isStatisticsVisible; }
    public getResourcesVisible(): boolean { return this.isResourcesVisible; }
    public getSkillsVisible(): boolean { return this.isSkillsVisible; }
    public getInventoryVisible(): boolean { return this.isInventoryVisible; }
    public getStateVisible(): boolean { return this.isStateVisible; }
    public getFocusedPlayer(): Player|null { return this.playerInFocus; }

    private mounted() {
      const sessionState = SessionState.get();

      if (sessionState !== null && !sessionState.authenticated && !sessionState.administrator) {
        this.$router.push('/login');
        return;
      }
      this.previousPage();
      setTimeout(() => ++this.revision, 500);
    }

    private filter() {
      this.hideModals();
      if (this.filterTimeout) clearTimeout(this.filterTimeout);
      this.filterTimeout = setTimeout(() => this.applyFilter(), 250);
    }

    private applyFilter() {
      this.currentPage = 0;
      this.hideModals();
      this.loadPlayerPageAsync(this.currentPage);
    }

    private hideModals() {
      this.isStatisticsVisible = false;
      this.isResourcesVisible = false;
      this.isSkillsVisible = false;
      this.isInventoryVisible = false;
      this.isStateVisible = false;
    }

    private getSortOrderClass(order: string) {
      return this.getSortOrder(order) ? 'fas fa-chevron-up' : 'fas fa-chevron-down';
    }

    private getSortOrder(order: string): boolean {
      return this.sortOrder.substring(1) === order && (this.sortOrder.charAt(0) === '+' || this.sortOrder.charAt(0) === '1');
    }

    private orderBy(order: string) {
      const ascending = this.getSortOrder(order);
      this.sortOrder = (ascending ? '0' : '1') + order;
      this.applyFilter();
    }

    private editingName(characterId: string): boolean {
      const edit = this.playerEdit.get(characterId);
      return !!edit && edit.isEditing;
    }

    private applyEditName(characterId: string) {
      this.hideModals();
      const edit = this.playerEdit.get(characterId);
      if (!edit) return;
      const player = PlayerRepository.getPlayerById(characterId);
      if (!player) {
        console.error('no user found for editing name (userId: ${characterId})');
        return;
      }

      AdminService.updatePlayerName(characterId, player.name).then((res) => {
        if (res) {
          edit.name = player.name;
          edit.isEditing = false;
          ++this.revision;
        }
      });
    }

    private cancelEditName(characterId: string) {
      this.hideModals();
      const edit = this.playerEdit.get(characterId);
      if (!edit) return;
      const player = PlayerRepository.getPlayerById(characterId);
      if (!player) {
        console.error('no user found for editing name (userId: ${characterId})');
        return;
      }
      player.name = edit.name;
      edit.isEditing = false;
      ++this.revision;
    }

    private editName(characterId: string) {
      this.hideModals();
      let edit = this.playerEdit.get(characterId);
      const player = PlayerRepository.getPlayerById(characterId);
      if (!player) {
        console.error('no user found for editing name (userId: ${userId})');
        return;
      }

      if (!edit) {
        edit = new PlayerEdit();
        edit.userId = characterId;
        edit.name = player.name;
        this.playerEdit.set(characterId, edit);
      }

      edit.isEditing = true;
      ++this.revision;
    }

    // tslint:disable-next-line:ban-types
    private showModal(id: string, action: Function) {
      this.hideModals();
      this.playerInFocus = PlayerRepository.getPlayerById(id);
      action();
      ++this.revision;
    }

    private showStatistics(characterId: string) {
      this.showModal(characterId, () => this.isStatisticsVisible = true);
    }

    private showSkills(characterId: string) {
      this.showModal(characterId, () => this.isSkillsVisible = true);
    }

    private showState(characterId: string) {
      this.showModal(characterId, () => this.isStateVisible = true);
    }

    private showResources(characterId: string) {
      this.showModal(characterId, () => this.isResourcesVisible = true);
    }

    private showInventory(characterId: string) {
      this.showModal(characterId, () => {
        this.isInventoryVisible = true;
        const player = this.getFocusedPlayer();
        if (!player) return;

        console.log('Showing inventory for player: ' + player.name);
      });
    }

    private kickPlayer(userId: string) {
      if (!confirm('Are you sure you want to kick this player?')) return;

      AdminService.kickPlayer(userId).then((res) => {
        if (res) {
          ++this.revision;
        }
      });
    }

    private mergePlayer(userId: string) {
      if (!confirm('Are you sure you want to merge this player?')) return;

      AdminService.mergePlayer(userId).then((res) => {
        if (res) {
          ++this.revision;
          this.query = userId;
          this.filter();
        }
      });
    }

    private resetPassword(userId: string) {
      const password: string = this.newPassword;
      if (!confirm('Are you sure you want to reset the password?')) return;

      AdminService.resetPassword(userId).then((res) => {
        if (res) {
          ++this.revision;
        }
      });
    }

    private suspend(userId: string) {
      if (!confirm('Are you sure you want to suspend this player?')) return;
      console.log(`suspend user: ${userId}`);
    }

    private previousPage() {
      ++this.revision;
      this.currentPage = Math.max(this.currentPage - 1, 0);
      this.loadPlayerPageAsync(this.currentPage);
    }

    private nextPage() {
      ++this.revision;
      this.currentPage = Math.min(this.currentPage + 1, PlayerRepository.getPageCount() - 1);
      this.loadPlayerPageAsync(this.currentPage);
    }

    private getPlayers(): Player[] {
      if (!PlayerRepository) return [];
      return PlayerRepository.getPlayers(this.currentPage, this.sortOrder, this.query);
    }

    private getPlayerCount(): number {
      if (!PlayerRepository) return 0;
      return PlayerRepository.getTotalCount();
    }

    private getPageCount(): number {
      if (!PlayerRepository) return 0;
      return PlayerRepository.getPageCount();
    }

    private getOffset(): number {
      if (!PlayerRepository) return 0;
      return PlayerRepository.getOffset(this.currentPage);
    }

    private getPageNumber(): number {
      return this.currentPage + 1;
    }

    private loadPlayerPageAsync(pageIndex: number) {
      ++this.loadCounter;

      PlayerRepository.loadPlayersAsync(
          pageIndex,
          this.sortOrder,
          this.query).then(() => {
            --this.loadCounter;
            ++this.revision;
          });
    }

    public get isLoading(): boolean {
      return this.loadCounter > 0;
    }
  }

  class PlayerEdit {
    public userId: string = '';
    public name: string = '';
    public isEditing: boolean = false;
  }

</script>

<style lang="scss" scoped>
.player-character-index {
  margin-left: 5px;
  display: inline-block;
  color: #a2a2a2;
  font-size: 9pt;
}

input.table-edit {
    padding: 3px;
    margin: 2px;
}

button.link-button {
    background-color: #0e0e0f;
    color: #fff;
    border: 0;
    padding: 5px 10px;
    cursor: pointer;
    margin: 2px;
}

input.search-input {
    padding: 10px 15px;
    border-radius: 10px;
    border: 1px solid #d5d5d5;
    width: 500px;
    max-width: 100%;
    box-sizing: border-box;
    outline: none;
    font-size: 13pt;
}

.pagenation {
  display: flex;
  flex-flow: row;
  justify-content: space-between;
  margin-top: 15px;
  margin-bottom: 10px;
  margin-left: 25px;
  margin-right: 25px;
}
.page-navigation {
  display: flex;
  flex-flow: row;
  align-items: flex-end;
  height: 35px;
}
.pages {
  display: flex;
  line-height: 17px;
  margin: 5px;
}
table.player-table {
    width: 100%;
    border-collapse: collapse;
}

  th {
    cursor: pointer;
    user-select: none;
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
  box-sizing: border-box;
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
