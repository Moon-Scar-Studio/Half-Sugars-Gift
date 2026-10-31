namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>
/// 道具躲猫猫的玩法限制：本模式中
///   1. 不能召开紧急会议；
///   2. 不能报告尸体；
///   3. 不能发动破坏（含关门），也不能使用破坏的修复控制台。
///
/// 为什么要做这么多层
/// ------------------
/// 以前只挂了一个 CheckCanPushEmergencyButtonEvent（见 PropHuntMeetingBlocker），
/// 它只影响紧急按钮小游戏里的「按钮能不能按」这层显示，
/// 报告按钮、Nebula 自己的报告 RPC、随机事件的「紧急会议」、破坏按钮、关门全都还能用。
///
/// 这里按「界面 → 本机发起 → 房主执行」三层依次封死：
///   · 界面层：隐藏报告 / 破坏按钮，紧急按钮与修复控制台变成不可用；
///   · 本机层：拦下 ReportButton / SabotageButton 的点击、CmdReportDeadBody、以破坏模式打开地图；
///   · 房主层（权威）：拦下 Nebula 的 ModReportDeadBody（所有会议的唯一入口，
///     包括原版报告、紧急按钮、溶解尸体报告、RequestEmergencyMeeting、HSG 随机事件），
///     以及 ShipStatus 上的破坏 / 关门执行。
///   即使客户端改包或其它插件绕过了界面，只要房主装了本插件就开不了会、破坏不了。
///
/// 与 PropHuntHarmony 一样全部手动 Patch，理由见该类注释（避免被 PatchAll 重复应用）。
///
/// 关于 Harmony 多前缀：本文件里的前缀都只做「拦截」，并在更底层留了同一条规则的兜底，
/// 所以不依赖「我们的前缀返回 false 之后 Nebula 的同名前缀还会不会执行」这一细节。
/// 例如 Nebula 的 SabotageButton.DoClick 前缀无论如何都会尝试打开破坏地图，
/// 但地图最终要走 MapBehaviour.Show，那里会被改回普通地图。
/// </summary>
public static class PropHuntRestrictions
{
    private static bool applied;

    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>所有限制的总闸门：只看模式，不看「道具机制」开关 —— 关掉道具机制也不该能开会。</summary>
    public static bool IsRestricted => PropHuntGameModeRegistration.IsPropHuntMode;

