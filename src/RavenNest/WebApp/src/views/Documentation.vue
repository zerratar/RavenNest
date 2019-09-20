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
  import {
    Component,
    Vue
  } from 'vue-property-decorator';
  import router from 'vue-router';

  @Component({})
  export default class Home extends Vue {

    private activeApi: any = null;
    private activePage: any = null;
    private apiDocument: any = null;

    getExample(method: any): string {
      return JSON.stringify(JSON.parse(method.response.example), null, 4);
    }

    getRequestContent(api: any, method: any): string {
      let requestPath = `${api.path}${method.path}`;
      if (requestPath.endsWith("/")) requestPath = requestPath.slice(0, -1);
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
      if (this.apiDocument == null) return [];
      return this.apiDocument.pages;
    }

    private get apis(): any[] {
      if (this.apiDocument == null) return [];
      return this.apiDocument.apis;
    }

    apiClicked(api: any) {
      if (this.activeApi != null) {
        this.activeApi.active = false;        
      }

      api.active = true;
      this.activeApi = api;
    }

    updated() {
      document.querySelectorAll('pre code').forEach((block) => {          
          (<any>(<any>window)["hljs"]).highlightBlock(block);
      });
    }

    mounted() {
      const winData = <any>(<any>window)["data"];
      this.apiDocument = winData;//window["data"];
      this.activeApi = this.apis[0];
      this.activeApi.active = true;
    
    // const apiDocument = data;
      // const apiNavigation = document.querySelector(".navigation-side");
      // const apiPage = document.querySelector(".main");

      // apiDocument.apis.forEach(api => {
      //   const name = api.name;
      //   // const path = api.Path;
      //   // const desc = api.Description;
      //   const methods = api.methods;
      //   const navItem = document.createElement("div");
      //   navItem.addEventListener("click", () => {
      //     if (activePage) {
      //       activePage.classList.toggle("expanded");
      //     }
      //     navItem.classList.toggle("expanded");
      //     activePage = navItem;

      //     apiPage.innerHTML = "";
      //     const pageHeader = document.createElement("h1");
      //     pageHeader.innerText = name;
      //     apiPage.appendChild(pageHeader);

      //     methods.forEach(method => {
      //       const methodHeader = document.createElement("h2");
      //       methodHeader.innerText = method.name;
      //       apiPage.appendChild(methodHeader);

      //       const methodDescription = document.createElement("p");
      //       methodDescription.innerText = method.description;
      //       apiPage.appendChild(methodDescription);


      //       if (method.parameters.length > 0) {

      //         const paramLabel = document.createElement("h4");
      //         paramLabel.innerText = "Parameters";
      //         apiPage.appendChild(paramLabel);

      //         let parameterstring = "";
      //         const param = document.createElement("pre");
      //         parameterstring += `<code class='csharp'>`;
      //         for (const parameter of method.parameters) {
      //           parameterstring += `${parameter.type} ${parameter.name}\r\n`;
      //         }

      //         parameterstring += `</code>`;
      //         param.innerHTML = parameterstring;
      //         apiPage.appendChild(param);
      //       }
      //       const requestLabel = document.createElement("h4");
      //       requestLabel.innerText = "Request";
      //       apiPage.appendChild(requestLabel);

      //       let requestPath = `${api.path}${method.path}`;
      //       if (requestPath.endsWith("/")) requestPath = requestPath.slice(0, -1);
      //       let requestContent = `${method.method} ${requestPath} HTTP/1.1\r\n`;
      //       if (method.requestBody != null) {
      //         const contentType = method.requestBody.contentType;
      //         const example = JSON.stringify(JSON.parse(method.requestBody.example), null, 4);
      //         requestContent +=
      //           `Host: ravenfall.stream\r\n` +
      //           `Content-Type: ${contentType}\r\n` +
      //           `Content-Length: ${example.length}\r\n` +
      //           `\r\n` +
      //           `${example}`;
      //       }

      //       const requestCode = document.createElement("pre");
      //       requestCode.innerHTML = `<code class='http'>${requestContent}</code>`;
      //       apiPage.appendChild(requestCode);


      //       if (method.response != null) {
      //         const responseLabel = document.createElement("h4");
      //         responseLabel.innerText = "Response";
      //         apiPage.appendChild(responseLabel);

      //         const responseType = document.createElement("p");
      //         responseType.innerText = method.response.returnType;
      //         apiPage.appendChild(responseType);

      //         const responseContent = JSON.stringify(JSON.parse(method.response.example), null, 4);;
      //         const responseCode = document.createElement("pre");
      //         responseCode.innerHTML = `<code class='json'>${responseContent}</code>`;
      //         apiPage.appendChild(responseCode);
      //       }
      //     });

      //     document.querySelectorAll('pre code').forEach((block) => {
      //       hljs.highlightBlock(block);
      //     });
      //   });
      //   navItem.classList.add("api-item");

      //   const navText = document.createElement("div");
      //   navText.classList.add("api-name");
      //   navText.innerText = name;
      //   navItem.appendChild(navText)

      //   // const navLinks = document.createElement("div");
      //   // navLinks.classList.add("api-methods");
      //   // navItem.appendChild(navLinks);

      //   methods.forEach(method => {
      //     const methodItem = document.createElement("div");
      //     methodItem.classList.add("api-method-item");
      //     methodItem.innerText = method.name;
      //     methodItem.addEventListener("click", e => {
      //       e.stopPropagation();

      //     });
      //     navItem.appendChild(methodItem);
      //   });

      //   apiNavigation.appendChild(navItem);
      // });
    }
  }
</script>

<style scoped>
  .documentation {
    font-family: 'Heebo', sans-serif;
    color: #333;
  }

  .navigation-side {
    position: fixed;
    overflow-y: auto;
    top: 92px;
    left: 0;
    bottom: 0;
    width: 350px;
    background-color: #fafafa;
    border-right: 1px solid #ddd;
  }

  .main {
    position: fixed;
    overflow-y: auto;
    top: 92px;
    left: 350px;
    right: 0;
    bottom: 0;
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