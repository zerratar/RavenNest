<template>
  <div class="home">

    <div class="section background">
      <video src="https://ravennestblobstorage.blob.core.windows.net/cdn/header.webm?sp=rl&st=2019-10-25T16:25:59Z&se=2031-10-04T11:25:00Z&sv=2019-02-02&sr=b&sig=x1Xwz52QJ8jLmAn1cjYwn5PPkZVCVN5T9ICJRQPwlGA%3D" autoplay muted loop></video>
    </div>

    <div class="section hero">
      <img class="hero-logo" src="assets/imgs/ravenfall_logo.png" />

      <div class="text">An idle game for streamers, made by streamers!<br />Allow your viewers to train, craft,
        fight together against huge raid bosses or challenge each other in the arena!</div>
      <a class="button" href="/download"><i class="fab fa-windows"></i> Download</a>
      <!-- <div class="button">Download <i class="fab fa-apple"></i></div>
      <div class="button">Download <i class="fab fa-linux"></i></div> -->
    </div>

    <div class="section body">     
      <h1>
        Ravenfall is a Twitch integrated idle game, allowing your viewers to control a character in a 3d world.
      </h1>
      <h3>        
        Viewers can craft their own equipment, fight against challenging raid bosses or even duel their rival players.
      </h3>

      <div class="section-group">
        <h1>Watch the trailer</h1>
        <div class="embed-container-outer">
          <div class='embed-container'>
            <iframe src='https://www.youtube.com/embed/lTMs19hsvNc' frameborder='0' allowfullscreen></iframe>
          </div>
        </div>
      </div>
      <div class="section-group">
        <h1>Game features</h1>
      <div class="feature-list">
        <div class="feature">
            <img class="feature-image" src="assets/imgs/raids.jpg" />
            <h2>
              Fight raid bosses
            </h2>
            <p>
              Engage in challenging raid boss fights alongside other players to earn great rewards.
            </p>
        </div>
        <div class="feature">
            <img class="feature-image" src="assets/imgs/captain.jpg" />
            <h2>
              Sail between islands
            </h2>
            <p>
              Be the captain of a big ferry. Train your sailing skill by embarking the ferry and enjoying the view.
            </p>
        </div>
        <div class="feature">
            <img class="feature-image" src="assets/imgs/crafting.jpg" />

            <h2>
              Craft your own equiment
            </h2>
            <p>
              Keep training your crafting skill and eventually you will be able to craft the most amazing equipment in the game.
            </p>
        </div>     
        <div class="feature">
            <img class="feature-image" src="assets/imgs/gathering.jpg" />

            <h2>
              Gather resources
            </h2>
            <p>
              You can fish, mine, farm or cut down trees. All to gain resources for your crafting needs!
            </p>
        </div>     
        <div class="feature">
            <img class="feature-image" src="assets/imgs/arena.jpg" />
            <h2>
              Fight in the Arena
            </h2>
            <p>
              You may participate in a free-for-all player combat; The arena. Fight other players and be victorious.
            </p>
        </div>   
        <div class="feature">
            <img class="feature-image" src="assets/imgs/pets.jpg" />
            <h2>
              Get yourself a pet
            </h2>
            <p>
              The game contains a variety of pets that can be obtained, they will follow your player around and just be ridiculously cute.
            </p>
        </div>       

          <div class="feature">
            <img class="feature-image" src="assets/imgs/raidwar.jpg" />
            <h2>
              Raid other streamers
            </h2>
            <p>
              When you're done with your stream, don't forget to raid another streamer playing Ravenfall.
            </p>
        </div>   
          <div class="feature">
            <img class="feature-image" src="assets/imgs/marketplace.jpg" />
            <h2>
              Trade in a global marketplace
            </h2>
            <p>
              Players can buy and sell items in a global marketplace, that any streamer can access.
            </p>
        </div>   
        </div>
        </div>
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

.embed-container-outer {
  margin-top: 50px;
  position: relative;
  max-width: 100%;
  width: 1100px;
  margin-left: auto;
  margin-right: auto;
}

.embed-container { 
    position: relative; 
    padding-bottom: 56.25%; 
    height: 0; 
    overflow: hidden; 
    max-width: 100%;       
  } 
  
  .embed-container iframe, .embed-container object, .embed-container embed { 
    position: absolute; top: 0; left: 0; width: 100%; height: 100%;    
  }

  img.hero-logo {
      max-width: 100%;
  }

  .feature {
      max-width: 100%;
      width: 300px;
      padding-bottom: 45px;
  }

  .feature-image {
    max-width: 100%;
    width: 150px;
  }

  .feature-list {
    margin-top: 50px;
    display:flex;
    flex-flow:row;
    justify-content: space-around;
    padding-bottom: 50px;
    flex-wrap: wrap;
    width: 1280px;
    max-width: 100%;
    margin-left: auto;
    margin-right: auto;
  }

  .section.background {
    height: 100vh;
    max-height: 100vh;
    opacity: 1;
    transition: all 0.75s cubic-bezier(0.2, 0.3, 0.25, 0.9) 0s;

    video {
    width: 100%;
    object-fit: cover;
    min-height: 300px;
    height: 100vh;
    max-height: 100vh;
    }
  }

  .section-group:nth-of-type(odd) {
    background-color: rgb(241, 241, 241);
    padding-bottom: 50px;
    margin-top: 20px;
  }

  .section.hero {
    height: 100vh;
    position: absolute;
    top: 0;
    color: #fff;
    /*background: url(/assets/imgs/header-tint.png) 0 0 no-repeat;*/
    background-size: cover;
    background-position: 50%;
    width: 100%;
    padding-top: 20vh;
    box-sizing: border-box;
  }

  .section .text {
    text-shadow: 2px 2px 2px #000000;
    font-size: 18pt;
    font-weight: 500;
    line-height: 26pt;
    width: 75%;
    margin-left: auto;
    margin-right: auto;
  }

  .section.body {
    background-color: #fff;
    width: 100%;
    min-height: 400px;
    max-height: 100%;

    display: flex;
    flex-flow: column;
    align-items: stretch;
  }

  .hero .button {
    margin-top: 25px;
    margin-left: 10px;
    margin-right: 10px
  }
  

  .content .button {
    cursor: pointer;
    font-size: 17pt;
    border: 2px solid #fff;
    border-radius: 25px;
    display: inline-block;
    font-size: 300;
    padding: 10px 25px;
    -webkit-user-select: none;
    -moz-user-select: none;
    -ms-user-select: none;
    user-select: none;
    -webkit-box-shadow: 0 0 10px rgba(0,0,0,.3);
    box-shadow: 0 0 10px rgba(0,0,0,.3)
  }

  .content .button:hover {
    background-color: white;
    color: #333;    
  }

a.button {
    text-decoration: none;
    color: white;
}

</style>