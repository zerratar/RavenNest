<template>
  <div class="login">
    <div class="twitch-auth-active" v-if="twitchAuthenticating()">
      <h2>Logging in with Twitch...</h2>
    </div>
    <div v-if="!twitchAuthenticating()">
      <h1>User login</h1>
      <p>Login to access character customization, marketplace and more!<br />If you don't have an account you can login
        with Twitch and then assign a username and password.</p>
      <div class="login-container">
        <div class="login">
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
  import {
    SessionState
  } from '@/App.vue';



  @Component({})
  export default class Login extends Vue {
    private username: string = "";
    private password: string = "";
    private badLoginResult: string = "";

    private mounted() {
      this.updateLoginStateAsync();
    }

    public twitchAuthenticating(): boolean {
      const hash = document.location.hash;
      return hash != null && hash.length > 0 && hash.includes('access_token');
    }

    private async authenticateWithUserCredentialsAsync() {
      const user = this.username;
      const pass = this.password;
      this.badLoginResult = "";

      if (user.length == 0 || pass.length == 0) {
        return;
      }

      try {
        const response = await Requests.sendAsync('/api/auth/login', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({
            "username": user,
            "password": pass
          })
        });

        let errorMessage = "";
        if (response.ok) {
          const result = await response.json();
          try {
            const newSessionState = result;
            SessionState.set(newSessionState);
            if (result.authenticated === true) {
              if (result.requiresPasswordChange) {
                this.$router.push("/password");
              } else {
                this.$router.push("/");
              }
            } else {
              errorMessage = 'Invalid username or password.';
            }
            ( < any > window)["AppClass"].$forceUpdate();
          } catch (err) {
            if (err != null && err) {
              errorMessage = err.toString();
            } else {
              errorMessage = "Invalid username or password.";
            }
          }
        } else {
          errorMessage = "Unable to login, unknown error.";
        }

        this.badLoginResult = errorMessage;
      } catch (err) {}
    }

    private async authenticateWithTwitchAsync() {
      const response = await Requests.sendAsync('/api/twitch/access', {
        method: 'GET'
      });
      const url = await response.text();
      if (url != null && url.length > 0) {
        window.location.href = url;
      }
    }

    private async updateLoginStateAsync() {
      let token = "";
      const hash = document.location.hash;
      if (hash != null && hash.length > 0 && hash.includes('access_token')) {
        token = hash.split('access_token=')[1];
        const response = await Requests.sendAsync('/api/twitch/session?token=' + token, {
          method: 'GET'
        });

        if (response.ok) {
          const result = await response.json();
          if (result != null) {
            SessionState.set(result);
            if (result.authenticated === true) {
              if (result.requiresPasswordChange) {
                this.$router.push("/password");
              } else {
                this.$router.push("/");
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