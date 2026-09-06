namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>对局阶段。</summary>
public enum PropHuntPhase : byte
{
    /// <summary>未开局。</summary>
    None = 0,
    /// <summary>躲藏阶段：抓捕者被冻结，道具自由躲藏。</summary>
    Hiding = 1,
    /// <summary>追捕阶段：倒计时递减。</summary>
    Hunting = 2,
    /// <summary>已结算。</summary>
    Finished = 3,
}

/// <summary>
/// 道具躲猫猫的对局状态。
///
/// 原版 PropHunt 的计时器完全由原版躲猫猫的 LogicGameFlowHnS 提供，
/// 它只调 AdjustEscapeTimer 扣时间。Nebula 把原版躲猫猫整个禁用了，
/// 所以计时器、阶段切换、胜负判定都得在这里自己实现。
///
/// 同步策略：房主权威。房主每帧推进倒计时，每秒（以及任何跳变时）广播一次校正；
/// 客户端本地也递减，两次校正之间不会看到卡顿。
/// </summary>
[NebulaRPCHolder]
public static class PropHuntState
{
    /// <summary>当前阶段。</summary>
    public static PropHuntPhase Phase { get; private set; } = PropHuntPhase.None;

    /// <summary>当前阶段的剩余秒数。</summary>
    public static float Remaining { get; private set; }

    /// <summary>开局时由分配器记录下来的抓捕者名单，用于开局演出。</summary>
    private static readonly HashSet<byte> InitialSeekers = new();

    /// <summary>是否处于最终倒计时。</summary>
    public static bool IsFinalCountdown =>
        Phase == PropHuntPhase.Hunting &&
        Remaining <= PropHuntSettings.FinalCountdownTime.GetValue();

    internal static void SetSeekersOnAssign(IEnumerable<byte> seekers)
    {
        InitialSeekers.Clear();
        foreach (var id in seekers) InitialSeekers.Add(id);
    }

    internal static void Reset()
    {
        Phase = PropHuntPhase.None;
        Remaining = 0f;
    }

    /// <summary>本地推进倒计时（房主与客户端都跑，房主负责校正）。</summary>
    internal static void Advance(float deltaTime)
    {
        if (Phase is PropHuntPhase.None or PropHuntPhase.Finished) return;
        Remaining = Mathf.Max(0f, Remaining - deltaTime);
    }

    /// <summary>房主设置阶段与时间，并广播。</summary>
    internal static void HostSet(PropHuntPhase phase, float remaining)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        RpcSync.Invoke(((byte)phase, remaining));
    }

    /// <summary>房主调整剩余时间（击空惩罚）。</summary>
    internal static void HostAdjust(float delta)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        HostSet(Phase, Mathf.Max(0f, Remaining + delta));
    }

    public static readonly RemoteProcess<(byte phase, float remaining)> RpcSync =
        new("HSG_PropHuntSync", (message, _) =>
        {
            Phase = (PropHuntPhase)message.phase;
            Remaining = message.remaining;
        });
}

