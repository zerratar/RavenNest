<template>
  <div class="login">
    <div class="twitch-auth-active" v-if="twitchAuthenticating()">
      <h2>{{loginMessage}}</h2>
    </div>
    <div v-if="!twitchAuthenticating()">
      <h1 class="login-title">User login</h1>
      <p class="login-subtitle">Login to access character customization, marketplace and more!<br />If you don't have an account you can login
        with Twitch and then assign a password.</p>
      <div class="login-container">
          <div class="input-row">
            <div>
              <div class="input-icon user"><i class="fas fa-user"></i></div>
              <input id="inputUsername" name="inputUsername" class="input-username" type="text" v-model="username"
                placeholder="Username" />
            </div>
            <div>
              <div class="input-icon"><i class="fas fa-lock-alt"></i></div>
              <input id="inputPassword" name="inputPassword" class="input-password" type="password" v-model="password"
                minlength="8" placeholder="Password (min 8 characters)" />
            </div>
            <button class="login-button" v-on:click="authenticateWithUserCredentialsAsync()">Login</button>
            <!-- <div class="login-links">            
            <router-link to="/register">Create your account</router-link>|
            <router-link to="/password-recovery">Forgot password?</router-link>
          </div> -->
            <div class="bad-login-result">{{badLoginResult}}</div>
            <div class="login seperator">
              <div class="seperator text">Or</div>
            </div>
            <div class="login twitch" v-on:click="authenticateWithTwitchAsync()">
              <i class="fab fa-twitch"></i> Login with Twitch
            </div>
          </div>
      </div>
    </div>

  </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
  } from 'vue-property-decorator';
  import V from 'vue';
  import SiteState from '../site-state';
  import Requests from '../requests';
  import router from 'vue-router';
  import { SessionState } from '@/App.vue';


  @Component({})
  export default class Login extends Vue {
    private username: string = '';
    private password: string = '';
    private badLoginResult: string = '';
    private loginMessage: string = 'Logging in with Twitch...';
    private loginMessageDefault: string = 'Logging in with Twitch...';
    private loginMessageSuccess: string = 'Login was successeful. You may now close this window';

    public mounted() {
      this.updateWebsiteLoginStateAsync();
      this.updateGameClientLoginStateAsync();
    }

    public twitchAuthenticating(): boolean {
      const token = this.getQueryParam('code');
      const hash = document.location.hash;
      return (hash != null && hash.length > 0 && hash.includes('access_token')) ||
             (token != null && token.length > 0);
    }

    private async authenticateWithUserCredentialsAsync() {
      const user = this.username;
      const pass = this.password;
      this.badLoginResult = '';

      if (user.length === 0 || pass.length === 0) {
        return;
      }

      try {
        const response = await Requests.sendAsync('/api/auth/login', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            username: user,
            password: pass,
          }),
        });

        let errorMessage = '';
        if (response.ok) {
          const result = await response.json();
          try {
            const newSessionState = result;
            SessionState.set(newSessionState);
            if (result.authenticated === true) {
              if (result.requiresPasswordChange) {
                this.$router.push('/password');
              } else {
                this.$router.push('/');
              }
            } else {
              errorMessage = 'Invalid username or password.';
            }
            (window as any)['AppClass'].$forceUpdate();
          } catch (err) {
            if (err != null && err) {
              errorMessage = err.toString();
            } else {
              errorMessage = 'Invalid username or password.';
            }
          }
        } else {
          errorMessage = 'Unable to login, unknown error.';
        }

        this.badLoginResult = errorMessage;
      } catch (err) {
        console.error(err);
      }
    }

    private async authenticateWithTwitchAsync() {
      const response = await Requests.sendAsync('/api/twitch/access', {
        method: 'GET',
      });
      const url = await response.text();
      if (url != null && url.length > 0) {
        window.location.href = url;
      }
    }

    private getQueryParam(name: string): string | null {
      let regex: RegExpExecArray | null;
      regex = (new RegExp('[?&]' + encodeURIComponent(name) + '=([^&]*)')).exec(location.search);
      if (regex) {
          return decodeURIComponent(regex[1]);
      }
      return null;
    }

    private async updateGameClientLoginStateAsync() {
      const token = this.getQueryParam('token');
      const state = this.getQueryParam('state');
      const user = this.getQueryParam('user');
      const id = this.getQueryParam('id');
      if (token != null && token.length > 0) {
        try {
          let requestUrl = 'http://localhost:8182/?token=' + token + '&state=' + state;
          if (id != null && id.length > 0) {
            requestUrl += '&id=' + id;
          }
          if (user != null && user.length > 0) {
            requestUrl += '&user=' + user;
          }
          const response = await Requests.sendAsync(requestUrl, {
            method: 'GET',
          });
        } catch (err) {
          // ignore the error here,
          // it will most likely just be because we are doing a cross domain call
          // but the client will still receive the message nontheless.
        }

        this.loginMessage = this.loginMessageSuccess;
      }

      (window as any)['AppClass'].$forceUpdate();
    }

    private async updateWebsiteLoginStateAsync() {
      let token = '';
      const hash = document.location.hash;
      if (hash != null && hash.length > 0 && hash.includes('access_token')) {
        token = hash.split('access_token=')[1];
        const response = await Requests.sendAsync('/api/twitch/session?token=' + token, {
          method: 'GET',
        });

        if (response.ok) {
          const result = await response.json();
          if (result != null) {
            SessionState.set(result);
            if (result.authenticated === true) {
              if (result.requiresPasswordChange) {
                this.$router.push('/password');
              } else {
                this.$router.push('/');
              }
            } else {
              this.badLoginResult = 'Invalid username or password.';
            }
            return;
          } else {
            this.badLoginResult = 'Login failed, unknown reason.';
          }
        }
      }
    }
  }
