import Requests from '@/requests';

export default class AdminService {

    private static requestCounter: number = 0;

    public static async updatePlayerName(characterId: string, newName: string): Promise<boolean> {
        ++AdminService.requestCounter;
        const url = `api/admin/updateplayername/${characterId}/${newName}`;
        const result = await Requests.sendAsync(url);
        --AdminService.requestCounter;
        return result.ok && await result.json();
    }

    public static async updatePlayerStat(characterId: string, statName: string, experience: number): Promise<boolean> {
        ++AdminService.requestCounter;
        const url = `api/admin/updateplayerskill/${characterId}/${statName}/${experience}`;
        const result = await Requests.sendAsync(url);
        --AdminService.requestCounter;
        return result.ok && await result.json();
    }

    public static async resetPassword(userId: string): Promise<boolean> {
        ++AdminService.requestCounter;
        const url = `api/admin/resetpassword/${userId}`;
        const result = await Requests.sendAsync(url);
        --AdminService.requestCounter;
        return result.ok && await result.json();
    }

    public static async mergePlayer(userId: string): Promise<boolean> {
        ++AdminService.requestCounter;
        const url = `api/admin/mergeplayer/${userId}`;
        const result = await Requests.sendAsync(url);
        --AdminService.requestCounter;
        return result.ok && await result.json();
    }

    public static async kickPlayer(characterId: string): Promise<boolean> {
        ++AdminService.requestCounter;
        const url = `api/admin/kick/${characterId}`;
        const result = await Requests.sendAsync(url);
        --AdminService.requestCounter;
        return result.ok && await result.json();
    }
}
