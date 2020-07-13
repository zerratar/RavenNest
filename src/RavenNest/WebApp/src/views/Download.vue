<template>
  <div class="download">
    <h1>Downloading and installing Ravenfall</h1>
    <p>Currently there isnt any streamlined way to install Ravenfall and you have to do some manual set up for it to work.</p>
    <h2>Download</h2>
    <p>You can download the current version, windows only. <a href="https://github.com/zerratar/Ravenfall-Legacy/releases/download/v0.3.4a-alpha/Ravenfall.v0.3.4a-alpha.7z">Ravenfall.v0.3.4a-alpha.7z</a> ca 150mb</p>
    <p>Make sure you have either WinRAR or 7zip installed. I recommend 7zip <a href="https://www.7-zip.org/download.html" target="_blank">www.7-zip.org/download</a></p>
    <p>Finally just unpack the archive where you want the game to be.</p>
    <h2>Installing</h2>
    <p>
      <ol>
        <li>Make sure the Twitch account you want to use for the bot has 2FA set up. <a href="https://help.twitch.tv/s/article/two-factor-authentication-with-authy?language=en_US" target="_blank">See here how</a></li>
        <li>
          Update the settings.json file that is in the same folder with your unpacked Ravenfall folder. It will look like this
          <code>{
  "twitchBotUsername": "bot username",
  "twitchBotAuthToken": "bot access token",
  "twitchChannel":  "channel name"
}</code>
        </li>
        <li>
          Set the twitchBotUsername to your bot (or your username).
        </li>
        <li>
          Set the twitchBotAuthToken to the token you can get from https://twitchtokengenerator.com/ Select 'chat bot'
        </li>
        <li>
          Set the twitchChannel to your Twitch channel
        </li>
        <li>
          It will look something similar to this when done:
          <code>{
  "twitchBotUsername": "RavenfallOfficial",
  "twitchBotAuthToken": "jiklawoeguh8aol3gtogha3hoga2w3pgp8390awh3g",
  "twitchChannel":  "Zerratar"
}</code>          
        </li>
        <li>
          Finally, before you can login to the game you need to set up a password. <a href="/login" target="_blank">You can do this by logging in using Twitch</a>.
           You will be asked to set a password first time you login.
        </li>
        <li>
          Now you can run Ravenbot.exe and Ravenfall.exe. Ready to be streamed and played! :) Please enjoy!
        </li>        
      </ol>
    </p>
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

  .download {
      margin-top: 140px;
      flex-grow:1;
  }

</style>