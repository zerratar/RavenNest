import { EntityRepository } from './entity-repository';
export default class SessionRepository {
    static get isLoading() {
        return SessionRepository.repo.isLoading;
    }
    static getPageSize() {
        return SessionRepository.repo.getPageSize();
    }
    static getPageCount() {
        return SessionRepository.repo.getPageCount();
    }
    static getOffset(pageIndex) {
        return SessionRepository.repo.getOffset(pageIndex);
    }
    static getTotalCount() {
        return SessionRepository.repo.getTotalCount();
    }
    static getSession(sessionId) {
        for (let page of SessionRepository.repo.getPages()) {
            const player = page.items.find(x => x.id == sessionId);
            if (player) {
                return player;
            }
        }
        return null;
    }
    static getSessions(pageIndex, sortOrder, query) {
        [sortOrder, query] = SessionRepository.ensureFilters(sortOrder, query);
        let page = SessionRepository.repo.getPage(pageIndex, sortOrder, query);
        if (page.isLoaded) {
            return page.items;
        }
        SessionRepository.repo.loadPageAsync(pageIndex, sortOrder, query);
        return [];
    }
    static loadSessionsAsync(pageIndex, sortOrder, query) {
        return SessionRepository.repo.loadPageAsync(pageIndex, sortOrder, query);
    }
    static ensureFilters(sortOrder, query) {
        if (!sortOrder || sortOrder == null || sortOrder.length == 0) {
            sortOrder = SessionRepository.defaultSortOrder;
        }
        if (!query || query == null || query.length == 0) {
            query = SessionRepository.defaultQuery;
        }
        return [sortOrder, query];
    }
}
SessionRepository.defaultSortOrder = "+Id";
SessionRepository.defaultQuery = "-";
SessionRepository.repo = new EntityRepository("sessions");
//# sourceMappingURL=session-repository.js.map