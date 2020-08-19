import Requests from '../requests';

export class Page<T> {
    public isLoaded: boolean = false;
    public items: T[] = [];
    public sortOrder: string = "";
    public query: string = "";
}

export class EntityRepository<T> {
    public isLoading: boolean = false;
    private defaultSortOrder: string = "1UserName";
    private defaultQuery: string = "0";
    private result: any;
    private totalSize: number = 0;
    private pageSize: number = 50;
    private pages: Page<T>[] = [];
    constructor(private readonly typeName: string) {
    }
    public getPageSize(): number {
        return this.pageSize;
    }
    public getPageCount(): number {
        return Math.floor(this.totalSize / this.pageSize) + 1;
    }
    public getOffset(pageIndex: number): number {
        return pageIndex * this.pageSize;
    }
    public getTotalCount(): number {
        return this.totalSize;
    }
    public getPages(): Page<T>[] {
        return this.pages;
    }
    public getItems(pageIndex: number, sortOrder: string, query: string): T[] {
        [sortOrder, query] = this.ensureFilters(sortOrder, query);
        let page: Page<T> = this.getPage(pageIndex, sortOrder, query);
        if (page.isLoaded) {
            return page.items;
        }
        this.loadPageAsync(pageIndex, sortOrder, query);
        return [];
    }
    public async loadPageAsync(pageIndex: number, sortOrder: string, query: string) {
        [sortOrder, query] = this.ensureFilters(sortOrder, query);
        let page: Page<T> = this.getPage(pageIndex, sortOrder, query);
        if (page.isLoaded) {
            return;
        }
        const size = this.pageSize;
        const offset = size * pageIndex;
        this.isLoading = true;
        const url = `api/admin/${this.typeName}/${offset}/${size}/${sortOrder}/${query}`;
        const result = await Requests.sendAsync(url);
        if (result.ok) {
            this.result = (await result.json());
            this.totalSize = this.result.totalSize;
            page.items = this.parseItemData(this.result.items);
            page.isLoaded = true;
        }
        this.isLoading = false;
    }
    public getPage(pageIndex: number, sortOrder: string, query: string): Page<T> {
        let page: Page<T> | null = this.pages[pageIndex];
        if (page && (page.query != query || page.sortOrder != sortOrder)) {
            this.pages = [];
            page = null;
        }
        if (!page || typeof page === 'undefined') {
            page = new Page<T>();
            page.sortOrder = sortOrder;
            page.query = query;
            this.pages[pageIndex] = page;
        }
        return page;
    }
    private parseItemData(itemData: any) {
        let players: T[] = [];
        for (let player of itemData) {
            players.push(<T>player);
        }
        return players;
    }
    private ensureFilters(sortOrder: string, query: string) {
        if (!sortOrder || sortOrder == null || sortOrder.length == 0) {
            sortOrder = this.defaultSortOrder;
        }
        if (!query || query == null || query.length == 0) {
            query = this.defaultQuery;
        }
        return [sortOrder, query];
    }
}