    public static void Apply(Harmony harmony)
    {
        if (applied) return;
        applied = true;

        var self = typeof(PropHuntRestrictions);
        MethodInfo Own(string name) => self.GetMethod(name, AnyMember)!;

        #region 会议 / 报告

        // 房主权威入口：Nebula 用 ModReportDeadBody 取代了原版 ReportDeadBody，
        // 原版报告、紧急按钮、溶解尸体、Player.RequestEmergencyMeeting 最终都汇到这里。
        // 它是 internal，所以用名字反射取。
        Patch(harmony, "MeetingHudExtension.ModReportDeadBody",
            () => typeof(MeetingHudExtension).GetMethod("ModReportDeadBody", AnyMember),
            prefix: Own(nameof(BlockPrefix)), priority: Priority.First);

        // 原版的房主入口（Nebula 在这里也挂了前缀转去 ModReportDeadBody），再挡一道。
        Patch(harmony, "PlayerControl.ReportDeadBody",
            () => typeof(PlayerControl).GetMethod(nameof(PlayerControl.ReportDeadBody), AnyMember),
            prefix: Own(nameof(BlockPrefix)), priority: Priority.First);

        // 客户端发起：报告与紧急按钮都会调用它把请求发给房主。
        Patch(harmony, "PlayerControl.CmdReportDeadBody",
            () => typeof(PlayerControl).GetMethod(nameof(PlayerControl.CmdReportDeadBody), AnyMember),
            prefix: Own(nameof(BlockPrefix)), priority: Priority.First);

        // 报告按钮（含键盘快捷键，快捷键也是调用 DoClick）。
        Patch(harmony, "ReportButton.DoClick",
            () => typeof(ReportButton).GetMethod(nameof(ReportButton.DoClick), AnyMember),
            prefix: Own(nameof(BlockPrefix)), priority: Priority.First);

        // 紧急按钮控制台：直接不可用（不高亮、按使用键无反应）。
        Patch(harmony, "SystemConsole.CanUse",
            () => typeof(SystemConsole).GetMethod(nameof(SystemConsole.CanUse), AnyMember),
            postfix: Own(nameof(SystemConsoleCanUsePostfix)), priority: Priority.Last);

        #endregion

        #region 破坏 / 关门 / 修复

        // 破坏按钮（含键盘快捷键）。
        Patch(harmony, "SabotageButton.DoClick",
            () => typeof(SabotageButton).GetMethod(nameof(SabotageButton.DoClick), AnyMember),
            prefix: Own(nameof(BlockPrefix)), priority: Priority.First);

        // 破坏地图：无论从哪条路打开（破坏按钮、内鬼点地图按钮、其它插件），都改成普通地图。
        // 没有破坏地图就点不到任何破坏 / 关门按钮。
        Patch(harmony, "MapBehaviour.Show",
            () => typeof(MapBehaviour).GetMethod(nameof(MapBehaviour.Show), AnyMember),
            prefix: Own(nameof(MapShowPrefix)), priority: Priority.First);

        // 房主权威：破坏请求最终都以 SystemTypes.Sabotage 走 ShipStatus.UpdateSystem。
        // 两个重载都挂上（byte 重载在原生代码里会转调 reader 重载，但多挡一层没有坏处）。
        Patch(harmony, "ShipStatus.UpdateSystem(reader)",
            () => typeof(ShipStatus).GetMethod(nameof(ShipStatus.UpdateSystem), AnyMember, null,
                new[] { typeof(SystemTypes), typeof(PlayerControl), typeof(Hazel.MessageReader) }, null),
            prefix: Own(nameof(UpdateSystemPrefix)), priority: Priority.First);
        Patch(harmony, "ShipStatus.UpdateSystem(byte)",
            () => typeof(ShipStatus).GetMethod(nameof(ShipStatus.UpdateSystem), AnyMember, null,
                new[] { typeof(SystemTypes), typeof(PlayerControl), typeof(byte) }, null),
            prefix: Own(nameof(UpdateSystemPrefix)), priority: Priority.First);

        // 关门：客户端发起 + 房主执行。
        Patch(harmony, "ShipStatus.RpcCloseDoorsOfType",
            () => typeof(ShipStatus).GetMethod(nameof(ShipStatus.RpcCloseDoorsOfType), AnyMember),
            prefix: Own(nameof(BlockPrefix)), priority: Priority.First);
        Patch(harmony, "ShipStatus.CloseDoorsOfType",
            () => typeof(ShipStatus).GetMethod(nameof(ShipStatus.CloseDoorsOfType), AnyMember),
            prefix: Own(nameof(BlockPrefix)), priority: Priority.First);

        // 修复控制台（灯、通讯、反应堆、氧气……）：本模式不允许使用。
        Patch(harmony, "Console.CanUse",
            () => typeof(global::Console).GetMethod(nameof(global::Console.CanUse), AnyMember),
            postfix: Own(nameof(ConsoleCanUsePostfix)), priority: Priority.Last);

        #endregion

        #region 按钮显示

        // Nebula 每帧在 HudManagerExtension.UpdateHudContent 里决定报告 / 破坏按钮是否显示，
        // 在它之后把两个按钮收起来。
        Patch(harmony, "HudManagerExtension.UpdateHudContent",
            () => typeof(HudManagerExtension).GetMethod(nameof(HudManagerExtension.UpdateHudContent), AnyMember),
            postfix: Own(nameof(UpdateHudContentPostfix)), priority: Priority.Last);

        #endregion

        HsgDebug.Log("[HSG] 道具躲猫猫：会议 / 报告 / 破坏限制补丁应用完成。");
    }

