<template>
    <div class="admin">          

      <nav class="admin-navigation">
        <router-link to="/admin/server" class="item">Server Management</router-link>
        <router-link to="/admin/sessions" class="item">Sessions</router-link>
        <router-link to="/admin/players" class="item">Players</router-link>
        <router-link to="/admin/items" class="item">Items</router-link>
      </nav>

      <router-view></router-view>
  </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
  } from 'vue-property-decorator';

  import { SessionState } from '@/App.vue';
  import GameMath from '../logic/game-math';
  import { Player } from '../logic/models';
  import PlayerRepository from '../logic/player-repository';
  import Requests from '../requests';
  import router from 'vue-router';


  @Component({})
  export default class Admin extends Vue {

    private mounted() {
      const sessionState = SessionState.get();
      if (sessionState !== null && !sessionState.authenticated && !sessionState.administrator) {
        this.$router.push('/login');
        return;
      }
    }
  }
</script>

<style scoped>
.admin {
    margin-top: 132px;
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
