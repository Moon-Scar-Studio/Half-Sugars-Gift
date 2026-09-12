namespace HalfSugarGift.GameMode.PropHunt;

#region 队友视角

/// <summary>
/// 躲藏者的「队友视角」。
///
/// 藏好之后本来就只能干等，这个功能让人有事可做：把相机切到另一个还活着的躲藏者身上，
/// 看看队友藏在哪、抓捕者摸到谁跟前了。
///
/// 【为什么切相机是安全的】
/// Nebula 自己就有一套 <c>AmongUsUtil.SetCamTarget</c>（Ubiquitous / Marionette 都在用），
/// 而且 <c>PlayerCanMovePatch</c> 已经写死了「相机目标不是本人时 CanMove 返回 false」，
/// 所以「观察时本体不能动」是白送的，不需要我们自己冻结速度，
/// 也就不会像 GainSpeedAttribute 那样把状态广播给别人。整个功能纯本地，零 RPC。
///
/// 【阴影】
/// 躲猫猫里 PropHuntHarmony.IntroPostfix 已经把躲藏者的 ShadowQuad 关掉了，
/// 所以视角飞出去也不会是一片黑。但道具机制关闭时那段逻辑会提前 return，
/// 因此这里进入观察时再关一次、退出时按进入前的状态还原，两种情况都覆盖到。
///
/// 【键位选择】
/// 主键用 SpectatorRight（默认「.」）、子操作键用 Spectator（默认 C）。
/// 这两个键在对局中本来就没有别的用途，语义也正好对得上「切换观战目标 / 退出观战」。
///
/// 最初用的是 AidAction，那是个错误：它的默认键是 LeftShift，
/// 而本模式的「Shift + 方向键微调道具」也吃 Shift —— 玩家一按下 Shift 准备微调，
/// 视角就先飞到队友身上去了。两者必然冲突，必须换。
///
/// 【循环与退出】
/// 主键点一次跳下一个队友，转完一圈也会自动回到自己。
/// 但「必须轮完一圈才能回来」在人多的时候很难受，所以退出还有两条路：
///   · 按钮的子操作键（观战键），会在按钮上自动显示键位提示；
///   · 键盘 C —— 这个模式里 C 本来就是「变回」，观察期间复用成「回到自己」很自然。
///     这条走的是 PropHuntInput 自己的 Harmony 补丁，不读 Nebula 的键位配置，
///     所以即使玩家把观战键改到了别处，C 依然管用。
///     （默认配置下观战键恰好就是 C，两条路会重合，重复调用是安全的。）
/// </summary>
public static class PropHuntSpectator
{
    /// <summary>当前观察的目标。byte.MaxValue 表示没在观察。</summary>
    private static byte watchingId = byte.MaxValue;

    /// <summary>进入观察前 ShadowQuad 是否是开着的，退出时用来还原。</summary>
    private static bool? shadowWasActive;

    public static bool IsWatching => watchingId != byte.MaxValue;

    /// <summary>最近一次真正退出观察所在的帧号。</summary>
    private static int lastStopFrame = -1;

    /// <summary>
    /// 本帧刚刚退出过观察。
    ///
    /// 退出观察有两个入口，而且默认配置下它们是同一个键（观战键默认就是 C）：
    ///   · 按钮的子操作键 —— 在 HudManager 的更新里轮询；
    ///   · PropHuntInput 的原始按键 —— 在 KeyboardJoystick.Update 里轮询。
    /// 这两处同属一帧，但谁先谁后没有保证。如果按钮那边先跑，
    /// 等 PropHuntInput 跑到时 IsWatching 已经是 false，于是继续往下走，
    /// 同一次 GetKeyDown 又被「C = 变回船员」消费了一次 —— 表现就是
    /// 退出观察的同时自己也变回了原形。
    ///
    /// 用帧号把这一帧标记掉，变回的判定绕开它，两种执行顺序就都安全了。
    /// </summary>
    public static bool JustStopped => lastStopFrame == Time.frameCount;

