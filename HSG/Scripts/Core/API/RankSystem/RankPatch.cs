//using HarmonyLib;
//using System;
//using Virial;
//using Virial.Assignable;
//using Virial.Game;
//using Virial.Events.Game;
//using Virial.Events.Player;

//namespace NebulaN.Systems.RankSystem;

//[HarmonyPatch]
//public static class RankPatch
//{
//    [Local]
//    static void OnGameEnds(GameEndEvent ev)
//    {
//        if (!IsStandardMode()) return;
//        var game = NebulaAPI.CurrentGame;
//        if (game == null) return;
//        var winnersMask = ev.EndState.Winners;
//        foreach (var player in game.GetAllPlayers())
//        {
//            if (player == null) continue;
//            bool win = winnersMask.Test(player);
//            CalculatePlayerRank(player, win);
//        }
//        RankSystem.Save();
//    }

//    private static void CalculatePlayerRank(GamePlayer player, bool win)
//    {
//        if (player.Role is IRankSystem custom && custom.CloseRank) return;
//        int score = GetResultScore(player, win);
//        if (score == 0) return;
//        RankSystem.AddRankScore(player.PlayerId, score, win ? "Game Win" : "Game Lose");
//    }

//    private static int GetResultScore(GamePlayer player, bool win)
//    {
//        if (player.Role is IRankSystem custom)
//            return win ? custom.WinScore : custom.LoseScore;
//        switch (player.Role.Role.Category)
//        {
//            case RoleCategory.CrewmateRole: return win ? 5 : -5;
//            case RoleCategory.ImpostorRole: return win ? 10 : -10;
//            case RoleCategory.NeutralRole: return win ? 15 : -10;
//            default: return 0;
//        }
//    }

//    private static bool IsStandardMode() => NebulaAPI.CurrentGame?.GetModule<IGameModeStandard>() != null;
//}

//#region Extra Score API
//public static class RankScoreEvents
//{
//    public static void OnTaskComplete(GamePlayer player)
//    {
//        if (!RankSystemEnabled()) return;
//        RankSystem.AddRankScore(player.PlayerId, 1, "Complete Task");
//    }

//    public static void OnRepairSabotage(GamePlayer player)
//    {
//        if (!RankSystemEnabled()) return;
//        if (player.Role.Role.Category == RoleCategory.ImpostorRole) return;
//        RankSystem.AddRankScore(player.PlayerId, 1, "Repair Sabotage");
//    }

//    public static void OnKill(GamePlayer killer)
//    {
//        if (!RankSystemEnabled()) return;
//        int score = 0;
//        switch (killer.Role.Role.Category)
//        {
//            case RoleCategory.ImpostorRole: score = 2; break;
//            case RoleCategory.NeutralRole: score = 5; break;
//        }
//        if (score != 0) RankSystem.AddRankScore(killer.PlayerId, score, "Kill Player");
//    }

//    private static bool RankSystemEnabled() => NebulaAPI.CurrentGame?.GetModule<IGameModeStandard>() != null;
//}
//#endregion