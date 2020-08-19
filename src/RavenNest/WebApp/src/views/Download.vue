<template>
  <div class="download">
    <h1>Downloading and installing Ravenfall</h1>
    <p>Currently there isnt any streamlined way to install Ravenfall and you have to do some manual set up for it to work.</p>
    <div class="sections">
      <div class="section">
          <h2>Download</h2>
          <p>You can download the current version, Windows only. <a href="https://github.com/zerratar/Ravenfall-Legacy/releases/download/v0.3.6a-alpha/Ravenfall.v0.3.6a-alpha.7z">Ravenfall.v0.3.6a-alpha.7z</a> ca 150mb</p>
          <p>Make sure you have either WinRAR or 7zip installed. I recommend 7zip <a href="https://www.7-zip.org/download.html" target="_blank">www.7-zip.org/download</a></p>
          <p>Finally just unpack the archive where you want the game to be.</p>
      </div>
      <div class="section">
 <h2>Installing</h2>
        <p>Make sure the Twitch account you want to use for the bot has 2FA set up. <a href="https://help.twitch.tv/s/article/two-factor-authentication-with-authy?language=en_US" target="_blank">See here how</a></p>
        
        <p>
          Update the settings.json file that is in the same folder with your unpacked Ravenfall folder. It will look like this
          <pre><code>{
  "twitchBotUsername": "bot username",
  "twitchBotAuthToken": "bot access token",
  "twitchChannel":  "channel name"
}</code></pre>
        </p>
        
        <ol>
        <li>
          Set the <i>twitchBotUsername</i> to your bot (or your username).
        </li>
        <li>
          Set the <i>twitchBotAuthToken</i> to the token you can get from https://twitchtokengenerator.com/ Select 'chat bot'
        </li>
        <li>
          Set the <i>twitchChannel</i> to your Twitch channel
        </li>
        </ol>
        <p>
          It will look something similar to this when done:
          <pre><code>{
  "twitchBotUsername": "RavenfallOfficial",
  "twitchBotAuthToken": "jiklawoeguh8aol3gtogha3hoga2w3pgp8390awh3g",
  "twitchChannel":  "Zerratar"
}</code></pre>   
        </p>
        <p>
          Finally, before you can login to the game you need to set up a password. <a href="/login" target="_blank">You can do this by logging in using Twitch</a>.
           You will be asked to set a password first time you login.
        </p>
        <p>
          Now you can run Ravenbot.exe and Ravenfall.exe. Ready to be streamed and played! :) Please enjoy!
        </p>        
      </div>
    </div>   
  </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
  } from 'vue-property-decorator';
  import { SessionState } from '@/App.vue';

  @Component({})
  export default class Download extends Vue {
  
    private mounted() {
     
    }
  }
</script>

<style scoped>

  .sections {
    display: flex;
    flex-flow: column;
    flex-wrap: wrap;
    padding: 0px 25px 25px 25px;
    max-width: 100%;
    width: 800px;
    justify-content: center;
    align-content: center;
    align-items: center;
    margin-left: auto;
    margin-right: auto;
  }
  .section {
    text-align: left;
    width: 800px;
    max-width: 100%;
  }
  .section ol { 
      padding-left: 20px;
  }
  .section li {
    line-height: 24pt;
  }
  .section h2 {
    border-bottom: 1px solid rgb(230, 230, 230);
    padding-bottom: 10px;
  }
  .section pre {
    background-color: #fafafa;
  }
  .download {
      margin-top: 140px;
      flex-grow:1;
  }

</style>