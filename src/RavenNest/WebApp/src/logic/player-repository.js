import Requests from '../requests';
export default class PlayerRepository {
    static getPageSize() {
        return PlayerRepository.pageSize;
    }
    static getPageCount() {
        return Math.floor(PlayerRepository.totalSize / PlayerRepository.pageSize) + 1;
    }
    static getOffset(pageIndex) {
        return pageIndex * PlayerRepository.pageSize;
    }
    static getTotalCount() {
        return PlayerRepository.totalSize;
    }
    static getPlayers(pageIndex) {
        let page = PlayerRepository.getPlayerPage(pageIndex);
        if (page.isLoaded) {
            return page.players;
        }
        if (!PlayerRepository.isLoading) {
            PlayerRepository.loadPlayersAsync(pageIndex);
        }
        return [];
    }
    static async loadPlayersAsync(pageIndex) {
        let page = PlayerRepository.getPlayerPage(pageIndex);
        if (PlayerRepository.isLoading || page.isLoaded) {
            return;
        }
        const size = PlayerRepository.pageSize;
        const offset = size * pageIndex;
        PlayerRepository.isLoading = true;
        const url = `api/admin/players/${offset}/${size}`;
        const result = await Requests.sendAsync(url);
        if (result.ok) {
            PlayerRepository.result = (await result.json());
            PlayerRepository.totalSize = PlayerRepository.result.totalSize;
            page.players = PlayerRepository.parseItemData(PlayerRepository.result.players);
            page.isLoaded = true;
        }
        PlayerRepository.isLoading = false;
    }
    static getPlayerPage(pageIndex) {
        let page = PlayerRepository.pages[pageIndex];
        if (!page || typeof page === 'undefined') {
            page = new PlayerPage();
            PlayerRepository.pages[pageIndex] = page;
        }
        return page;
    }
    static parseItemData(itemData) {
        let players = [];
        for (let player of itemData) {
            players.push(player);
        }
        return players;
    }
}
PlayerRepository.isLoading = false;
PlayerRepository.totalSize = 0;
PlayerRepository.pageSize = 50;
PlayerRepository.pages = [];
export class PlayerPage {
    constructor() {
        this.isLoaded = false;
        this.players = [];
    }
}
//# sourceMappingURL=player-repository.js.map