<template>
  <div id="app">


    <div class="nav-bar">
      <div class="nav-bar-content">
        <div class="logo">
          <img src="assets/imgs/RavenfallGray.png" width="200" alt="" />
        </div>
        <div class="links">
          <router-link to="/" class="item">Home</router-link>
          <router-link to="/customization" class="item">Customization</router-link>
          <router-link to="/highscore" class="item">HighScore</router-link>
          <router-link to="/docs" class="item">Developer</router-link>
          <div class="right">
            <router-link to="/login" class="item" v-if="!authenticated()">Login</router-link>
            <!-- <router-link to="/register" class="item" v-if="!authenticated()">Register</router-link> -->
            <router-link to="/logout" class="item" v-if="authenticated()">Logout</router-link>
          </div>
        </div>
        <div class="social">
          <ul>
            <li><a href="https://www.twitch.tv/zerratar" target="_blank"><i class="fab fa-twitch"></i></a></li>
            <li><a href="https://www.twitter.com/zerratar" target="_blank"><i class="fab fa-twitter"></i></a></li>
            <li><a href="https://www.github.com/zerratar" target="_blank"><i class="fab fa-github"></i></a></li>
          </ul>
        </div>
      </div>
    </div>

    <div class="content">
      <router-view />
    </div>

    <div class="footer"></div>
  </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
  } from 'vue-property-decorator';
  import SiteState from "./site-state";

  export class SessionState {
    constructor(
      public id: string,
      public authenticated: boolean,
      public userId: string,
      public userName: string,
      public requiresPasswordChange: boolean,
    ) {}

    public static get(): SessionState | null {
      const win = < any > window;
      const sessionSettings = < any > win["SessionState"];
      if (typeof sessionSettings !== 'undefined') {
        return SessionState.mapSessionState(sessionSettings);
      }
      return null;
    }

    public static set(state: any): SessionState {
      const ss = SessionState.mapSessionState(state);
      const win = < any > window;
      win["SessionState"] = ss;
      return ss;
    }

    private static mapSessionState(state: any): SessionState {
      return new SessionState(state.id, state.authenticated, state.userId, state.userName, state.requiresPasswordChange);
    }
  }

  @Component({})
  export default class App extends Vue {

    mounted() {
      ( < any > window)["AppClass"] = this;
    }

    public authenticated(): boolean {
      const sessionState = SessionState.get();
      if (sessionState != null) {
        return sessionState.authenticated;
      }
      return false;
    }

  }
</script>

<style lang="scss">
  body,
  html {
    margin: 0;
    padding: 0;
  }
  h1 {
      text-transform: uppercase;
      margin-top: 50px;
      margin-bottom: 60px;
      font-size: 34pt;      
      margin-bottom: 0;
  }
  p {
    line-height: 25pt;
  }
  #app {
    font-family: 'Nunito', sans-serif;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
    text-align: center;
    color: #2c3e50;
  }

  .nav-bar {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    background-color: #0e0e0f;
    padding: 5px 20px;
    transition: all 150ms ease-in-out;
    z-index: 10;


    .logo {
      display: flex;
      align-items: center;
    }

    .nav-bar-content {
      margin-left: auto;
      margin-right: auto;
      display: flex;
      color: #e9e9e9;
      justify-content: space-between;
      max-width: 100%;
      width: 1760px;
    }

    .links {
      flex-grow: 1;
      padding-left: 30px;
    }

    .social ul,
    .links {
      display: flex;
      align-items: center;
    }

    .social li {
      list-style: none;
    }

    .social li a,
    .links a {
      text-decoration: none;
      font-size: 13pt;
      text-transform: uppercase;
      padding: 10px 19px;
      display: inline-block;
      transition: all 150ms ease-in-out;
      user-select: none;
      height: 30px;
      color: #d4d4d4;
      line-height: 32px;
    }

    .links {

      .item:hover,
      .sub-item:hover {
        cursor: pointer;
        color: #3498db;
      }

      .sub-item {
        font-size: 11pt;
      }

      .item.selected:after,
      .sub-item.selected:after {
        content: "";
        height: 1px;
        width: 100%;
        border-bottom: 1px solid white;
        position: relative;
        display: block;
        line-height: 2px;
        color: white;
        font-size: 0pt;
        top: 0px;
      }

      .item:hover:after,
      .sub-item:hover:after {
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
        animation: selection linear 0.125s;
        animation-fill-mode: forwards;
      }
    }

    .social a:hover {
      border-radius: 30px;
      cursor: pointer;
      background-color: #3498db;
      box-shadow: 0 15px 30px 0 rgba(0, 0, 0, .25);
    }

    .sub-items {
      position: fixed;
      display: none;
      flex-flow: column;
      padding-left: 0;
      padding-top: 15px;
      margin-left: -15px;
      background-color: rgb(34, 34, 43);
      box-shadow: 0 15px 30px 0 rgba(0, 0, 0, .25);
      transition: opacity 125ms ease;
      opacity: 0;
    }

    .sub-item.separator {
      border-bottom: 1px solid rgba(255, 255, 255, .1);
      border-top: 1px solid rgba(0, 0, 0, .75);
      height: 1px;
      padding-top: 0px;
      padding-bottom: 0;
      margin-left: 15px;
      margin-right: 15px;
      margin-top: -5px;
      box-sizing: border-box;
    }

    .sub-items:hover,
    .item:hover~.sub-items {
      display: flex;
      opacity: 1;
    }

    .right {
      margin-left: auto;
    }
  }

  .content {
    top: 92px;
    position: absolute;
    width: 100%;
  }
</style>