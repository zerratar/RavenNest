const port = window.location.port;
var DEBUG = window.location.href.indexOf("localhost") >= 0 || (port != null && port && parseInt(port) >= 500);
export default class Requests {
    public static async sendAsync(url: string, data: any | null = null) {
        if (DEBUG) {
            // localhost:5001
            url = "//localhost:" + port + "/" + (url.startsWith("/") ? url.substring(1) : url);
            // url = "https://localhost:5001/" + (url.startsWith("/") ? url.substring(1) : url);
        } else {
            url = url.startsWith("/") ? url : `/${url}`;
        }
        return await fetch(url, data);
    }
}