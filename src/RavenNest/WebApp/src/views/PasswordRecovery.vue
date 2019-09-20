<template>
  <div class="password-recovery">
    <h1>Forgot your password?</h1>
    <p>Authenticate with twitch to reset your password.</p>
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
  export default class Register extends Vue {
    private username: string = "";
    private password: string = "";
    private badLoginResult: string = "";

    private mounted() {
    }

  }
</script>

<style scoped>
 
</style>