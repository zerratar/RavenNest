import { Player, Statistics } from './models';
import Requests from '../requests';

export default class PlayerRepository {

    public static isLoading: boolean = false;

    private static result: any;
    private static totalSize: number = 0;
    private static pageSize: number = 50;
    private static pages: PlayerPage[] = [];

    public static getPageSize(): number {
        return PlayerRepository.pageSize;
    }

    public static getPageCount(): number {
        return Math.floor(PlayerRepository.totalSize / PlayerRepository.pageSize) + 1;
    }

    public static getOffset(pageIndex: number): number {
        return pageIndex * PlayerRepository.pageSize;
    }

    public static getTotalCount(): number {
        return PlayerRepository.totalSize;
    }

    public static getPlayer(userId: string): Player | null {
        for (let page of PlayerRepository.pages) {
            const player = page.players.find(x => x.userId == userId);
            if (player) {
                return player;
            }
        }
        return null;
    }

    public static getPlayers(pageIndex: number): Player[] {
        let page: PlayerPage = PlayerRepository.getPlayerPage(pageIndex);

        if (page.isLoaded) {
            return page.players;
        }

        if (!PlayerRepository.isLoading) {
            PlayerRepository.loadPlayersAsync(pageIndex);
        }

        return [];
    }

    public static async loadPlayersAsync(pageIndex: number) {
        let page: PlayerPage = PlayerRepository.getPlayerPage(pageIndex);
        
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

    private static getPlayerPage(pageIndex: number):PlayerPage {
        let page: PlayerPage = PlayerRepository.pages[pageIndex];  
        
        if (!page || typeof page  === 'undefined') {
            page = new PlayerPage();
            PlayerRepository.pages[pageIndex] = page;
        }
        return page;
    }

    private static parseItemData(itemData: any) {
        let players: Player[] = [];
        for (let player of itemData) {
            players.push(<Player>player);
        }
        return players;
    }
}

export class PlayerPage {
    public isLoaded: boolean = false;
    public players: Player[] = [];
}