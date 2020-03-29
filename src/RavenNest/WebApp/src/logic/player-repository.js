import { EntityRepository } from './entity-repository';
export default class PlayerRepository {
    static get isLoading() {
        return PlayerRepository.repo.isLoading;
    }
    static getPageSize() {
        return PlayerRepository.repo.getPageSize();
    }
    static getPageCount() {
        return PlayerRepository.repo.getPageCount();
    }
    static getOffset(pageIndex) {
        return PlayerRepository.repo.getOffset(pageIndex);
    }
    static getTotalCount() {
        return PlayerRepository.repo.getTotalCount();
    }
    static getPlayer(userId) {
        for (let page of PlayerRepository.repo.getPages()) {
            const player = page.items.find(x => x.userId == userId);
            if (player) {
                return player;
            }
        }
        return null;
    }
    static getPlayers(pageIndex, sortOrder, query) {
        [sortOrder, query] = PlayerRepository.ensureFilters(sortOrder, query);
        let page = PlayerRepository.repo.getPage(pageIndex, sortOrder, query);
        if (page.isLoaded) {
            return page.items;
        }
        PlayerRepository.repo.loadPageAsync(pageIndex, sortOrder, query);
        return [];
    }
    static loadPlayersAsync(pageIndex, sortOrder, query) {
        return PlayerRepository.repo.loadPageAsync(pageIndex, sortOrder, query);
    }
    static ensureFilters(sortOrder, query) {
        if (!sortOrder || sortOrder == null || sortOrder.length == 0) {
            sortOrder = PlayerRepository.defaultSortOrder;
        }
        if (!query || query == null || query.length == 0) {
            query = PlayerRepository.defaultQuery;
        }
        return [sortOrder, query];
    }
}
PlayerRepository.defaultSortOrder = "+UserName";
PlayerRepository.defaultQuery = "-";
PlayerRepository.repo = new EntityRepository("players");
//# sourceMappingURL=player-repository.js.map