    public static GamePlayer? WatchingPlayer =>
        IsWatching ? GamePlayer.AllPlayers.FirstOrDefault(p => p != null && p.PlayerId == watchingId) : null;

    #region 判定

    /// <summary>本地玩家现在能不能用这个功能。</summary>
    public static bool CanUse()
    {
        if (!PropHuntGameModeRegistration.IsPropHuntActive) return false;
        if (!PropHuntSettings.EnableHiderSkills || !PropHuntSettings.EnableSpectate) return false;
        if (PropHuntState.Phase is not (PropHuntPhase.Hiding or PropHuntPhase.Hunting)) return false;

        var local = GamePlayer.LocalPlayer;
        if (local == null || local.IsDead || local.IsImpostor) return false;

        // 「固定住以后才能看」：要求先变成道具。关掉这个开关就随时能看。
        if (PropHuntSettings.SpectateNeedsDisguise && !PropManager.IsDisguised(local.PlayerId)) return false;

        return HasAnyTarget();
    }

    /// <summary>有没有可观察的队友。按钮的 Availability 每帧都会问一次，所以这条路径不分配列表。</summary>
    private static bool HasAnyTarget()
    {
        var local = GamePlayer.LocalPlayer;
        if (local == null) return false;

        foreach (var player in GamePlayer.AllPlayers)
        {
            if (player == null) continue;
            if (player.PlayerId == local.PlayerId) continue;
            if (player.IsImpostor || player.IsDead || player.IsDisconnected) continue;
            return true;
        }
        return false;
    }

    /// <summary>可观察的队友：活着的、没断线的、不是自己的躲藏者。按 PlayerId 排序保证循环顺序稳定。</summary>
    private static List<GamePlayer> CollectTargets()
    {
        var local = GamePlayer.LocalPlayer;
        var result = new List<GamePlayer>();
        if (local == null) return result;

        foreach (var player in GamePlayer.AllPlayers)
        {
            if (player == null) continue;
            if (player.PlayerId == local.PlayerId) continue;
            if (player.IsImpostor || player.IsDead || player.IsDisconnected) continue;
            result.Add(player);
        }

        result.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
        return result;
    }

    #endregion

    #region 操作

    /// <summary>切到下一个队友；已经是最后一个就回到自己。</summary>
    public static void CycleNext()
    {
        if (!CanUse())
        {
            Stop();
            return;
        }

        var targets = CollectTargets();
        if (targets.Count == 0)
        {
            Stop();
            return;
        }

        int index = targets.FindIndex(p => p.PlayerId == watchingId);

        // -1（没在观察）→ 0；最后一个 → 越界 → 回到自己。
        index++;
        if (index >= targets.Count)
        {
            Stop();
            return;
        }

        Watch(targets[index]);
    }

    private static void Watch(GamePlayer target)
    {
        var control = Helpers.GetPlayer(target.PlayerId);
        if (control == null)
        {
            Stop();
            return;
        }

        HideShadow();

        watchingId = target.PlayerId;
        AmongUsUtil.SetCamTarget(control);

        PropHuntOverlay.ShowFlash(
            Language.Translate("hsg.propHunt.hud.watching") + " " + target.Name
            + "\n<size=60%>" + Language.Translate("hsg.propHunt.hud.watchExitHint") + "</size>",
            new Color(0.53f, 0.87f, 1f),
            2.5f);
    }

    /// <summary>
    /// 玩家主动退出观察。和自动退出分开，是为了只在主动退出时给一条确认提示 ——
    /// 自动退出（目标被抓、自己被抓、对局结束）时再弹提示只会添乱。
    /// </summary>
    public static void StopByPlayer()
    {
        if (!IsWatching) return;

        Stop();

        PropHuntOverlay.ShowFlash(
            Language.Translate("hsg.propHunt.hud.watchExited"),
            new Color(0.53f, 0.87f, 1f),
            1.2f);
    }