</script>

<style scoped>
  .input-row {
    display: -webkit-box;
    display: -ms-flexbox;
    display: flex;
    -webkit-box-orient: vertical;
    -webkit-box-direction: normal;
    -ms-flex-flow: column;
    flex-flow: column;
    max-width: 100%;
    width: 410px;
    left: 50%;
    -webkit-transform: translate(-50%);
    transform: translate(-50%);
    position: relative;
  }

  label {
    text-align: left;
    font-size: 12pt;
    font-weight: 600;
    padding: 5px 0;
  }

  input#inputPassword,
  input#inputUsername {
    padding: 16px 35px;
    font-size: 13pt;
    border: 1px solid #cecccc;
    MARGIN-BOTTOM: 10px;
    background-color: transparent;
    width: 100%;
    display: inline-block;
    box-sizing: border-box;
  }

  input#inputUsername {
    margin-top: 20px;
  }

  .content>.login {
      margin-top: 162px;
      flex-grow: 1;
      padding-bottom: 40px;
  }

  .login-title {
      
  }

  .login-subtitle {
      font-size: 18pt;
      font-weight: 500;
      max-width: 100%;
      width: 720px;
      margin-left: auto;
      margin-right: auto;
  }

  button.login-button {
    font-size: 12pt;
    font-weight: 300;
    background: none;
    border: 0;
    background-color: #2a93d2;
    color: white;
    padding: 15px 25px;
    width: 350px;
    max-width: 100%;
    margin-left: auto;
    margin-right: auto;
    width: 100%;
    cursor: pointer;
    user-select: none;
    margin-top: 10px;
    margin-bottom: 30px;
  }

  button.login-button:active {
    /* border:1px solid #0a5585; */
    background-color: #0a5585;
  }

  .login.seperator {
    display: block;
    background: url(/assets/imgs/line.png) 0 50% no-repeat;
    text-align: center;
  }

  .seperator.text {
    display: inline-block;
    padding: 0 12px;
    font-weight: bold;
    font-size: 17px;
    background: white;
    vertical-align: top;
  }

  .login.twitch {
    PADDING: 15PX 25px;
    color: white;
    background-color: #6441a3;
    cursor: pointer;
    user-select: none;
    margin-top: 25px;
  }

  .login.twitch:active {
    background-color: #3a2464;
  }

  .bad-login-result {
    color: #e84118;
    margin-bottom: 25px;
  }

  .bad-login-result:empty {
    margin-bottom: 0;
  }

  .input-icon {
    position: absolute;
    z-index: 10;
    margin-left: 13px;
    margin-top: 15px;
    color: #c7c7c7;
  }

  .input-icon.user {
    margin-top: 36px;
  }
</style>
