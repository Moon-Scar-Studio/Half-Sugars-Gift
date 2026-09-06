using HarmonyLib;
using Nebula.Roles.Abilities;

namespace hvtXsvc.GameMode.CowboyDuel;

/// <summary>牛仔对决不是自由模式，屏蔽 MetaAbility 带入的自由模式调试按钮（切换/复活/自尽/判定检测）。</summary>
[HarmonyPatch(typeof(MetaAbility), MethodType.Constructor)]
[HarmonyPriority(Priority.First)]
public static class CowboyDuelMetaAbilityPatch
{
    /// <summary>牛仔对决模式下跳过 MetaAbility 构造，不创建调试按钮。</summary>
    public static bool Prefix() =>
        CowboyDuelGameModeRegistration.Registration == null ||
        Nebula.Configuration.GeneralConfigurations.CurrentGameMode != CowboyDuelGameModeRegistration.Registration.Definition;
}