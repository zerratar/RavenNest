<template>
  <div class="home">

    <div class="section background">
      <video src="/assets/header.webm" autoplay muted loop></video>
    </div>

    <div class="section hero">
      <img class="hero-logo" src="assets/imgs/ravenfall_logo.png" />

      <div class="text">An idle game for streamers, made by streamers!<br />Allow your viewers to train, craft,
        fight together against huge raid bosses or challenge each other in the arena!</div>
      <div class="button"><i class="fab fa-windows"></i> Download</div>
      <!-- <div class="button">Download <i class="fab fa-apple"></i></div>
      <div class="button">Download <i class="fab fa-linux"></i></div> -->
    </div>

    <div class="section body">

      <h1 style="text-align: center;">The website is still under development, please come back later.</h1>
    </div>

  </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue
  } from 'vue-property-decorator';
  import router from 'vue-router';
  import HelloWorld from '@/components/HelloWorld.vue'; // @ is an alias to /src

  @Component({
    components: {
      HelloWorld,
    },
  })
  export default class Home extends Vue {

  }
</script>

<style scoped lang="scss">
  .section.background {
    height: 762px;
    background-color: black;

    video {
      width: 100%;
      position: relative;
      top: 50%;
      transform: translate(0, -50%);
    }
  }

  .section.hero {
    height: 762px;
    position: absolute;
    top: 0;
    color: white;
    background: url(/assets/imgs/header-tint.png) 0 0 no-repeat;
    background-size: cover;
    background-position: center;
    width: 100%;

    padding-top: 110px;
  }

  .section .text {
    font-size: 17pt;
    font-weight: 300;
    line-height: 27pt;
  }

  .section.body {
    position: absolute;
    background-color: white;
    width: 100%;
    min-height: 400px;
    max-height: 100%;
  }

  .hero .button {
    margin-top: 25px;
    margin-left: 10px;
    margin-right: 10px;
  }
  

  .content .button {
    cursor: pointer;
    font-size: 17pt;
    border: 2px solid white;
    border-radius: 25px;
    display: inline-block;
    font-size: 300;
    padding: 10px 25px;
    user-select: none;
    box-shadow: 0 0 10px rgba(0,0,0, 0.3);
  }

  .content .button:hover {
    background-color: white;
    color: #333;    
  }

</style>