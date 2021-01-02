using System;
using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IGameManager
    {
        GameInfo GetGameInfo(SessionToken session);
        EventCollection GetGameEvents(SessionToken session);
        ScrollUseResult UseScroll(SessionToken session, Guid characterId, ScrollType scrollType);

        //bool Join(string userId, string targetUserId);
        //bool Leave(string userId);
        //bool WalkTo(string userId, int x, int y, int z);
        //bool Attack(string userId, string targetId, AttackType attackType);
        //bool ObjectAction(string userId, string targetId, ObjectActionType actionType);
        //bool SetTask(string userId, string task, string taskArgument);
        //bool JoinRaid(string userId);
        //bool JoinDungeon(string userId);
        //bool JoinArena(string userId);
        //bool DuelAccept(string userId);
        //bool DuelDecline(string userId);
        //bool DuelRequest(string userId, string targetUserId);
        //bool Travel(string userId, string island);
    }
}
