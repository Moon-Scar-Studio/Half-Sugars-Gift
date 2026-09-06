namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>
/// 躲藏者的「危险程度」提示条。
///
/// 原版躲猫猫这套东西由 LogicHnSDangerLevel 驱动 HudManager.DangerMeter，
/// 判据是最近抓捕者的距离，分 scaryMusicDistance / veryScaryMusicDistance 两档。
/// Nebula 把原版躲猫猫整个禁用了，所以 LogicHnSDangerLevel 从来不会被创建，
/// 但 HudManager 上那个 DangerMeter 字段和它的 public SetDangerValue(float, float)
/// 仍然存在 —— 我们直接借用这个控件。
///
/// 两条路径：
///   · 首选：原版 DangerMeter。观感与原版躲猫猫完全一致（分段格子 + 配色 + 抖动）。
///   · 兜底：文字提示条。因为 Nebula 从没走过这条路径，无法确认 DangerMeter 字段
///           在实际 prefab 里一定非空，所以字段为空或首次调用抛异常时永久降级，
///           不会每帧刷异常。降级用的方块字符是 U+25A0「■」——
///           这是 Nebula 全库唯一有先例、确认能在游戏字体里正常渲染的方块字符
///           （PlayerControlPatch 用它做玩家颜色标记），不用 U+2588 是怕缺字变豆腐块。
///
/// 可见范围：仅存活的躲藏者。抓捕者和死人都不显示（否则等于白送情报）。
/// 躲藏阶段也显示 —— 抓捕者虽然被冻结，但知道他冻在哪对选藏点有意义。
/// </summary>
public static class PropHuntDangerMeter
{
    /// <summary>兜底文字条的格数。</summary>
    private const int TextBarSegments = 10;

    private const string BarHotColor = "#FF4444";
    private const string BarWarnColor = "#FFC83D";
    private const string BarEmptyColor = "#3B3F45";
    private const string LabelColor = "#C7CCD1";

    private static DangerMeter? vanillaMeter;
    private static bool vanillaUnavailable;
    private static bool fallbackHudCreated;

    private static bool visible;
    private static float levelWarn;
    private static float levelHot;

    #region 生命周期

    /// <summary>开局初始化。由 PropHuntRuntime.OnGameStart 调用。</summary>
    public static void Setup(Virial.Game.Game game)
    {
        vanillaMeter = null;
        vanillaUnavailable = false;
        fallbackHudCreated = false;
        visible = false;
        levelWarn = 0f;
        levelHot = 0f;

        try
        {
            if (HudManager.InstanceExists) vanillaMeter = HudManager.Instance.DangerMeter;
        }
        catch
        {
            vanillaMeter = null;
        }

        if (vanillaMeter == null)
        {
            HsgDebug.LogWarning("[HSG] 道具躲猫猫：HudManager.DangerMeter 不可用，危险度改用文字提示条。");
            FallBack(game);
            return;
        }

        // 原版控件默认是关着的，等真正需要时才打开。
        try { vanillaMeter.gameObject.SetActive(false); } catch { }
        HsgDebug.Log("[HSG] 道具躲猫猫：危险度使用原版 DangerMeter。");
    }

    /// <summary>结束时收起。由 PropHuntRuntime.OnGameEnd 调用。</summary>
    public static void Teardown()
    {
        try
        {
            if (vanillaMeter != null) vanillaMeter.gameObject.SetActive(false);
        }
        catch { /* HUD 可能已销毁 */ }

        vanillaMeter = null;
        vanillaUnavailable = false;
        fallbackHudCreated = false;
        visible = false;
        levelWarn = 0f;
        levelHot = 0f;
    }