    /// <summary>回到自己的视角。重复调用是安全的。</summary>
    public static void Stop()
    {
        if (!IsWatching)
        {
            RestoreShadow();
            return;
        }

        watchingId = byte.MaxValue;
        lastStopFrame = Time.frameCount;

        try
        {
            AmongUsUtil.SetCamTarget();
        }
        catch (Exception exception)
        {
            HsgDebug.LogWarning($"[HSG] 道具躲猫猫：相机还原失败：{exception.Message}");
        }

        RestoreShadow();
    }

    #endregion

    #region 每帧校验

    /// <summary>
    /// 观察中途条件失效就自动退出。
    /// 覆盖：目标被抓 / 目标掉线 / 自己被抓 / 自己变回船员 / 对局结束。
    /// </summary>
    public static void Update()
    {
        if (!IsWatching) return;

        var target = WatchingPlayer;
        if (target == null || target.IsDead || target.IsDisconnected || !CanUse())
        {
            Stop();
        }
    }

    #endregion

    #region 阴影

    private static void HideShadow()
    {
        if (shadowWasActive.HasValue) return;

        try
        {
            var shadowCollab = UnityEngine.Object.FindObjectOfType<ShadowCollab>();
            if (shadowCollab == null || shadowCollab.ShadowQuad == null) return;

            shadowWasActive = shadowCollab.ShadowQuad.gameObject.activeSelf;
            shadowCollab.ShadowQuad.gameObject.SetActive(false);
        }
        catch (Exception exception)
        {
            HsgDebug.LogWarning($"[HSG] 道具躲猫猫：关闭阴影失败：{exception.Message}");
        }
    }

    private static void RestoreShadow()
    {
        if (!shadowWasActive.HasValue) return;

        try
        {
            var shadowCollab = UnityEngine.Object.FindObjectOfType<ShadowCollab>();
            if (shadowCollab != null && shadowCollab.ShadowQuad != null)
                shadowCollab.ShadowQuad.gameObject.SetActive(shadowWasActive.Value);
        }
        catch { /* HUD 可能已销毁 */ }

        shadowWasActive = null;
    }

    #endregion

    /// <summary>开局 / 结束时的硬复位。</summary>
    public static void Reset()
    {
        watchingId = byte.MaxValue;
        try { AmongUsUtil.SetCamTarget(); } catch { }
        RestoreShadow();
    }
}

#endregion

#region 替身

/// <summary>
/// 「替身」留下的假道具。
///
/// 它不是一个能被击杀的目标，只是一个贴图 —— 这正是它平衡的地方：
/// 抓捕者对着它挥一刀，走的是本模式原有的「击空」流程（扣时间 + 强制冷却），
/// 所以替身不给躲藏方增加血量，只是把抓捕者的失误概率变高。
/// 反过来，抓捕者如果看出来了，砍碎它也不亏（击空销毁会把替身一起清掉，见 PropHuntPatches）。
///
/// 渲染参数刻意抄 PropManager：同样的 layer 11、同样的 PropZOffset，
/// 这样假道具和真躲藏者在画面上完全一致，看不出破绽。
/// </summary>
public static class PropHuntDecoys
{
    private sealed class Decoy
    {
        public GameObject Object = null!;
        public byte OwnerId;
        public float ExpireAt;
    }

    private static readonly List<Decoy> Active = new();

