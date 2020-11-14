<template>
    <div class="sessions">     
      <table class="game-sessions" :key="revision">
        <thead>
          <tr>
            <td>Id</td>
            <td>User Id</td>
            <td>Twitch User Id</td>
            <td>UserName</td>
            <td>Has Admin Priveleges</td>
            <td>Has Mod Priveleges</td>
            <td>Players</td>
            <td>Started</td>
            <td>Last updated</td>
            <td>Status</td>
          </tr>
        </thead>
        <tbody>
          <tr class="game-session" v-for="session in getSessions()" :key="session.id">
            <td>{{session.id}}</td>
            <td>{{session.userId}}</td>
            <td>{{session.twitchUserId}}</td>
            <td><a :href="streamerUrl(session.userName)" target="_blank">{{session.userName}}</a></td>
            <td>{{session.adminPrivileges}}</td>
            <td>{{session.modPrivileges}}</td>
            <td>{{playerCount(session)}}</td>
            <td>{{session.started}}</td>
            <td>{{session.updated}}</td>
            <td>{{session.status}}</td>
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
  import { Component, Vue } from 'vue-property-decorator';
  import { SessionState } from '@/App.vue';
  import GameMath from '@/logic/game-math';
  import { GameSession } from '@/logic/models';
  import SessionRepository from '@/logic/session-repository';
  import MyPlayer from '@/logic/my-player';
  import Requests from '@/requests';
  import router from 'vue-router';
  import AdminService from '@/logic/admin-service';

  @Component({})
  export default class Sessions extends Vue {

    private loadCounter: number = 0;
    private currentPage: number = 0;
    private sortOrder: string = '';
    private query: string = '';
    private revision: number = 0;

    public streamerUrl(name: string): string {
      return `https://www.twitch.tv/${name}`;
    }

    public playerCount(session:GameSession):number {
      if (session.players != null){
        return session.players.length;
      }
      return 0;      
    }

    private mounted() {
      const sessionState = SessionState.get();

      if (sessionState !== null && !sessionState.authenticated && !sessionState.administrator) {
        this.$router.push('/login');
        return;
      }

      this.loadSessionPageAsync(this.currentPage);
      setTimeout(() => ++this.revision, 500);
    }

    private getSessions(): GameSession[] {
      if (!SessionRepository) return [];
      return SessionRepository.getSessions(this.currentPage, this.sortOrder, this.query);
    }

    private loadSessionPageAsync(pageIndex: number) {
      ++this.loadCounter;

      SessionRepository.loadSessionsAsync(
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
</script>

<style scoped>

table.game-sessions {
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

  table.game-sessions tr {
    border: none;
  }

  table.game-sessions th:nth-of-type(2),
  table.game-sessions td:nth-of-type(2) {
    text-align: left;
  }

  table.game-sessions tr:nth-of-type(even) {
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
