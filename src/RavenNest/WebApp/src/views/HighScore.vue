<template>
  <div class="highscore">
    <h1>HighScore - Top 100</h1>
    <div class="skill-selector">
      <a v-for="skill in skills" :key="skill.name" :href="skill.link"
        v-bind:class="{active: skill.active}">{{skill.name}}</a>
    </div>

    <table class="highscore-list">
      <thead>
        <tr>
          <th>Rank</th>
          <th>Player</th>
          <th>Level</th>
          <th>Experience</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="player in players" :key="player.playerName" v-bind:class="{isMe: player.isMe}">
          <td class='player-rank'>{{player.rank}}</td>
          <td class='player-name'>{{player.playerName}}</td>
          <td class='player-level'>{{player.level}}</td>
          <td class='player-experience'>{{player.experience}}</td>
        </tr>
      </tbody>
    </table>
    <div v-if="dataLoading" class="loader">
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
  import router from 'vue-router';
  import Skill from '../skill';
  import Requests from '../requests';
  import { SessionState } from '@/App.vue';

  @Component({})
  export default class HighScore extends Vue {
    private dataLoading: boolean = true;
    private selectedSkill: Skill | null = null;
    private players: any[] = [];
    private skills: Skill[] = [
      new Skill('All', false),
      new Skill('Attack', false),
      new Skill('Defense', false),
      new Skill('Strength', false),
      new Skill('Health', false),
      new Skill('Magic', false),
      new Skill('Ranged', false),
      new Skill('Woodcutting', false),
      new Skill('Fishing', false),
      new Skill('Mining', false),
      new Skill('Crafting', false),
      new Skill('Cooking', false),
      new Skill('Farming', false),
      new Skill('Slayer', false),
      new Skill('Sailing', false),
    ];

    selectHighScore(skill: Skill): void {
      skill.active = true;
    }

    private async loadHighScore(skill: string) {
      this.dataLoading = true;
      const url = `api/highscore/${skill}`; // https://www.ravenfall.stream/
      const result = await Requests.sendAsync(url);
      if (result.ok) {
        this.players = (await result.json()).players;
        const sessionState = SessionState.get();
        this.players.forEach(player => {
          if (sessionState != null && sessionState.authenticated) {
            player.isMe = player.playerName == sessionState.userName;
          } else {
            player.isMe = false;
          }
        });
      }

      if (this.selectedSkill != null) {
        this.selectedSkill.active = false;
      }

      skill = skill === '' ? 'all' : skill;
      const targetSkill = this.skills.find(x => x.name.toLowerCase() === skill.toLowerCase());
      if (targetSkill != null) {
        targetSkill.active = true;
        this.selectedSkill = targetSkill;
      }
      this.dataLoading = false;
    }

    private mounted() {
      const loadByHash = () => {
        const hash = window.location.hash;
        if (hash && hash.length > 0) {
          const skill = hash.substring(1);
          this.loadHighScore(skill.toLowerCase() === 'all' ? '' : skill);
        } else {
          this.loadHighScore('');
        }
      };

      window.onhashchange = (e:any) => {
        loadByHash();
      };

      loadByHash();
    }
  }
</script>

<style scoped>

  .skill-selector a {
    padding: 5px 15px;
    margin-bottom: 15px;
    display: inline-block;
    text-decoration: none;
    cursor: pointer;
    color: #333;
    transition: all 150ms ease-in-out;
    border-bottom: 1px solid white;
  }

  .skill-selector a:hover {
    color: #3498db;
  }

  .skill-selector a::after {
    content: "";
    height: 1px;
    width: 0%;
    border-bottom: 1px solid white;
    position: relative;
    display: block;
    line-height: 2px;
    color: white;
    font-size: 0pt;
    top: 0px;
  }

  .skill-selector {
      margin-top: 25px;
  }

  .skill-selector a.active {
      background-color: #0e0e0f;
      color: white;
  }
  a.active::after,
  .skill-selector a:hover::after {
    content: "";
    height: 1px;
    width: 0%;
    border-bottom: 1px solid #333;
    position: relative;
    display: block;
    line-height: 2px;
    color: white;
    font-size: 0pt;
    top: 0px;
    animation: selection linear 0.125s;
    animation-fill-mode: forwards;
  }

  table.highscore-list {
    width: 100%;
    border-collapse: collapse;
  }

  th,
  td {
    text-align: right;
    padding: 10px 15px;
    border: 1px solid #eeeeee;
  }

  table.highscore-list tr {
    border: none;
  }

  table.highscore-list th:nth-of-type(2),
  table.highscore-list td:nth-of-type(2) {
    text-align: left;
  }

  table.highscore-list tr:nth-of-type(even) {
    background-color: #f6f6f6;
  }

  table.highscore-list tr.isMe {
    background-color: #6acc91;
    color: white;
  }

  /*
    Loading CSS
*/

  .highscore {
      margin-top: 140px;
      flex-grow:1;
  }

</style>