/// <summary>
/// 道具躲猫猫的对局驱动。
///
/// 注册方式沿用 HSG 里 FunModes / MoreVoteSettings 的做法：
/// DIManager.RegisterModule&lt;Game&gt; + AbstractModule&lt;Game&gt;，
/// 这样每局重建 GameOperatorManager 时会自动重新注入，
/// 不需要像牛仔对决那样反射补丁 NebulaGameManager.OnGameStart。
/// </summary>
[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
[NebulaRPCHolder]
public class PropHuntRuntime : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.DIManager.RegisterModule<Virial.Game.Game>(() => new PropHuntRuntime());
    }

    protected new void OnInjected(Virial.Game.Game container) => this.Register(container);

    private float syncAccumulator;
    private float pingAccumulator;
    private readonly List<Arrow> seekerArrows = new();
    private bool arrowsBuilt;

    #region 开局

    void OnGameStart(GameStartEvent ev)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;

        syncAccumulator = 0f;
        pingAccumulator = 0f;
        arrowsBuilt = false;
        ClearArrows();

        SetUpHud();
        ApplySeekerVision();

        // 任务屏蔽：清空 Nebula 侧的任务额度（原版任务实体已由 SetTasks 前缀挡掉），
        // 并把左上角的原版进度条收起来。详见 PropHuntTaskDisplay 的说明。
        PropHuntTaskDisplay.WaiveAllTasks();
        PropHuntTaskDisplay.HideProgressTracker();

        // 躲藏者的危险程度提示条。
        var currentGame = NebulaAPI.CurrentGame;
        if (currentGame != null) PropHuntDangerMeter.Setup(currentGame);

        if (AmongUsClient.Instance.AmHost)
        {
            float hidingTime = PropHuntSettings.HidingTime.GetValue();
            if (hidingTime > 0f)
            {
                PropHuntState.HostSet(PropHuntPhase.Hiding, hidingTime);
            }
            else
            {
                PropHuntState.HostSet(PropHuntPhase.Hunting, PropHuntSettings.EscapeTime.GetValue());
            }
        }

        FreezeSeekersForHiding();

        HsgDebug.Log("[HSG] 道具躲猫猫开局。");
    }

    /// <summary>躲藏阶段冻结抓捕者：速度归零 + 全屏遮挡。</summary>
    private void FreezeSeekersForHiding()
    {
        float hidingTime = PropHuntSettings.HidingTime.GetValue();
        if (hidingTime <= 0f) return;

        var local = GamePlayer.LocalPlayer;
        if (local == null || !local.IsImpostor) return;

        // 速度归零由房主下发，保证所有端一致。
        if (AmongUsClient.Instance.AmHost)
        {
            foreach (var player in GamePlayer.AllPlayers.Where(p => p.IsImpostor))
            {
                player.GainSpeedAttribute(0f, hidingTime, false, 100, "HsgPropHuntFreeze");
            }
        }

        PatchManager.ShowScreenOverlay(new Color(0f, 0f, 0f, 1f), hidingTime);
    }

    /// <summary>应用抓捕者视野倍率（对应原版预设的 ImpostorLightMod）。</summary>
    private void ApplySeekerVision()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        float ratio = PropHuntSettings.SeekerLightMod.GetValue();
        if (Mathf.Approximately(ratio, 1f)) return;

        foreach (var player in GamePlayer.AllPlayers.Where(p => p.IsImpostor))
        {
            player.GainAttribute(PlayerAttributes.Eyesight, float.MaxValue, ratio, true, 50, "HsgPropHuntVision");
        }
    }

    private void SetUpHud()
    {
        var game = NebulaAPI.CurrentGame;
        if (game == null) return;

        Helpers.TextHudContent("HsgPropHuntTimer", game, tmPro =>
        {
            tmPro.text = BuildTimerText();
        });
    }

    private static string BuildTimerText()
    {
        int seconds = Mathf.CeilToInt(PropHuntState.Remaining);
        string clock = $"{seconds / 60:00}:{seconds % 60:00}";

        return PropHuntState.Phase switch
        {
            PropHuntPhase.Hiding =>
                $"<color=#88CCFF>{Language.Translate("hsg.propHunt.hud.hiding")}</color> {clock}",
            PropHuntPhase.Hunting when PropHuntState.IsFinalCountdown =>
                $"<color=#FF5555>{Language.Translate("hsg.propHunt.hud.final")}</color> {clock}",
            PropHuntPhase.Hunting =>
                $"{Language.Translate("hsg.propHunt.hud.remaining")} {clock}",
            _ => string.Empty,
        };
    }

    #endregion

    #region 每帧

    void OnUpdate(GameUpdateEvent ev)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;

        // 这两项不受阶段限制：结算后危险度要归零收起，任务栏文案在死亡后也要能切换。
        PropHuntDangerMeter.Update();
        PropHuntTaskDisplay.Refresh();

        if (PropHuntState.Phase is PropHuntPhase.None or PropHuntPhase.Finished) return;

        PropHuntState.Advance(ev.DeltaTime);

        UpdateFinalCountdownAssist(ev.DeltaTime);

        if (!AmongUsClient.Instance.AmHost) return;

        HostTick(ev.DeltaTime);
    }

    private void HostTick(float deltaTime)
    {
        // 阶段切换
        if (PropHuntState.Remaining <= 0f)
        {
            switch (PropHuntState.Phase)
            {
                case PropHuntPhase.Hiding:
                    PropHuntState.HostSet(PropHuntPhase.Hunting, PropHuntSettings.EscapeTime.GetValue());
                    ResetSeekerCooldown();
                    return;

                case PropHuntPhase.Hunting:
                    // 时间耗尽：道具方胜利。
                    TriggerEnd(PropHuntGameEnds.PropWin, p => !p.IsImpostor);
                    return;
            }
        }

        // 抓完所有道具：抓捕者胜利。
        if (PropHuntState.Phase == PropHuntPhase.Hunting && !GamePlayer.AllPlayers.Any(IsAliveProp))
        {
            TriggerEnd(PropHuntGameEnds.SeekerWin, p => p.IsImpostor);
            return;
        }

        // 定期校正
        syncAccumulator += deltaTime;
        if (syncAccumulator >= 1f)
        {
            syncAccumulator = 0f;
            PropHuntState.HostSet(PropHuntState.Phase, PropHuntState.Remaining);
        }
    }

    private static bool IsAliveProp(GamePlayer player)
        => player != null && !player.IsDead && !player.IsDisconnected && !player.IsImpostor;

    private static void ResetSeekerCooldown()
    {
        var local = GamePlayer.LocalPlayer;
        if (local == null || !local.IsImpostor) return;
        NebulaAPI.CurrentGame?.KillButtonLikeHandler.SetCooldown(PropHuntSettings.SeekerKillCooldown.GetValue());
    }

    #endregion

    #region 最终倒计时辅助（Seeker Pings / Final Map）

    private void UpdateFinalCountdownAssist(float deltaTime)
    {
        var local = GamePlayer.LocalPlayer;
        bool active = PropHuntState.IsFinalCountdown && local != null && local.IsImpostor && !local.IsDead;

        // --- Seeker Pings：周期性播报所有存活道具的位置 ---
        if (active && PropHuntSettings.SeekerPings)
        {
            pingAccumulator += deltaTime;
            if (pingAccumulator >= 5f)
            {
                pingAccumulator = 0f;
                var positions = GamePlayer.AllPlayers
                    .Where(IsAliveProp)
                    .Select(p => (Vector2)p.TruePosition)
                    .ToArray();
                if (positions.Length > 0) AmongUsUtil.Ping(positions, true);
            }
        }
        else
        {
            pingAccumulator = 0f;
        }

        // --- Seeker Final Map：常驻指向所有存活道具的箭头 ---
        // 对应原版 SeekerAdminMapEnabledPatch：选项关闭时抓捕者拿不到任何位置信息。
        bool wantArrows = active && PropHuntSettings.SeekerFinalMap;
        if (wantArrows && !arrowsBuilt)
        {
            BuildArrows();
        }
        else if (!wantArrows && arrowsBuilt)
        {
            ClearArrows();
        }

        if (!arrowsBuilt) return;

        int index = 0;
        foreach (var player in GamePlayer.AllPlayers.Where(IsAliveProp))
        {
            if (index >= seekerArrows.Count) break;
            seekerArrows[index].TargetPos = player.TruePosition;
            seekerArrows[index].IsActive = true;
            index++;
        }
        for (; index < seekerArrows.Count; index++) seekerArrows[index].IsActive = false;
    }

    private void BuildArrows()
    {
        ClearArrows();

        var game = NebulaAPI.CurrentGame;
        if (game == null) return;

        foreach (var _ in GamePlayer.AllPlayers.Where(IsAliveProp))
        {
            var arrow = new Arrow();
            arrow.Register(game);
            seekerArrows.Add(arrow);
        }
        arrowsBuilt = seekerArrows.Count > 0;
    }

    private void ClearArrows()
    {
        foreach (var arrow in seekerArrows) arrow.Release();
        seekerArrows.Clear();
        arrowsBuilt = false;
    }

    #endregion

    #region 击杀 / 击空

    /// <summary>抓捕者击空。对应原版 RPCFailedKill。</summary>
    public static readonly RemoteProcess<byte> RpcFailedKill =
        new("HSG_PropHuntFailedKill", (playerId, _) =>
        {
            if (!PropHuntGameModeRegistration.IsPropHuntMode) return;

            // 扣时间由房主权威执行，避免每个客户端各扣一次。
            if (AmongUsClient.Instance.AmHost)
            {
                PropHuntState.HostAdjust(-PropHuntSettings.MissTimePenalty.GetValue());
            }

            // 演出所有人都放，与原版一致。
            PropHuntUtility.PlayMissEffect();

            if (!PropHuntSettings.DestroyPropOnMiss) return;

            var control = Helpers.GetPlayer(playerId);
            if (control == null) return;

            float radius = GameOptionsManager.Instance.CurrentGameOptions.GetInt(
                               AmongUs.GameOptions.Int32OptionNames.KillDistance)
                           + PropHuntSettings.MissDestroyRadiusBonus.GetValue();

            var closest = PropHuntUtility.FindClosestConsole(control.gameObject, radius);
            if (closest != null) UnityEngine.Object.Destroy(closest);
        });

    /// <summary>道具被抓。处理感染模式与胜负检查。</summary>
    [OnlyHost]
    void OnPlayerDie(PlayerDieEvent ev)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;
        if (PropHuntState.Phase != PropHuntPhase.Hunting) return;
        if (ev.Player == null || ev.Player.IsImpostor) return;

        if (PropHuntSettings.Infection)
        {
            // 感染模式：被抓的道具原地复活并转为抓捕者。
            // 原版因为原版躲猫猫强依赖单内鬼而禁用了这条，Nebula 侧没有该限制。
            var position = ev.Player.TruePosition;
            ev.Player.Revive(null, position, true, false);
            ev.Player.SetRole(Nebula.Roles.Impostor.Impostor.MyRole);
        }
    }

    #endregion

    #region 结算

    /// <summary>
    /// 拦下所有不属于本模式的胜负判定。
    ///
    /// 因为模式容器用的是 IGameModeStandard（理由见 PropHuntGameMode.cs 顶部说明），
    /// ImpostorGameRule / CrewmateGameRule / LoversCriteria / JackalCriteria 会被一并注入，
    /// 它们会在「内鬼数×2≥存活数」「任务全清」「破坏倒计时归零」等时机调用 TriggerGameEnd。
    /// 这些都要在这里丢掉，否则躲猫猫会被判成一局普通的内鬼局。
    ///
    /// 本模式自己的两个结算走 NebulaGameEnd.RpcSendGameEnd，
    /// 根本不经过 CriteriaManager，所以不会被这里误伤。
    /// </summary>
    void OnEndCriteriaPreMet(EndCriteriaPreMetEvent ev)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;
        ev.Reject();
    }

    private void TriggerEnd(GameEnd end, Func<GamePlayer, bool> winnerPredicate)
    {
        if (PropHuntState.Phase == PropHuntPhase.Finished) return;
        PropHuntState.HostSet(PropHuntPhase.Finished, 0f);

        var winners = BitMasks.AsPlayer();
        foreach (var player in GamePlayer.AllPlayers.Where(winnerPredicate)) winners.Add(player);

        NebulaGameEnd.RpcSendGameEnd(
            end,
            (int)winners.AsRawPattern,
            0,
            GameEndReason.Special,
            end,
            GameEndReason.Special);
    }

    void OnGameEnd(GameEndEvent ev)
    {
        ClearArrows();
        PropManager.Clear();
        PropHuntDangerMeter.Teardown();
        PropHuntTaskDisplay.RestoreProgressTracker();
        PropHuntState.Reset();
    }

    #endregion
}
