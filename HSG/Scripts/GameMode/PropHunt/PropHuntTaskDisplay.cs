namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>
/// 任务屏蔽与任务栏文案。
///
/// 本模式没有任务，但 IGameModeStandard 会照常给船员发任务。
/// 胜负判定已经被 PropHuntRuntime.OnEndCriteriaPreMet 拦住了，
/// 这里负责把「任务」这件事从玩法和界面上彻底抹掉，一共三层：
///
///   1. PlayerControl.SetTasks 前缀清空列表
///      → 不生成任何原版任务实体，控制台按不动，进度条永远不动。
///
///   2. 房主开局对所有玩家调 PlayerTaskState.WaiveAllTasksAsOutsider()
///      → 第 1 层只挡住了原版任务实体，Nebula 自己的任务状态（Quota / TotalTasks）
///        仍然是按游戏设置算出来的非零值。而 PlayerModInfo 在拼名字时的条件是
///        「Quota > 0 || TotalTasks > 0」就在名字后面挂进度，
///        不清掉的话死人视角下每个名字后面都会跟一串 (0/5)。
///        WaiveAllTasksAsOutsider 内部会 RpcSyncTaskState 广播，房主调一次即可。
///
///   3. 隐藏 ProgressTracker（原版左上角任务进度条）
///      → 纯观感。开局隐藏、结束恢复；不能用 ProgressTracker.Start 补丁，
///        因为 HudManager 在进大厅时就建好了，那时还没选模式，
///        而且一旦隐藏不恢复会污染之后的普通对局。
///
/// 任务栏文案走 TaskPanelBehaviour.SetTaskText 的后缀（Priority.Last），
/// 而不是 Nebula 的 PlayerTaskTextLocalEvent —— 后者虽然 ReplaceBody 是 public，
/// 但事件类本身是 internal，拿它当方法参数类型会撞上和 CS0060 同源的
/// 可访问性一致性检查（CS0051）。这条路径零 internal 类型，稳。
/// </summary>
public static class PropHuntTaskDisplay
{
    #region 文案

    private const string HiderColor = "#77DDFF";
    private const string SeekerColor = "#FF6B6B";
    private const string DeadColor = "#9AA0A6";

    /// <summary>按本地玩家的阵营与生死拼出任务栏那一行。</summary>
    public static string BuildTaskText()
    {
        var local = GamePlayer.LocalPlayer;
        if (local == null) return string.Empty;

        // 死亡优先于阵营：抓捕者若因故死亡也走同一行。
        if (local.IsDead)
            return $"<color={DeadColor}>{Language.Translate("hsg.propHunt.task.dead")}</color>";

        return local.IsImpostor
            ? $"<color={SeekerColor}>{Language.Translate("hsg.propHunt.task.seeker")}</color>"
            : $"<color={HiderColor}>{Language.Translate("hsg.propHunt.task.hider")}</color>";
    }

    /// <summary>
    /// TaskPanelBehaviour.SetTaskText 的后缀，以 Priority.Last 应用。
    /// Nebula 自己的 TaskTextPatch 是默认优先级（Normal），所以我们一定在它之后覆盖。
    /// </summary>
    public static void SetTaskTextPostfix(TaskPanelBehaviour __instance)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;
        if (__instance == null) return;

        var text = __instance.taskText;
        if (text == null) return;

        text.text = BuildTaskText();
    }

    /// <summary>
    /// 每帧兜底写入。
    /// 原版在任务数为 0 时是否仍然调用 SetTaskText 无法从反编译签名确认，
    /// 所以这里再写一次。两处写的是同一个字符串，不会互相打架。
    /// </summary>
    public static void Refresh()
    {
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;
        if (!HudManager.InstanceExists) return;

        var panel = HudManager.Instance.TaskPanel;
        if (panel == null) return;

        var text = panel.taskText;
        if (text == null) return;

        text.text = BuildTaskText();
    }

    #endregion

    #region 第一层：清空原版任务列表

    /// <summary>PlayerControl.SetTasks 的前缀。清空入参列表，后续协程就没东西可生成了。</summary>
    public static void SetTasksPrefix(Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo.TaskInfo> __0)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;
        if (__0 == null) return;

        __0.Clear();
    }

    #endregion

    #region 第二层：清空 Nebula 的任务状态

    /// <summary>房主把所有玩家的任务额度清零并广播。开局调用一次。</summary>
    public static void WaiveAllTasks()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        foreach (var player in GamePlayer.AllPlayers)
        {
            if (player == null) continue;

            try
            {
                // PlayerTaskState 是 Nebula.Player 下的 public 类，
                // 直接强转即可，不需要 internal 的 Unbox() 扩展方法。
                if (player.Tasks is PlayerTaskState state) state.WaiveAllTasksAsOutsider();
            }
            catch (Exception exception)
            {
                HsgDebug.LogWarning($"[HSG] 道具躲猫猫：清空 {player.PlayerId} 的任务状态失败：{exception.Message}");
            }
        }
    }

    #endregion

    #region 第三层：隐藏任务进度条

    private static ProgressTracker? hiddenTracker;

    /// <summary>开局隐藏原版任务进度条。</summary>
    public static void HideProgressTracker()
    {
        RestoreProgressTracker();

        try
        {
            if (!HudManager.InstanceExists) return;

            var tracker = HudManager.Instance.GetComponentInChildren<ProgressTracker>(true);
            if (tracker == null) return;

            tracker.gameObject.SetActive(false);
            hiddenTracker = tracker;
        }
        catch (Exception exception)
        {
            HsgDebug.LogWarning($"[HSG] 道具躲猫猫：隐藏任务进度条失败：{exception.Message}");
        }
    }

    /// <summary>结束时把进度条还回去，避免污染之后的普通对局。</summary>
    public static void RestoreProgressTracker()
    {
        try
        {
            if (hiddenTracker != null) hiddenTracker.gameObject.SetActive(true);
        }
        catch { /* HUD 可能已经销毁，忽略 */ }

        hiddenTracker = null;
    }

    #endregion
}
