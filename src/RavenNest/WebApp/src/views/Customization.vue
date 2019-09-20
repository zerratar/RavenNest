<template>
    <div class="customization">
      <h1>Character Customization Tool</h1>
      <div class="webgl-content">
        <div id="unityContainer" style="width: 960px; height: 600px"></div>
        <div class="footer">
          <div class="webgl-logo"></div>
          <div class="fullscreen" v-on:click="unityInstance.SetFullscreen(1)"></div>
          <div class="title">Ravenfall</div>
        </div>
    </div>
  </div>
</template>

<script lang="ts">
  import {
    Component,
    Vue,
  } from 'vue-property-decorator';
  import router from 'vue-router';

  @Component({})
  export default class Customization extends Vue {

    private unityInstance: any = null;

    mounted() {
      // this.unityInstance = null;
      const win = <any>window;
      const loader = <any>win["UnityLoader"];
      const progress = <any>win["UnityProgress"];
      this.unityInstance = loader.instantiate('unityContainer', 'assets/Build/Build.json', {
        onProgress: progress
      });
    }
  }
</script>

<style scoped>
.webgl-content * {border: 0; margin: 0; padding: 0}
.webgl-content {margin-left:auto;margin-right:auto; display:flex; justify-content: center; flex-flow:column;align-items: center;}

.webgl-content .logo, .progress {position: absolute; left: 50%; top: 50%; -webkit-transform: translate(-50%, -50%); transform: translate(-50%, -50%);}
.webgl-content .logo {background: url('/assets/templatedata/progressLogo.Light.png') no-repeat center / contain; width: 154px; height: 130px;}
.webgl-content .progress {height: 18px; width: 141px; margin-top: 90px;}
.webgl-content .progress .empty {background: url('/assets/templatedata/progressEmpty.Light.png') no-repeat right / cover; float: right; width: 100%; height: 100%; display: inline-block;}
.webgl-content .progress .full {background: url('/assets/templatedata/progressFull.Light.png') no-repeat left / cover; float: left; width: 0%; height: 100%; display: inline-block;}

.webgl-content .logo.Dark {background-image: url('/assets/templatedata/progressLogo.Dark.png');}
.webgl-content .progress.Dark .empty {background-image: url('/assets/templatedata/progressEmpty.Dark.png');}
.webgl-content .progress.Dark .full {background-image: url('/assets/templatedata/progressFull.Dark.png');}

.webgl-content .footer {margin-top: 5px; height: 38px; line-height: 38px; font-family: Helvetica, Verdana, Arial, sans-serif; font-size: 18px;}
.webgl-content .footer .webgl-logo, .title, .fullscreen {height: 100%; display: inline-block; background: transparent center no-repeat;}
.webgl-content .footer .webgl-logo {background-image: url('/assets/templatedata/webgl-logo.png'); width: 204px; float: left;}
.webgl-content .footer .title {margin-right: 10px; float: right;}
.webgl-content .footer .fullscreen {background-image: url('/assets/templatedata/fullscreen.png'); width: 38px; float: right;}

.webgl-content .footer{width: 960px; max-width: 100%;}

.webgl-content .progress.Dark {
    width: 100%;
    height: 100%;
    position: absolute;
    transform: translate(0, -100%);
    z-index: 999999;
}

</style>