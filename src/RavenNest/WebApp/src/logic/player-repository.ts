import { Player } from './models';
import { EntityRepository, Page } from './entity-repository';

export default class PlayerRepository {

    private static defaultSortOrder: string = '1UserName';
    private static defaultQuery: string = '0';
    private static repo: EntityRepository<Player> = new EntityRepository<Player>('players');

    public static get isLoading(): boolean {
        return PlayerRepository.repo.isLoading;
    }

    public static getPageSize(): number {
        return PlayerRepository.repo.getPageSize();
    }

    public static getPageCount(): number {
        return PlayerRepository.repo.getPageCount();
    }

    public static getOffset(pageIndex: number): number {
        return PlayerRepository.repo.getOffset(pageIndex);
    }

    public static getTotalCount(): number {
        return PlayerRepository.repo.getTotalCount();
    }

    public static getPlayer(userId: string): Player | null {
        for (const page of PlayerRepository.repo.getPages()) {
            const player = page.items.find(x => x.userId === userId);
            if (player) {
                return player;
            }
        }
        return null;
    }

    public static getPlayers(pageIndex: number, sortOrder: string, query: string): Player[] {
        [sortOrder,query] = PlayerRepository.ensureFilters(sortOrder, query);

        const page: Page<Player> = PlayerRepository.repo.getPage(pageIndex, sortOrder, query);

        if (page.isLoaded) {
            return page.items;
        }

        PlayerRepository.repo.loadPageAsync(pageIndex, sortOrder, query);

        return [];
    }

    public static loadPlayersAsync(pageIndex: number, sortOrder: string, query: string) {
        return PlayerRepository.repo.loadPageAsync(pageIndex, sortOrder, query);
    }

    private static ensureFilters(sortOrder: string, query: string) {
        if (!sortOrder || sortOrder == null || sortOrder.length === 0) {
            sortOrder = PlayerRepository.defaultSortOrder;
        }

        if (!query || query == null || query.length === 0) {
            query = PlayerRepository.defaultQuery;
        }
        return [sortOrder, query];
    }
}