    /// <summary>生成一个假道具。所有客户端都会调用到这里。</summary>
    public static void Spawn(byte ownerId, int consoleIndex, float x, float y, float duration)
    {
        try
        {
            var ship = ShipStatus.Instance;
            if (ship == null || ship.AllConsoles == null) return;
            if (consoleIndex < 0 || consoleIndex >= ship.AllConsoles.Length) return;

            var console = ship.AllConsoles[consoleIndex];
            if (console == null) return;

            var consoleRenderer = console.gameObject.GetComponent<SpriteRenderer>();
            if (consoleRenderer == null || consoleRenderer.sprite == null) return;

            var obj = new GameObject("HsgPropDecoy") { layer = 11 };
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = consoleRenderer.sprite;
            renderer.sharedMaterial = consoleRenderer.sharedMaterial;

            obj.transform.localScale = console.gameObject.transform.lossyScale;
            // z 与真道具一致：世界排序值（y/1000）再叠上道具的前置偏移。
            obj.transform.position = new Vector3(x, y, (y / 1000f) + PropManager.PropZOffset);

            Active.Add(new Decoy
            {
                Object = obj,
                OwnerId = ownerId,
                ExpireAt = Time.time + duration,
            });
        }
        catch (Exception exception)
        {
            HsgDebug.LogWarning($"[HSG] 道具躲猫猫：替身生成失败：{exception.Message}");
        }
    }

    /// <summary>到期清理。由 PropHuntRuntime.OnUpdate 调用。</summary>
    public static void Update()
    {
        if (Active.Count == 0) return;

        float now = Time.time;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var decoy = Active[i];
            if (decoy.Object == null)
            {
                Active.RemoveAt(i);
                continue;
            }
            if (decoy.ExpireAt > now) continue;

            UnityEngine.Object.Destroy(decoy.Object);
            Active.RemoveAt(i);
        }
    }

    /// <summary>销毁某个位置附近最近的一个替身。抓捕者击空时调用，返回是否砍中了替身。</summary>
    public static bool DestroyNearest(Vector2 position, float radius)
    {
        int best = -1;
        float bestDistance = radius;

        for (int i = 0; i < Active.Count; i++)
        {
            if (Active[i].Object == null) continue;

            float distance = Vector2.Distance(position, Active[i].Object.transform.position);
            if (distance > bestDistance) continue;

            best = i;
            bestDistance = distance;
        }

        if (best < 0) return false;

        UnityEngine.Object.Destroy(Active[best].Object);
        Active.RemoveAt(best);
        return true;
    }

    /// <summary>某人被抓时收掉他留下的替身，免得死人的诱饵还在场上骗人。</summary>
    public static void ClearOwnedBy(byte ownerId)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (Active[i].OwnerId != ownerId) continue;
            if (Active[i].Object != null) UnityEngine.Object.Destroy(Active[i].Object);
            Active.RemoveAt(i);
        }
    }

    public static void Clear()
    {
        foreach (var decoy in Active)
        {
            if (decoy.Object != null) UnityEngine.Object.Destroy(decoy.Object);
        }
        Active.Clear();
    }
}

#endregion

#region 技能总装

/// <summary>
/// 道具躲猫猫的趣味技能。
///
/// 设计原则是「给躲藏方找点事做，但不改变胜负结构」：
///   · 队友视角  —— 纯观看，零信息泄露给抓捕方，零收益给躲藏方。
///   · 嘲讽      —— 主动暴露自己的位置，换取追捕倒计时缩短。代价与收益都在明面上。
///   · 替身      —— 不是额外的命，只是提高抓捕者击空的概率；击空本身已有惩罚机制。
///   · 声呐      —— 抓捕方的对称补偿，默认关闭；房主觉得躲藏方技能太强时打开。
///
/// 按钮走 NebulaAPI.Modules 的公开工厂，寿命绑在当局（Virial.Game.Game 本身就是 ILifespan），
/// 因此不需要给这个模式引入任何职业或能力类型 —— 躲藏者依然是干净的原版船员。
/// </summary>
[NebulaRPCHolder]
public static class PropHuntSkills
{
    #region 状态

    private static ModAbilityButton? tauntButton;
    private static ModAbilityButton? decoyButton;
    private static ModAbilityButton? watchButton;
    private static ModAbilityButton? sonarButton;

    /// <summary>剩余次数。-1 表示无限。</summary>
    private static int tauntLeft = -1;
    private static int decoyLeft = -1;

    #endregion

    #region 生命周期

