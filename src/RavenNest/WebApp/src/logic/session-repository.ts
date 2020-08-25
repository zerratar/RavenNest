import { GameSession } from './models';
import { EntityRepository, Page } from './entity-repository';

export default class SessionRepository {
    private static defaultSortOrder: string = '1Id';
    private static defaultQuery: string = '0';
    private static repo: EntityRepository<GameSession> = new EntityRepository<GameSession>('sessions');

    public static get isLoading(): boolean {
        return SessionRepository.repo.isLoading;
    }

    public static getPageSize(): number {
        return SessionRepository.repo.getPageSize();
    }

    public static getPageCount(): number {
        return SessionRepository.repo.getPageCount();
    }

    public static getOffset(pageIndex: number): number {
        return SessionRepository.repo.getOffset(pageIndex);
    }

    public static getTotalCount(): number {
        return SessionRepository.repo.getTotalCount();
    }

    public static getSession(sessionId: string): GameSession | null {
        for (const page of SessionRepository.repo.getPages()) {
            const player = page.items.find(x => x.id === sessionId);
            if (player) {
                return player;
            }
        }
        return null;
    }

    public static getSessions(pageIndex: number, sortOrder: string, query: string): GameSession[] {
        [sortOrder,query] = SessionRepository.ensureFilters(sortOrder, query);
        const page: Page<GameSession> = SessionRepository.repo.getPage(pageIndex, sortOrder, query);
        if (page.isLoaded) {
            return page.items;
        }
        SessionRepository.repo.loadPageAsync(pageIndex, sortOrder, query);
        return [];
    }

    public static loadSessionsAsync(pageIndex: number, sortOrder: string, query: string) {
        return SessionRepository.repo.loadPageAsync(pageIndex, sortOrder, query);
    }

    private static ensureFilters(sortOrder: string, query: string) {
        if (!sortOrder || sortOrder == null || sortOrder.length === 0) {
            sortOrder = SessionRepository.defaultSortOrder;
        }
        if (!query || query == null || query.length === 0) {
            query = SessionRepository.defaultQuery;
        }
        return [sortOrder, query];
    }
}
