const port = window.location.port;
var DEBUG = window.location.href.indexOf("localhost") >= 0 || (port != null && port && parseInt(port) >= 500);
export default class Requests {
    public static async sendAsync(url: string, data: any | null = null) {
        if (DEBUG) {
            url = "https://localhost:5001/" + (url.startsWith("/") ? url.substring(1) : url);
        }
        return await fetch(url, data);
    }
}