    /// <summary>开局装配本地玩家的技能按钮。由 PropHuntRuntime.OnGameStart 调用。</summary>
    public static void Setup(Virial.Game.Game game)
    {
        Teardown();

        if (!PropHuntGameModeRegistration.IsPropHuntActive) return;

        var local = GamePlayer.LocalPlayer;
        if (local == null) return;

        try
        {
            if (local.IsImpostor)
                SetUpSeekerButtons(game, local);
            else
                SetUpHiderButtons(game, local);
        }
        catch (Exception exception)
        {
            HsgDebug.LogError($"[HSG] 道具躲猫猫：技能按钮装配失败：{exception}");
        }
    }

    public static void Teardown()
    {
        tauntButton = null;
        decoyButton = null;
        watchButton = null;
        sonarButton = null;
        tauntLeft = -1;
        decoyLeft = -1;

        PropHuntSpectator.Reset();
        PropHuntDecoys.Clear();
    }

    /// <summary>由 PropHuntRuntime.OnUpdate 调用。</summary>
    public static void Update()
    {
        PropHuntSpectator.Update();
        PropHuntDecoys.Update();
    }

    #endregion

    #region 躲藏者按钮

    private static void SetUpHiderButtons(Virial.Game.Game game, GamePlayer local)
    {
        if (!PropHuntSettings.EnableHiderSkills) return;

        // --- 队友视角 ---
        // 这里刻意用最低级的 AbilityButton 重载自己装配，而不是带 player 的那个：
        // 带 player 的重载会把 Availability 包一层 player.CanMove，
        // 而观察队友的时候 CanMove 恰好是 false（Nebula 的相机补丁所致），
        // 用那个重载会导致「进去就出不来」—— 按钮自己把自己锁死。
        if (PropHuntSettings.EnableSpectate)
        {
            watchButton = NebulaAPI.Modules.AbilityButton(game, isLeftSideButton: true);
            watchButton.SetLabel("hsg.propHunt.watch");
            watchButton.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            // 主键：切到下一位队友。
            // 【不要改回 AidAction】它默认是 LeftShift，会和本模式的
            // 「Shift + 方向键微调道具」抢同一个键。详见类注释。
            watchButton.BindKey(VirtualKeyInput.SpectatorRight, "hsg.propHunt.watchNext");

            // 子操作键 = 直接退出观察，不必把队友轮完一圈。
            // BindSubKey 会自动在按钮上画出键位提示。
            // 如果玩家把这个键解绑了，NebulaInput.GetInput 会返回空、这里静默跳过 ——
            // 所以 PropHuntInput 里还留了一条键盘 C 的保底路径。
            watchButton.BindSubKey(VirtualKeyInput.Spectator, "hsg.propHunt.watchExit");
            watchButton.OnSubAction = _ => PropHuntSpectator.StopByPlayer();

            var watchTimer = NebulaAPI.Modules.Timer(game, 0.4f).SetAsAbilityTimer();
            watchButton.CoolDownTimer = watchTimer.Start();

            watchButton.Availability = _ => PropHuntSpectator.CanUse() || PropHuntSpectator.IsWatching;
            watchButton.Visibility = _ => !local.IsDead && PropHuntState.Phase
                is PropHuntPhase.Hiding or PropHuntPhase.Hunting;
            watchButton.OnClick = button =>
            {
                PropHuntSpectator.CycleNext();
                button.StartCoolDown();
            };
        }

        // --- 嘲讽 ---
        if (PropHuntSettings.EnableTaunt)
        {
            tauntLeft = PropHuntSettings.TauntCharges.GetValue();
            if (tauntLeft <= 0) tauntLeft = -1;

            tauntButton = NebulaAPI.Modules.AbilityButton(
                lifespan: game,
                player: local,
                input: VirtualKeyInput.Ability,
                inputHelp: "hsg.propHunt.taunt",
                cooldown: PropHuntSettings.TauntCooldown.GetValue(),
                label: "hsg.propHunt.taunt",
                image: null,
                availability: _ => PropHuntState.Phase == PropHuntPhase.Hunting && tauntLeft != 0,
                visibility: _ => !local.IsDead);
            tauntButton.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            if (tauntLeft > 0) tauntButton.ShowUsesIcon(0, tauntLeft.ToString());

            tauntButton.OnClick = button =>
            {
                var control = PlayerControl.LocalPlayer;
                if (control == null) return;

                var position = (Vector2)local.TruePosition;
                RpcTaunt.Invoke((control.PlayerId, position.x, position.y));

                if (tauntLeft > 0)
                {
                    tauntLeft--;
                    if (tauntLeft > 0) button.UpdateUsesIcon(tauntLeft.ToString());
                    else button.HideUsesIcon();
                }

                button.StartCoolDown();
            };
        }

        // --- 替身 ---
        if (PropHuntSettings.EnableDecoy)
        {
            decoyLeft = PropHuntSettings.DecoyCharges.GetValue();
            if (decoyLeft <= 0) decoyLeft = -1;

            decoyButton = NebulaAPI.Modules.AbilityButton(
                lifespan: game,
                player: local,
                input: VirtualKeyInput.SecondaryAbility,
                inputHelp: "hsg.propHunt.decoy",
                cooldown: PropHuntSettings.DecoyCooldown.GetValue(),
                label: "hsg.propHunt.decoy",
                image: null,
                // 必须先变成道具才能留替身 —— 不然没有贴图可抄。
                availability: _ => PropHuntState.Phase == PropHuntPhase.Hunting
                                   && decoyLeft != 0
                                   && PropManager.IsDisguised(local.PlayerId),
                visibility: _ => !local.IsDead);
            decoyButton.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            if (decoyLeft > 0) decoyButton.ShowUsesIcon(0, decoyLeft.ToString());

            decoyButton.OnClick = button =>
            {
                var control = PlayerControl.LocalPlayer;
                if (control == null) return;
                if (!PropManager.TryGetConsoleIndex(control.PlayerId, out int consoleIndex)) return;

                // 用道具渲染器的世界坐标，而不是玩家的 TruePosition。
                // TruePosition 取的是碰撞体中心，跟贴图实际画在哪差着一段；
                // 而且玩家可能用 Shift + 方向键微调过道具的偏移，
                // 只有渲染器自己的坐标才是「别人眼里这个道具在哪」。
                var propRenderer = PropManager.Get(control.PlayerId);
                if (propRenderer == null) return;

                Vector2 position = propRenderer.transform.position;
                RpcDecoy.Invoke((
                    control.PlayerId,
                    consoleIndex,
                    position.x,
                    position.y,
                    PropHuntSettings.DecoyDuration.GetValue()));

                if (decoyLeft > 0)
                {
                    decoyLeft--;
                    if (decoyLeft > 0) button.UpdateUsesIcon(decoyLeft.ToString());
                    else button.HideUsesIcon();
                }

                button.StartCoolDown();
            };
        }
    }

