<template>
  <div class="docs">
    <div class="documentation">
      <nav class="navigation-side">

        <div class="api-item" v-for="api of apis" :key="api.name" v-bind:class="{expanded: api.active}">
          <div class="api-name" v-on:click="apiClicked(api)">{{api.name}}</div>
          <div class="api-method-item" v-for="method of api.methods" :key="method.name">{{method.name}}</div>
        </div>
      </nav>

      <div class="main" v-if="activeApi !== null">
        <h1>{{activeApi.name}}</h1>
        <div class="method" v-for="method of activeApi.methods" :key="method.name">
          <h2>{{method.name}}</h2>
          <p>{{method.description}}</p>
          <h4>Request</h4>
          <pre><code class='http'>{{getRequestContent(activeApi, method)}}</code></pre>
          <div class="parameters" v-if="method.parameters.length > 0">
            <h4>Parameters</h4>
            <pre><code class='csharp'>{{ method.parameters.map(parameter => `${parameter.type} ${parameter.name}`).join("\r\n") }}</code></pre>
          </div>           
          <div class="response" v-if="method.response != null">
            <h4>Response</h4>
            <p>{{method.response.returnType}}</p>
            <pre><code class='json'>{{getExample(method)}}</code></pre>
          </div>         
        </div>
      </div>
    </div>
  </div>
</template>

<script lang="ts">
  import { Component, Vue } from 'vue-property-decorator';
  import router from 'vue-router';

  @Component({})
  export default class Home extends Vue {

    private activeApi: any = null;
    private activePage: any = null;
    private apiDocument: any = null;


    public getExample(method: any): string {
      return JSON.stringify(JSON.parse(method.response.example), null, 4);
    }

    public getRequestContent(api: any, method: any): string {
      let requestPath = `${api.path}${method.path}`;
      if (requestPath.endsWith('/')) requestPath = requestPath.slice(0, -1);
      let requestContent = `${method.method} ${requestPath} HTTP/1.1\r\n`;
      if (method.requestBody != null) {
        const contentType = method.requestBody.contentType;
        const example = JSON.stringify(JSON.parse(method.requestBody.example), null, 4);
        requestContent +=
          `Host: ravenfall.stream\r\n` +
          `Content-Type: ${contentType}\r\n` +
          `Content-Length: ${example.length}\r\n` +
          `\r\n` +
          `${example}`;
      }
      return requestContent;
    }

    private get pages(): any[] {
      if (this.apiDocument === null) return [];
      return this.apiDocument.pages;
    }

    private get apis(): any[] {
      if (this.apiDocument === null) return [];
      return this.apiDocument.apis;
    }

    private apiClicked(api: any) {
      if (this.activeApi != null) {
        this.activeApi.active = false;
      }

      api.active = true;
      this.activeApi = api;
    }

    private updated() {
      document.querySelectorAll('pre code').forEach((block) => {
          ((window as any)['hljs'] as any).highlightBlock(block);
      });
    }

    private mounted() {
      const winData = (window as any)['data'] as any;
      this.apiDocument = winData; // window["data"];
      this.activeApi = this.apis[0];
      this.activeApi.active = true;
    }
  }
</script>



<style scoped>
  .documentation {
    font-family: 'Heebo', sans-serif;
    color: #333;
    display: flex;
  }

  .docs {
      margin-top: 112px;
  }

  .navigation-side {
    overflow-y: auto;
    width: 350px;
    background-color: #fafafa;
    border-right: 1px solid #ddd;
  }  

  .main {
    padding: 30px 30px;
    text-align: left;    
  }

  .main h1 {
    padding-top: 0;
    font-size: 2.1em;
    line-height: 1.2;
    font-weight: 500;
    margin: 0 0 2rem 0;
  }

  .main p {
    font-size: 1.1rem;
    line-height: 1.6em;
  }

  .main h2 {
    font-size: 1.8em;
    line-height: 1.2em;
    border-bottom: 1px solid #dbd7df;
    font-weight: 400;
    margin-top: 40px;
  }

  .api-item {
    border-bottom: 1px solid #ddd;
  }

  .api-name {
    font-size: 1em;
    padding: 15px 10px;
    cursor: pointer;
    font-weight: 500;
    user-select: none;
    padding-left: 35px;
  }

  .api-name:before {
    content: '›';
    display: inline-block;
    padding: 0 7px 0 0;
    position: absolute;
    left: 15px;
  }

  .expanded .api-name:before {
    transform: rotate(90deg);
    margin-top: 5px;
  }

  .api-name:hover {
    background-color: #eee;
  }

  .api-method-item {
    font-size: .9em;
    padding: 10px 10px;
    padding-left: 35px;
    cursor: pointer;
    display: none;
    user-select: none;
  }

  .expanded .api-method-item {
    display: block;
  }

  .api-method-item:hover {
    text-decoration: underline;
  }


  .nav-bar {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    background-color: rgb(34, 34, 43);
    box-shadow: 0 3px 10px 0 rgba(0, 0, 0, .25);
    padding: 0px 20px;
    transition: all 150ms ease-in-out;
    z-index: 10;
  }
</style>