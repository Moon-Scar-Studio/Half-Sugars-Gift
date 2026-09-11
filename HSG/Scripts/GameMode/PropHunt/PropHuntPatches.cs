namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>
/// 道具躲猫猫的全部 Harmony 补丁。
///
/// ⚠️ 这里刻意**不使用** [HarmonyPatch] 特性，全部改为手动 Patch。
/// 原因：牛仔对决的注册代码里有一句 harmony.PatchAll(assembly)，
/// 它会扫描整个 HSG 程序集并应用所有带特性的补丁类。
/// 如果本模式也用特性声明补丁，就会被两套 Harmony 实例各应用一次（前缀/后缀跑两遍）。
/// 手动 Patch 可以保证无论其它模式怎么 PatchAll，本模式的补丁都只生效一次。
/// </summary>
public static class PropHuntHarmony
{
    private static bool applied;

    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static void Apply(Harmony harmony)
    {
        if (applied) return;
        applied = true;

        var self = typeof(PropHuntHarmony);

        Patch(harmony, typeof(PlayerControl), nameof(PlayerControl.Start),
            postfix: self.GetMethod(nameof(PlayerStartPostfix)));

        Patch(harmony, typeof(PlayerControl), nameof(PlayerControl.Die),
            postfix: self.GetMethod(nameof(PlayerDiePostfix)));

        Patch(harmony, typeof(AmongUsClient), nameof(AmongUsClient.ExitGame),
            postfix: self.GetMethod(nameof(ExitGamePostfix)));

        Patch(harmony, typeof(PlayerPhysics), nameof(PlayerPhysics.ResetAnimState),
            postfix: self.GetMethod(nameof(ResetAnimStatePostfix)));

        Patch(harmony, typeof(ActionButton), nameof(ActionButton.SetDisabled),
            prefix: self.GetMethod(nameof(SetDisabledPrefix)));

        Patch(harmony, typeof(KillButton), nameof(KillButton.DoClick),
            prefix: self.GetMethod(nameof(KillButtonDoClickPrefix)), prefixPriority: Priority.First);

        Patch(harmony, typeof(IntroCutscene), nameof(IntroCutscene.CoBegin),
            postfix: self.GetMethod(nameof(IntroPostfix)));

        Patch(harmony, typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update),
            prefix: typeof(PropHuntInput).GetMethod(nameof(PropHuntInput.KeyboardUpdatePrefix)));

        // --- 任务屏蔽 ---
        // PlayerControl 上只有一个 SetTasks 重载（另一个 SetTasks 在 NetworkedPlayerInfo 上，
        // 不是同一个类），所以按名字取方法不会抛 AmbiguousMatchException。
        Patch(harmony, typeof(PlayerControl), nameof(PlayerControl.SetTasks),
            prefix: typeof(PropHuntTaskDisplay).GetMethod(nameof(PropHuntTaskDisplay.SetTasksPrefix)),
            prefixPriority: Priority.First);

