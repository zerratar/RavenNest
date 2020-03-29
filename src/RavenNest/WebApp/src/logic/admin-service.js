import Requests from '@/requests';
export default class AdminService {
    static async updatePlayerName(userId, newName) {
        ++AdminService.requestCounter;
        const url = `api/admin/updateplayername/${userId}/${newName}`;
        const result = await Requests.sendAsync(url);
        --AdminService.requestCounter;
        return result.ok && await result.json();
    }
    static async updatePlayerStat(userId, statName, experience) {
        ++AdminService.requestCounter;
        const url = `api/admin/updateplayerskill/${userId}/${statName}/${experience}`;
        const result = await Requests.sendAsync(url);
        --AdminService.requestCounter;
        return result.ok && await result.json();
    }
}
AdminService.requestCounter = 0;
//# sourceMappingURL=admin-service.js.map