    #endregion

    #region 抓捕者按钮

    private static void SetUpSeekerButtons(Virial.Game.Game game, GamePlayer local)
    {
        if (!PropHuntSettings.EnableSeekerSonar) return;

        sonarButton = NebulaAPI.Modules.AbilityButton(
            lifespan: game,
            player: local,
            input: VirtualKeyInput.Ability,
            inputHelp: "hsg.propHunt.sonar",
            cooldown: PropHuntSettings.SonarCooldown.GetValue(),
            label: "hsg.propHunt.sonar",
            image: null,
            availability: _ => PropHuntState.Phase == PropHuntPhase.Hunting,
            visibility: _ => !local.IsDead);
        sonarButton.SetLabelType(ModAbilityButton.LabelType.Impostor);

        sonarButton.OnClick = button =>
        {
            // 纯本地演出：扫描结果只有按下的人看得到，不需要 RPC。
            float radius = PropHuntSettings.SonarRadius.GetValue();
            var origin = (Vector2)local.TruePosition;

            var positions = GamePlayer.AllPlayers
                .Where(p => p != null && !p.IsImpostor && !p.IsDead && !p.IsDisconnected)
                .Select(p => (Vector2)p.TruePosition)
                .Where(p => Vector2.Distance(origin, p) <= radius)
                .ToArray();

            if (positions.Length > 0)
            {
                AmongUsUtil.Ping(positions, true);
            }
            else
            {
                PropHuntOverlay.ShowFlash(
                    Language.Translate("hsg.propHunt.notice.sonarEmpty"),
                    new Color(1f, 0.6f, 0.3f),
                    1.5f);
            }

            button.StartCoolDown();
        };
    }