        // 任务栏文案。Harmony 的 postfix 高优先级后执行：Priority.First 保证
        // 在 Nebula 自己的 TaskTextPatch（默认优先级）之后写入，不被它覆盖。
        Patch(harmony, typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText),
            postfix: typeof(PropHuntTaskDisplay).GetMethod(nameof(PropHuntTaskDisplay.SetTaskTextPostfix)),
            postfixPriority: Priority.First);

        HsgDebug.Log("[HSG] 道具躲猫猫补丁应用完成。");
    }

    private static void Patch(Harmony harmony, Type target, string methodName,
        MethodInfo? prefix = null, MethodInfo? postfix = null,
        int? prefixPriority = null, int? postfixPriority = null)
    {
        try
        {
            var original = target.GetMethod(methodName, AnyMember);
            if (original == null)
            {
                HsgDebug.LogError($"[HSG] 道具躲猫猫：找不到 {target.Name}.{methodName}，该补丁跳过。");
                return;
            }
            if (prefix == null && postfix == null)
            {
                HsgDebug.LogError($"[HSG] 道具躲猫猫：{target.Name}.{methodName} 的补丁方法解析失败，跳过。");
                return;
            }

            HarmonyMethod? prefixMethod = prefix == null ? null : new HarmonyMethod(prefix);
            if (prefixMethod != null && prefixPriority.HasValue) prefixMethod.priority = prefixPriority.Value;

            HarmonyMethod? postfixMethod = postfix == null ? null : new HarmonyMethod(postfix);
            if (postfixMethod != null && postfixPriority.HasValue) postfixMethod.priority = postfixPriority.Value;

            harmony.Patch(original, prefixMethod, postfixMethod);
        }
        catch (Exception exception)
        {
            HsgDebug.LogError($"[HSG] 道具躲猫猫：补丁 {target.Name}.{methodName} 失败：{exception}");
        }
    }

    #region 道具渲染器的生命周期

    /// <summary>给每个玩家挂上道具渲染器。对应原版 PlayerControlStartPatch。</summary>
    public static void PlayerStartPostfix(PlayerControl __instance) => PropManager.Attach(__instance);

    /// <summary>退出对局时清空道具状态。对应原版 OnExitGame。</summary>
    public static void ExitGamePostfix()
    {
        PropManager.Clear();
        // 中途退出时 GameEndEvent 不一定跑得到，这里也收一次尾，
        // 否则任务进度条会一直被隐藏、污染之后的普通对局。
        PropHuntDangerMeter.Teardown();
        PropHuntTaskDisplay.RestoreProgressTracker();
        PropHuntState.Reset();
    }

    /// <summary>玩家死亡时移除其道具。对应原版 OnPlayerDiePatch。</summary>
    public static void PlayerDiePostfix(PlayerControl __instance)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntActive) return;
        if (__instance == null) return;

        PropManager.Detach(__instance.PlayerId);
        __instance.Visible = true;
    }

    #endregion

    #region 隐身维持

    /// <summary>
    /// 原版通过 PlayerPhysics.ResetAnimState 的后缀持续把伪装中的玩家压成不可见，
    /// 因为原版有多处会把 Visible 改回 true。这里保留同样的策略。
    /// </summary>
    public static void ResetAnimStatePostfix(PlayerPhysics __instance)
    {
        if (!AmongUsClient.Instance.IsGameStarted) return;
        if (!PropHuntGameModeRegistration.IsPropHuntActive) return;

        var player = __instance?.myPlayer;
        if (player == null || player.Data == null || player.Data.Role == null) return;
        if (player.Data.Role.IsImpostor || player.Data.IsDead) return;
        if (!player.Visible) return;

        if (PropManager.IsDisguised(player.PlayerId)) player.Visible = false;
    }

    #endregion

    #region 击杀按钮

    /// <summary>
    /// 抓捕者的击杀按钮永远亮着。
    ///
    /// 原版通过 KillButtonHighlightPatch（SetTarget 后缀无条件 SetEnabled）实现，
    /// 目的是让抓捕者无法靠按钮亮不亮判断眼前的道具是不是玩家。
    /// Nebula 改成在 NebulaGameManager.Update 里根据 KillButtonTracker 调 SetEnabled/SetDisabled，
    /// 所以这里改为拦截 SetDisabled。
    /// </summary>
    public static bool SetDisabledPrefix(ActionButton __instance)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntActive) return true;
        if (!HudManager.InstanceExists) return true;

        var killButton = HudManager.Instance.KillButton;
        if (__instance == null || killButton == null) return true;
        if (__instance.Pointer != killButton.Pointer) return true;

        var local = GamePlayer.LocalPlayer;
        if (local == null || !local.IsImpostor || local.IsDead) return true;

        // 冷却中仍然允许变灰，否则玩家无从判断冷却状态。
        if (killButton.isCoolingDown) return true;

        __instance.SetEnabled();
        return false;
    }

    /// <summary>
    /// 击空判定。对应原版 KillButtonClickPatch。
    ///
    /// Nebula 用一个 Prefix 完全接管了 KillButton.DoClick（无条件返回 false），
    /// 所以这里必须用 Priority.First 抢在它前面，且只处理「没有目标」的情况。
    /// </summary>
    public static void KillButtonDoClickPrefix(KillButton __instance)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntActive) return;
        if (PropHuntState.Phase != PropHuntPhase.Hunting) return;

        var local = GamePlayer.LocalPlayer;
        if (local == null || local.IsDead || !local.IsImpostor) return;

        var control = PlayerControl.LocalPlayer;
        if (control == null || control.inVent) return;

        if (__instance.isCoolingDown) return;

        // 有目标就交给 Nebula 的正常击杀流程。
        if (NebulaGameManager.Instance?.KillButtonTracker?.CurrentTarget != null) return;

        PropHuntRuntime.RpcFailedKill.Invoke(control.PlayerId);
        NebulaAPI.CurrentGame?.KillButtonLikeHandler.SetCooldown(
            PropHuntSettings.MissKillCooldown.GetValue());
    }

    #endregion

    #region 开局演出

    /// <summary>
    /// 开局时把玩家本体压到道具后面，并按阵营处理阴影层。
    /// 对应原版 IntroCuscenePatch。
    /// </summary>
    public static void IntroPostfix()
    {
        if (!PropHuntGameModeRegistration.IsPropHuntActive) return;

        try
        {
            foreach (var info in GameData.Instance.AllPlayers.GetFastEnumerator())
            {
                var body = info?.Object;
                if (body == null) continue;

                var bodyForms = body.transform.FindChild("BodyForms");
                var cosmetics = body.transform.FindChild("Cosmetics");
                if (bodyForms != null) bodyForms.localPosition = new Vector3(0f, 0f, -5f);
                if (cosmetics != null) cosmetics.localPosition = new Vector3(0f, 0f, -5f);
            }

            var shadowCollab = UnityEngine.Object.FindObjectOfType<ShadowCollab>();
            if (shadowCollab == null) return;

            var local = GamePlayer.LocalPlayer;
            if (local != null && local.IsImpostor)
            {
                shadowCollab.ShadowQuad.material.color = new Color(0f, 0f, 0f, 1f);
                shadowCollab.ShadowQuad.gameObject.SetActive(true);
            }
            else
            {
                shadowCollab.ShadowQuad.gameObject.SetActive(false);
            }
        }
        catch (Exception exception)
        {
            HsgDebug.LogWarning($"[HSG] 道具躲猫猫开局演出异常：{exception.Message}");
        }
    }

    #endregion
}

#region 屏蔽会议

/// <summary>
/// 躲猫猫没有会议。屏蔽紧急按钮。
/// 原版靠原版躲猫猫本身就没有会议流程，Nebula 这边要自己挡。
/// </summary>
[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
public class PropHuntMeetingBlocker : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.DIManager.RegisterModule<Virial.Game.Game>(() => new PropHuntMeetingBlocker());
    }

    protected override void OnInjected(Virial.Game.Game container) => this.Register(container);

    void OnCheckEmergency(CheckCanPushEmergencyButtonEvent ev)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;
        ev.DenyButton(Language.Translate("hsg.propHunt.noMeeting"));
    }
}

#endregion
