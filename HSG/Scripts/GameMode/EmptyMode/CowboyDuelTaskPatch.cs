using HarmonyLib;

namespace hvtXsvc.GameMode.CowboyDuel;

/// <summary>牛仔对决不生成原版任务列表。</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetTasks))]
[HarmonyPriority(Priority.First)]
public static class CowboyDuelTaskPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo.TaskInfo> tasks)
    {
        if (!__instance.AmOwner) return true;
        if (CowboyDuelGameModeRegistration.Registration == null) return true;
        if (Nebula.Configuration.GeneralConfigurations.CurrentGameMode != CowboyDuelGameModeRegistration.Registration.Definition) return true;

        tasks.Clear();
        return true;
    }
}

/// <summary>游戏开始时触发牛仔对决初始化。GameOperatorManager 每局重建，插件加载期订阅无效，故直接补丁 OnGameStart。由 Apply 手动应用，不参与 PatchAll。</summary>
public static class CowboyDuelGameStartPatch
{
    private static bool patched;

    /// <summary>NebulaGameManager 是 Nebula 内部类型，需反射找到 OnGameStart 并手动补丁。</summary>
    public static void Apply(Harmony harmony)
    {
        if (patched) return;
        patched = true;

        var managerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Nebula.Game.NebulaGameManager", false))
            .FirstOrDefault(type => type is not null);
        var method = managerType?.GetMethod("OnGameStart");
        if (method == null)
        {
            HsgDebug.LogError("[HSG] 找不到 NebulaGameManager.OnGameStart，牛仔对决无法开局。");
            return;
        }
        harmony.Patch(method, postfix: new HarmonyMethod(typeof(CowboyDuelGameStartPatch), nameof(Postfix)));
    }

    private static void Postfix()
    {
        if (CowboyDuelGameModeRegistration.Registration == null) return;
        if (Nebula.Configuration.GeneralConfigurations.CurrentGameMode != CowboyDuelGameModeRegistration.Registration.Definition) return;
        CowboyDuelGameModeModule.StartRound();
    }
}