    private static void Patch(Harmony harmony, string label, Func<MethodBase?> resolve,
        MethodInfo? prefix = null, MethodInfo? postfix = null, int priority = Priority.Normal)
    {
        try
        {
            var original = resolve();
            if (original == null)
            {
                HsgDebug.LogError($"[HSG] 道具躲猫猫限制：找不到 {label}，该补丁跳过。");
                return;
            }

            HarmonyMethod? prefixMethod = prefix == null ? null : new HarmonyMethod(prefix) { priority = priority };
            HarmonyMethod? postfixMethod = postfix == null ? null : new HarmonyMethod(postfix) { priority = priority };
            harmony.Patch(original, prefixMethod, postfixMethod);
        }
        catch (Exception exception)
        {
            HsgDebug.LogError($"[HSG] 道具躲猫猫限制：补丁 {label} 失败：{exception}");
        }
    }

    #region 补丁实现

    /// <summary>通用拦截：躲猫猫模式下直接跳过原方法。</summary>
    public static bool BlockPrefix() => !IsRestricted;

    /// <summary>紧急按钮控制台不可用。</summary>
    public static void SystemConsoleCanUsePostfix(SystemConsole __instance, ref float __result, ref bool canUse, ref bool couldUse)
    {
        if (!IsRestricted || __instance == null) return;

        try
        {
            var prefab = __instance.MinigamePrefab;
            if (prefab == null || prefab.TryCast<EmergencyMinigame>() == null) return;

            canUse = false;
            couldUse = false;
            __result = float.MaxValue;
        }
        catch { }
    }

    /// <summary>破坏修复控制台不可用。判定方式与 Nebula 的 ConsoleCanUsePatch 一致：控制台的任务类型属于破坏任务。</summary>
    public static void ConsoleCanUsePostfix(global::Console __instance, ref float __result, ref bool canUse, ref bool couldUse)
    {
        if (!IsRestricted || __instance == null) return;

        try
        {
            var ship = ShipStatus.Instance;
            if (ship == null || ship.SpecialTasks == null || __instance.TaskTypes == null) return;

            bool isRepairConsole = false;
            foreach (var task in ship.SpecialTasks)
            {
                if (task != null && __instance.TaskTypes.Contains(task.TaskType))
                {
                    isRepairConsole = true;
                    break;
                }
            }
            if (!isRepairConsole) return;

            canUse = false;
            couldUse = false;
            __result = float.MaxValue;
        }
        catch { }
    }

    /// <summary>把破坏地图改成普通地图。</summary>
    public static void MapShowPrefix(MapOptions opts)
    {
        if (!IsRestricted || opts == null) return;
        if (opts.Mode == MapOptions.Modes.Sabotage) opts.Mode = MapOptions.Modes.Normal;
    }

    /// <summary>房主拒绝一切破坏请求。只拦 SystemTypes.Sabotage（发动），其余系统（开门、梯子、平台等）照常。</summary>
    public static bool UpdateSystemPrefix(SystemTypes systemType)
    {
        if (!IsRestricted) return true;
        if (systemType != SystemTypes.Sabotage) return true;

        HsgDebug.Log("[HSG] 道具躲猫猫：已拒绝一次破坏请求。");
        return false;
    }

    /// <summary>隐藏报告按钮与破坏按钮。</summary>
    public static void UpdateHudContentPostfix()
    {
        if (!IsRestricted) return;
        if (!HudManager.InstanceExists) return;

        try
        {
            var hud = HudManager.Instance;
            if (hud.ReportButton != null) hud.ReportButton.ToggleVisible(false);
            if (hud.SabotageButton != null) hud.SabotageButton.ToggleVisible(false);
        }
        catch { }
    }

    #endregion
}