    /// <summary>降级到文字条。可能发生在开局，也可能发生在运行中首次调用失败时。</summary>
    private static void FallBack(Virial.Game.Game? game)
    {
        vanillaUnavailable = true;

        try
        {
            if (vanillaMeter != null) vanillaMeter.gameObject.SetActive(false);
        }
        catch { }
        vanillaMeter = null;

        if (fallbackHudCreated) return;

        game ??= NebulaAPI.CurrentGame;
        if (game == null) return;

        fallbackHudCreated = true;

        // 只在真正需要时才创建，避免原版控件可用时 HUD 网格里多出一个空槽位。
        Helpers.TextHudContent("HsgPropHuntDanger", game, tmPro => tmPro.text = BuildFallbackText());
    }

    #endregion

    #region 每帧

    /// <summary>由 PropHuntRuntime.OnUpdate 调用。</summary>
    public static void Update()
    {
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;

        visible = TryComputeDanger(out levelWarn, out levelHot);

        if (vanillaUnavailable || vanillaMeter == null) return;

        try
        {
            if (vanillaMeter.gameObject.activeSelf != visible) vanillaMeter.gameObject.SetActive(visible);
            if (visible) vanillaMeter.SetDangerValue(levelWarn, levelHot);
        }
        catch (Exception exception)
        {
            HsgDebug.LogWarning($"[HSG] 道具躲猫猫：原版 DangerMeter 调用失败，改用文字提示条：{exception.Message}");
            FallBack(null);
        }
    }

    /// <summary>
    /// 计算两档危险度。
    ///
    /// levelWarn 对应原版的 scaryMusicDistance 档，levelHot 对应 veryScaryMusicDistance 档，
    /// 都是 0~1：距离等于阈值时为 0，贴脸时为 1。因为危险距离恒小于等于警戒距离，
    /// levelHot 天然不会超过 levelWarn，符合 DangerMeter 分段染色的预期。
    /// </summary>
    private static bool TryComputeDanger(out float warn, out float hot)
    {
        warn = 0f;
        hot = 0f;

        if (!PropHuntSettings.ShowDangerMeter) return false;
        if (PropHuntState.Phase is not (PropHuntPhase.Hiding or PropHuntPhase.Hunting)) return false;

        var local = GamePlayer.LocalPlayer;
        if (local == null || local.IsDead || local.IsImpostor) return false;

        var origin = (Vector2)local.TruePosition;

        float nearest = float.MaxValue;
        foreach (var player in GamePlayer.AllPlayers)
        {
            if (player == null) continue;
            if (!player.IsImpostor || player.IsDead || player.IsDisconnected) continue;

            float distance = Vector2.Distance(origin, (Vector2)player.TruePosition);
            if (distance < nearest) nearest = distance;
        }

        // 场上没有存活抓捕者（理论上不会发生，感染模式下也总有人是抓捕者）。
        if (nearest >= float.MaxValue) return false;

        float warnDistance = Mathf.Max(0.5f, PropHuntSettings.DangerWarnDistance.GetValue());
        float hotDistance = Mathf.Clamp(PropHuntSettings.DangerAlertDistance.GetValue(), 0.5f, warnDistance);

        warn = Mathf.Clamp01(1f - nearest / warnDistance);
        hot = Mathf.Clamp01(1f - nearest / hotDistance);
        return true;
    }

    #endregion

    #region 兜底文字条

    private static string BuildFallbackText()
    {
        if (!visible) return string.Empty;

        int filled = Mathf.Clamp(Mathf.CeilToInt(levelWarn * TextBarSegments), 0, TextBarSegments);
        int hot = Mathf.Clamp(Mathf.CeilToInt(levelHot * TextBarSegments), 0, filled);

        var builder = new StringBuilder();
        builder.Append("<color=").Append(LabelColor).Append('>')
               .Append(Language.Translate("hsg.propHunt.hud.danger"))
               .Append("</color> ");

        if (hot > 0) builder.Append("<color=").Append(BarHotColor).Append('>').Append('■', hot).Append("</color>");
        if (filled > hot) builder.Append("<color=").Append(BarWarnColor).Append('>').Append('■', filled - hot).Append("</color>");
        if (TextBarSegments > filled) builder.Append("<color=").Append(BarEmptyColor).Append('>').Append('■', TextBarSegments - filled).Append("</color>");

        return builder.ToString();
    }

    #endregion
}
