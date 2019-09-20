<template>
  <div class="password">
    <h1>Add a password</h1>
    <p>To complete your account, please add a password. This password is required to stream Ravenfall<br />
    Note: The password is not required for playing Ravenfall in someone elses stream.</p>

    <div class="registration-form">
      <div class="username"><span>Your login username is </span><span class="bold">{{username}}</span></div>
      <div>
        <div class="input-icon"><i class="fas fa-lock-alt"></i></div>
        <input id="inputPassword" name="inputPassword" class="input-password" type="password" v-model="password"
          minlength="8" placeholder="Password (min 8 characters)" />
      </div>
      <button class="save-button" v-on:click="signup()">Save</button>
      <!-- <div class="btn-skip" v-on:click="skipSignup()">Not now, ask me again next time I login.</div> -->
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
  export default class Password extends Vue {
    private username: string = "";
    private password: string = "";
    private passwordError: string = "";

    mounted() {
      const sessionState = SessionState.get();
      if (sessionState != null) {
        this.username = sessionState.userName;
      }
    }

    public async signup() {
      this.passwordError = "";
      if (this.password == null || this.password.length < 8) {
        this.passwordError = "You must have a password with minimum 8 characters."
        return;
      }

      const result = await Requests.sendAsync('/api/auth/signup', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          'password': this.password
        })
      });

      if (result.ok) {
        const sessionState = SessionState.get();
        if (sessionState == null) return;
        sessionState.requiresPasswordChange = false;
        this.$router.push("/");
      }
    }

    // public async skipSignup() {
    //   const result = await Requests.sendAsync('/api/auth/skip-signup');
    //   if (result.ok) {
    //     const sessionState = SessionState.get();
    //     if (sessionState == null) return;
    //     sessionState.requiresPasswordChange = false;
    //     this.$router.push("/");
    //   }
    // }

  }
</script>

<style scoped>
  .bold {
    font-weight: bold;
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

  button.save-button {
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

  button.save-button:active {
    /* border:1px solid #0a5585; */
    background-color: #0a5585;
  }
.registration-form {
    width: 410px;
    max-width: 100%;
    margin-left: auto;
    margin-right: auto;
}
  .input-icon {
    position: absolute;
    z-index: 10;
    margin-left: 13px;
    margin-top: 15px;
    color: #c7c7c7;
  }

  .username {
    margin-bottom: 25px;
    font-size: 18pt;
  }
</style>