    #endregion

    #region RPC

    /// <summary>
    /// 嘲讽。
    ///
    /// 「大嗓门」用的是原版 Noisemaker 的警报音：AmongUsUtil.InstantiateNoisemakerArrow
    /// 会按距离衰减音量、附带一个 3 秒后淡出的指向箭头，还会震手柄。
    /// 不用自带音频资源，也不用担心音量在远处也一样大。
    ///
    /// 只有活着的抓捕者会听到方向；躲藏方只看到一条提示，知道有人在作死。
    /// </summary>
    public static readonly RemoteProcess<(byte playerId, float x, float y)> RpcTaunt =
        new("HSG_PropHuntTaunt", (message, _) =>
        {
            if (!PropHuntGameModeRegistration.IsPropHuntActive) return;

            // 缩时由房主权威执行，避免每个客户端各扣一次。
            if (AmongUsClient.Instance.AmHost)
            {
                float bonus = PropHuntSettings.TauntTimeBonus.GetValue();
                if (bonus > 0f) PropHuntState.HostAdjust(-bonus);
            }

            var local = GamePlayer.LocalPlayer;
            if (local == null) return;

            var position = new Vector2(message.x, message.y);

            if (local.IsImpostor && !local.IsDead)
            {
                try
                {
                    AmongUsUtil.InstantiateNoisemakerArrow(position, true);
                }
                catch (Exception exception)
                {
                    // Noisemaker 的 prefab 理论上一直在，但万一取不到就退回原版躲猫猫的 Ping。
                    HsgDebug.LogWarning($"[HSG] 道具躲猫猫：嘲讽箭头失败，改用 Ping：{exception.Message}");
                    AmongUsUtil.Ping(new[] { position }, true);
                }

                PropHuntOverlay.ShowFlash(
                    Language.Translate("hsg.propHunt.notice.taunted"),
                    new Color(1f, 0.4f, 0.4f),
                    2f);
            }
            else if (local.PlayerId == message.playerId)
            {
                PropHuntOverlay.ShowFlash(
                    Language.Translate("hsg.propHunt.notice.tauntSelf"),
                    new Color(1f, 0.85f, 0.4f),
                    2f);
            }
            else
            {
                PropHuntOverlay.ShowFlash(
                    Language.Translate("hsg.propHunt.notice.tauntAlly"),
                    new Color(0.53f, 0.87f, 1f),
                    1.5f);
            }
        });

    /// <summary>替身。</summary>
    public static readonly RemoteProcess<(byte playerId, int consoleIndex, float x, float y, float duration)> RpcDecoy =
        new("HSG_PropHuntDecoy", (message, _) =>
        {
            if (!PropHuntGameModeRegistration.IsPropHuntActive) return;

            PropHuntDecoys.Spawn(message.playerId, message.consoleIndex, message.x, message.y, message.duration);

            var local = GamePlayer.LocalPlayer;
            if (local != null && local.PlayerId == message.playerId)
            {
                PropHuntOverlay.ShowFlash(
                    Language.Translate("hsg.propHunt.notice.decoy"),
                    new Color(0.53f, 0.87f, 1f),
                    1.5f);
            }
        });

    #endregion
}

#endregion
