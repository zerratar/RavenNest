const port = window.location.port;
var DEBUG = window.location.href.indexOf("localhost") >= 0 || (port != null && port && parseInt(port) >= 500);
export default class Requests {
    static async sendAsync(url, data = null) {
        if (DEBUG) {
            url = "https://localhost:5001/" + (url.startsWith("/") ? url.substring(1) : url);
        }
        return await fetch(url, data);
    }
}
//# sourceMappingURL=requests.js.map