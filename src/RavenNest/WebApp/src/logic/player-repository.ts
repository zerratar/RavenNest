import { Player, Statistics } from './models';
import Requests from '../requests';

export default class PlayerRepository {

    public static isLoading: boolean = false;

    private static result: any;
    private static totalSize: number = 0;
    private static pageSize: number = 25;
    private static pages: PlayerPage[] = [];

    public static getPlayers(pageIndex: number): Player[] {
        let page: PlayerPage;

        if (!PlayerRepository.pages || PlayerRepository.pages.length < pageIndex) {
            page = PlayerRepository.pages[pageIndex] = new PlayerPage();
        } else {
            page = PlayerRepository.pages[pageIndex];
        }

        if (page.isLoaded) {
            return page.players;
        }

        if (!PlayerRepository.isLoading) {
            PlayerRepository.loadPlayersAsync(pageIndex);
        }

        return [];
    }

    public static async loadPlayersAsync(pageIndex: number) {
        let page: PlayerPage = PlayerRepository.pages[pageIndex];
        if (PlayerRepository.isLoading || page.isLoaded) {
            return;
        }

        const size = PlayerRepository.pageSize;
        const offset = size * pageIndex;

        PlayerRepository.isLoading = true;
        const url = `api/admin/players/${offset}/${size}`;
        const result = await Requests.sendAsync(url);
        if (result.ok) {
            this.result = (await result.json());
            this.totalSize = this.result.totalSize;
            page.players = this.parseItemData(this.result.players);
            page.isLoaded = true;
        }
        PlayerRepository.isLoading = false;
    }

    private static parseItemData(itemData: any) {
        let players: Player[] = [];
        for (let raw of itemData) {            
            players.push(<Player>raw);
        }
        return players;
    }
}

export class PlayerPage {
    public isLoaded: boolean = false;
    public players: Player[] = [];
}