import Requests from '@/requests';

export default class AdminService {

    private static requestCounter: number = 0;

    public static async updatePlayerName(userId: string, newName: string): Promise<boolean>{
        ++AdminService.requestCounter;
        const url = `api/admin/updateplayername/${userId}/${newName}`;
        const result = await Requests.sendAsync(url);
        --AdminService.requestCounter;
        return result.ok && await result.json();
    }

    public static async updatePlayerStat(userId: string, statName: string, experience: number): Promise<boolean> {
        ++AdminService.requestCounter;
        const url = `api/admin/updateplayerskill/${userId}/${statName}/${experience}`;
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

    public static async kickPlayer(userId: string): Promise<boolean> {
        ++AdminService.requestCounter;
        const url = `api/admin/kick/${userId}`;
        const result = await Requests.sendAsync(url);
        --AdminService.requestCounter;
        return result.ok && await result.json();
    }
}