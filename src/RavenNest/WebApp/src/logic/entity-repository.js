import Requests from '../requests';
export class Page {
    constructor() {
        this.isLoaded = false;
        this.items = [];
        this.sortOrder = "";
        this.query = "";
    }
}
export class EntityRepository {
    constructor(typeName) {
        this.typeName = typeName;
        this.isLoading = false;
        this.defaultSortOrder = "+UserName";
        this.defaultQuery = "-";
        this.totalSize = 0;
        this.pageSize = 50;
        this.pages = [];
    }
    getPageSize() {
        return this.pageSize;
    }
    getPageCount() {
        return Math.floor(this.totalSize / this.pageSize) + 1;
    }
    getOffset(pageIndex) {
        return pageIndex * this.pageSize;
    }
    getTotalCount() {
        return this.totalSize;
    }
    getPages() {
        return this.pages;
    }
    getItems(pageIndex, sortOrder, query) {
        [sortOrder, query] = this.ensureFilters(sortOrder, query);
        let page = this.getPage(pageIndex, sortOrder, query);
        if (page.isLoaded) {
            return page.items;
        }
        this.loadPageAsync(pageIndex, sortOrder, query);
        return [];
    }
    async loadPageAsync(pageIndex, sortOrder, query) {
        [sortOrder, query] = this.ensureFilters(sortOrder, query);
        let page = this.getPage(pageIndex, sortOrder, query);
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
    getPage(pageIndex, sortOrder, query) {
        let page = this.pages[pageIndex];
        if (page && (page.query != query || page.sortOrder != sortOrder)) {
            this.pages = [];
            page = null;
        }
        if (!page || typeof page === 'undefined') {
            page = new Page();
            page.sortOrder = sortOrder;
            page.query = query;
            this.pages[pageIndex] = page;
        }
        return page;
    }
    parseItemData(itemData) {
        let players = [];
        for (let player of itemData) {
            players.push(player);
        }
        return players;
    }
    ensureFilters(sortOrder, query) {
        if (!sortOrder || sortOrder == null || sortOrder.length == 0) {
            sortOrder = this.defaultSortOrder;
        }
        if (!query || query == null || query.length == 0) {
            query = this.defaultQuery;
        }
        return [sortOrder, query];
    }
}
//# sourceMappingURL=entity-repository.js.map