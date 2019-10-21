<template>
  <div id="app">
    <div class="nav-bar" :class="{open: isMenuOpen, dark: isDark, scrolled: isScrolled}">
      <div class="nav-bar-content">
        <router-link to="/" class="logo">
          <img src="assets/imgs/RavenfallGray.png" width="200" alt="" />
        </router-link>
        <div class="links">
          <router-link to="/" class="item">Home</router-link>
          <!-- <router-link to="/customization" class="item">Customization</router-link> -->
          <router-link to="/how-to-play" class="item">How to play</router-link>
          <router-link to="/highscore" class="item">HighScore</router-link>
          <router-link to="/docs" class="item">Developer</router-link>
          <router-link to="/character" class="item" v-if="authenticated()">My character</router-link>
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

        <div class="btn-hamburger-menu" v-on:click="toggleMenu()">
          <i class="fas fa-bars"></i>
        </div>
      </div>
    </div>

    <div class="content">
      <router-view />      
      <div class="footer">
        Copyright &copy; ravenfall.stream 2019, all rights reserved.
      </div>
    </div>
  </div>
</template>

<script lang="ts">
import {
    Component,
    Vue,
    Prop, 
    Watch
  } from 'vue-property-decorator';
  import SiteState from "./site-state";

  const mobileMenuMinWith = 1300;

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
    public isMenuOpen: boolean = false;
    public isScrolled: boolean = false;
    public isDark: boolean = false;
    
    mounted() {
      ( < any > window)["AppClass"] = this;
      window.addEventListener("resize", e=>{
        this.isMenuOpen = this.isMenuOpen && window.innerWidth < mobileMenuMinWith;
      });
      window.addEventListener("scroll", e => {
        this.isScrolled = window.scrollY > 25;
      });
    }    

    @Watch('$route', { immediate: true, deep: true })
    onUrlChange(newVal: any) {
        this.isDark = newVal.path !== "/";
        this.isScrolled = window.scrollY > 25;
        this.isMenuOpen = false;//this.isMenuOpen && window.innerWidth < mobileMenuMinWith;
    }

    public toggleMenu(): void {
      this.isMenuOpen = !this.isMenuOpen;
      this.isScrolled = window.scrollY > 25;
      this.isMenuOpen = this.isMenuOpen && window.innerWidth < mobileMenuMinWith;
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
    height: 100%;
  }
  h1 {    
      margin-top: 50px;
      margin-bottom: 60px;
      font-size: 34pt;
      margin-bottom: 0;
      font-weight: 300;
      font-family: Heebo,sans-serif;
      max-width:100%;
      width: 800px;
      margin-left: auto;
      margin-right: auto;
  }
  h3,
  h2 {
    font-weight:300;
    margin-bottom: 0;
  }

  p {
    line-height: 20pt;
  }
  #app {
    font-family: 'Nunito', sans-serif;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
    text-align: center;
    color: #2c3e50;
    height: 100%;
  }

  .nav-bar {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    background-color: transparent;
    padding: 15px 20px;
    -webkit-transition: all .15s ease-in-out;
    transition: all .15s ease-in-out;
    z-index: 10;


    .btn-hamburger-menu {
      display: none;
    }

    .logo {
      -webkit-box-align: center;
      -ms-flex-align: center;
      align-items: center;
      cursor: pointer;
    }

    .logo,.nav-bar-content {
        display: -webkit-box;
        display: -ms-flexbox;
        display: flex;        
    }

    .nav-bar-content {
      margin-left: auto;
      margin-right: auto;
      /* color: #e9e9e9; */
      -webkit-box-pack: justify;
      -ms-flex-pack: justify;
      justify-content: space-between;
      max-width: 100%;
      height:82px;
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
      -webkit-transition: all .15s ease-in-out;
      transition: all .15s ease-in-out;
      -webkit-user-select: none;
      -moz-user-select: none;
      -ms-user-select: none;
      user-select: none;
      height: 30px;
      line-height: 32px;
      font-weight: bold;
      color: white;
    }

    .links {
      .item,.sub-item{
        border-bottom: 2px solid transparent;
      }
      .item:hover,
      .sub-item:hover {
        cursor: pointer;         
        border-bottom: 2px solid white;
      }

      .sub-item {
        font-size: 11pt;
      }

      .item.selected:after,
      .sub-item.selected:after {
        content: "";
        height: 1px;
        width: 0;
        border-bottom: 1px solid #fff;
        position: relative;
        display: block;
        line-height: 2px;
        color: #fff;
        font-size: 0;
        top: 0;
        -webkit-animation: selection .125s linear;
        animation: selection .125s linear;
        -webkit-animation-fill-mode: forwards;
        animation-fill-mode: forwards;
      }

      .item:hover:after,
      .sub-item:hover:after {
        content: "";
        height: 1px;
        width: 0;
        border-bottom: 1px solid #fff;
        position: relative;
        display: block;
        line-height: 2px;
        color: #fff;
        font-size: 0;
        top: 0;
        -webkit-animation: selection .125s linear;
        animation: selection .125s linear;
        -webkit-animation-fill-mode: forwards;
        animation-fill-mode: forwards;
      }
    }

    .social a:hover {
      border-radius: 30px;
      cursor: pointer;
      background-color: #3498db;
      -webkit-box-shadow: 0 15px 30px 0 rgba(0,0,0,.25);
      box-shadow: 0 15px 30px 0 rgba(0,0,0,.25)
    }

    .sub-items {
      position: fixed;
      display: none;
      -webkit-box-orient: vertical;
      -webkit-box-direction: normal;
      -ms-flex-flow: column;
      flex-flow: column;
      padding-left: 0;
      padding-top: 15px;
      margin-left: -15px;
      background-color: #22222b;
      -webkit-transition: opacity 125ms ease;
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


  .nav-bar.open {
    background-color: #0e0e0f;
    height: 100vh;
  }

  .content {
    width: 100%;
    height: 100%;
    display: flex;
    flex-flow: column;
  }

  .content>div:first-of-type {
      flex-grow:1;
  }


  .nav-bar.scrolled,
  .nav-bar.dark {
      background-color: #0e0e0f;
  }


  .loader {
    position: fixed;
    top: 50vh;
    left: 50%;
    transform: translate(-50%, 0);
    z-index: 999;
  }

  .lds-ripple {
    display: inline-block;
    position: relative;
    width: 64px;
    height: 64px;
  }

  .lds-ripple div {
    position: absolute;
    border: 4px solid #3498db;
    opacity: 1;
    border-radius: 50%;
    animation: lds-ripple 1s cubic-bezier(0, 0.2, 0.8, 1) infinite;
  }

  .lds-ripple div:nth-child(2) {
    animation-delay: -0.5s;
  }


  
  @keyframes lds-ripple {
    0% {
      top: 28px;
      left: 28px;
      width: 0;
      height: 0;
      opacity: 1;
    }

    100% {
      top: -1px;
      left: -1px;
      width: 58px;
      height: 58px;
      opacity: 0;
    }
  }

@media screen and (max-width: 1300px) {
  .social,
  .nav-bar .links {
    display: none;
  }

  .nav-bar {
    .btn-hamburger-menu {
      display: block;
      color: white;
      font-size: 23pt;
      line-height: 67pt;
      padding: 0px 15px;
      cursor: pointer;
    }
  }
}

.footer {
    width: 100%;
    background-color: #252527;
    color: #868686;
    display: flex;
    min-height: 160px;
    align-items: center;
    justify-content: center;
}

.nav-bar.open .nav-bar-content {
  height: auto;
}

.nav-bar.open .logo {
  height: 82px;
}

.open .right {
    margin-left: initial;
}

.nav-bar.open .social {
    display: flex;
    flex-flow: column;
    text-align: center;
    align-content: center;
    
    position: absolute;
    bottom: 30px;
    left: 0;
}

.nav-bar.open .links {
    display: flex;
    flex-flow: column;
    text-align: center;
    align-content: center;
    
    padding: 0;    
    position: absolute;
    margin-top: 90px;    
    left: 0;
    width: 100%;
}

</style>