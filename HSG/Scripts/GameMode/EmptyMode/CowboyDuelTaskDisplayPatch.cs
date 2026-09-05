using HarmonyLib;

namespace hvtXsvc.GameMode.CowboyDuel;

/// <summary>牛仔对决只显示枪械组装任务。</summary>
[HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText))]
[HarmonyPriority(Priority.First)]
public static class CowboyDuelTaskDisplayPatch
{
    public static void Postfix(TaskPanelBehaviour __instance)
    {
        if (CowboyDuelGameModeRegistration.Registration == null) return;
        if (Nebula.Configuration.GeneralConfigurations.CurrentGameMode != CowboyDuelGameModeRegistration.Registration.Definition) return;

        __instance.taskText.text = Language.Translate("task.hsg.cowboyDuel.assembly");
    }
}
