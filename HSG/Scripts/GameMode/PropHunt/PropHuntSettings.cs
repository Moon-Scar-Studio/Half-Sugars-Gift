namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>
/// 道具躲猫猫的全部可配置项。
///
/// 原版 PropHunt 的可配置面分三层，这里全部迁移保留：
///
/// 【第一层】原版设置面板里的显式选项（PropHuntPlugin.Load / PropHuntSettings.SetupCustomSettings）
///   · Prop Hunt      → <see cref="EnablePropMechanics"/>
///   · Miss Penalty   → <see cref="MissTimePenalty"/>
///   · Infection      → <see cref="Infection"/>（原版代码里存在但被注释掉，这里实现出来并默认关闭）
///
/// 【第二层】原版预设 PropHuntPreset.SetRecommendations 里写死的推荐值
///   原版这些值由原版躲猫猫（HideNSeekGameOptionsV10）承载，Nebula 禁用了原版躲猫猫，
///   没有任何设置项可以承载它们，所以在这里提升为可配置项，默认值与原版推荐值一致。
///   · EscapeTime         = 240f → <see cref="EscapeTime"/>
///   · FinalCountdownTime = 30f  → <see cref="FinalCountdownTime"/>
///   · SeekerPings        = false→ <see cref="SeekerPings"/>
///   · SeekerFinalMap     = false→ <see cref="SeekerFinalMap"/>
///   · ImpostorLightMod   = 1    → <see cref="SeekerLightMod"/>
///
/// 【第三层】原版硬编码常量（PropHuntPlugin 的 const / Patches 里的字面量）
///   同样提升为可配置项，默认值与原版常量一致。
///   · propMoveSpeed    = 0.5f       → <see cref="PropMoveSpeed"/>
///   · maxPropDistance  = 0.6f       → <see cref="MaxPropDistance"/>
///   · 变身搜索半径      = 3f         → <see cref="PropSearchRadius"/>
///   · 失误后击杀冷却    = 3f         → <see cref="MissKillCooldown"/>
///   · 失误销毁半径      = 击杀距离+5  → <see cref="MissDestroyRadiusBonus"/> / <see cref="DestroyPropOnMiss"/>
///
/// 另外补充了原版由原版躲猫猫提供、Nebula 这边必须自己实现的两项：
///   · <see cref="SeekerCount"/>（原版靠内鬼人数设置，PreventZeroImpPatch 保证至少 1）
///   · <see cref="HidingTime"/>（原版躲猫猫的躲藏阶段，预设注释里提到「Longer Hiding Time」）
/// </summary>
public static class PropHuntSettings
{
    #region 第一层：原版显式选项

    /// <summary>
    /// 对应原版的 "Prop Hunt" 开关。
    /// 关掉之后本模式退化为一场没有道具机制的普通抓捕（计时器仍然运作），
    /// 保留它是为了完整迁移原版选项，同时方便调试。
    /// </summary>
    public static readonly BoolConfiguration EnablePropMechanics =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.enable", true);

    /// <summary>
    /// 对应原版的 "Miss Penalty"。抓捕者击空时从倒计时里扣掉的秒数。
    /// 原版：范围 0~60，步长 5，默认 10。
    /// </summary>
    public static readonly FloatConfiguration MissTimePenalty =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.missPenalty", (0f, 60f, 5f), 10f,
            FloatConfigurationDecorator.Second);

    /// <summary>
    /// 对应原版的 "Infection"。道具被抓后转变为抓捕者。
    /// 原版因为原版躲猫猫强依赖「只有一个内鬼」而把它注释掉了；
    /// Nebula 这边没有这个限制，所以真正实现出来，但保持原版默认值（关闭）。
    /// </summary>
    public static readonly BoolConfiguration Infection =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.infection", false);

    #endregion

    #region 第二层：原版预设推荐值

    /// <summary>抓捕者的追捕总时长。原版预设 EscapeTime = 240。</summary>
    public static readonly FloatConfiguration EscapeTime =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.escapeTime", (30f, 900f, 15f), 240f,
            FloatConfigurationDecorator.Second);

    /// <summary>最终倒计时长度。剩余时间进入这个区间后触发 Ping / 最终地图。原版预设 FinalCountdownTime = 30。</summary>
    public static readonly FloatConfiguration FinalCountdownTime =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.finalCountdownTime", (0f, 120f, 5f), 30f,
            FloatConfigurationDecorator.Second);

    /// <summary>
    /// 最终倒计时期间，是否周期性地向抓捕者播报所有存活道具的位置（原版躲猫猫的 Seeker Pings）。
    /// 原版预设 SeekerPings = false。
    /// </summary>
    public static readonly BoolConfiguration SeekerPings =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.seekerPings", false);

    /// <summary>
    /// 最终倒计时期间，是否给抓捕者常驻指向所有存活道具的箭头（原版躲猫猫的 Seeker Final Map）。
    /// 原版预设 SeekerFinalMap = false，且 SeekerAdminMapEnabledPatch 在关闭时强制屏蔽。
    /// </summary>
    public static readonly BoolConfiguration SeekerFinalMap =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.seekerFinalMap", false);

    /// <summary>抓捕者的视野倍率。原版预设 ImpostorLightMod = 1。</summary>
    public static readonly FloatConfiguration SeekerLightMod =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.seekerLightMod", (0.25f, 5f, 0.25f), 1f,
            FloatConfigurationDecorator.Ratio);

    #endregion

    #region 第三层：原版硬编码常量

    /// <summary>道具形态下按住 Shift 微调位置的速度。原版 const propMoveSpeed = 0.5f。</summary>
    public static readonly FloatConfiguration PropMoveSpeed =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.propMoveSpeed", (0.1f, 3f, 0.1f), 0.5f);

    /// <summary>道具相对本体能偏移的最大距离。原版 const maxPropDistance = 0.6f。</summary>
    public static readonly FloatConfiguration MaxPropDistance =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.maxPropDistance", (0.2f, 3f, 0.1f), 0.6f);

    /// <summary>按 R 变身时搜索最近控制台的半径。原版 FindClosestConsole(player, 3)。</summary>
    public static readonly FloatConfiguration PropSearchRadius =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.propSearchRadius", (1f, 10f, 0.5f), 3f);

    /// <summary>击空之后强制进入的击杀冷却。原版 SetKillTimer(3f)。</summary>
    public static readonly FloatConfiguration MissKillCooldown =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.missKillCooldown", (0f, 30f, 1f), 3f,
            FloatConfigurationDecorator.Second);

    /// <summary>击空时是否顺手销毁附近的一个控制台。原版 RPCFailedKill 无条件执行。</summary>
    public static readonly BoolConfiguration DestroyPropOnMiss =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.destroyPropOnMiss", true);

    /// <summary>击空销毁控制台的搜索半径 = 击杀距离 + 本值。原版为 KillDistance + 5。</summary>
    /// <remarks>
    /// 注意这里必须显式写出 decorator 参数。float 版 Configuration 的第 4 个位置参数是
    /// FloatConfigurationDecorator 而不是 predicate，省略它会让重载解析掉到 bool 版上。
    /// </remarks>
    public static readonly FloatConfiguration MissDestroyRadiusBonus =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.missDestroyRadiusBonus", (0f, 15f, 1f), 5f,
            FloatConfigurationDecorator.None, () => DestroyPropOnMiss);

    #endregion

    #region 补充项：Nebula 侧必须自己实现的部分

    /// <summary>抓捕者人数。原版靠原版内鬼人数设置 + PreventZeroImpPatch 保证至少 1 人。</summary>
    public static readonly IntegerConfiguration SeekerCount =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.seekerCount", (1, 5), 1);

    /// <summary>躲藏阶段时长，这段时间内抓捕者被冻结且看不见画面。</summary>
    public static readonly FloatConfiguration HidingTime =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.hidingTime", (0f, 120f, 5f), 30f,
            FloatConfigurationDecorator.Second);

    /// <summary>抓捕者的击杀冷却。</summary>
    public static readonly FloatConfiguration SeekerKillCooldown =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.killCooldown", (0f, 60f, 2.5f), 10f,
            FloatConfigurationDecorator.Second);

    /// <summary>
    /// 是否给存活的躲藏者显示「危险程度」提示条。
    /// 对应原版躲猫猫的 DangerMeter，判据同样是最近抓捕者的距离。
    /// </summary>
    public static readonly BoolConfiguration ShowDangerMeter =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.dangerMeter", true);

    /// <summary>危险度的警戒距离。抓捕者进入这个范围后提示条开始上涨。</summary>
    /// <remarks>float 版 Configuration 的第 4 个位置参数是 decorator 而不是 predicate，必须显式写出。</remarks>
    public static readonly FloatConfiguration DangerWarnDistance =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.dangerWarnDistance", (3f, 40f, 1f), 15f,
            FloatConfigurationDecorator.None, () => ShowDangerMeter);

    /// <summary>危险度的告警距离。抓捕者进入这个范围后提示条转红。始终不会大于警戒距离。</summary>
    public static readonly FloatConfiguration DangerAlertDistance =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.dangerAlertDistance", (1f, 20f, 0.5f), 6f,
            FloatConfigurationDecorator.None, () => ShowDangerMeter);

    #endregion

    #region 升级项：躲藏阶段的视野封锁

    /// <summary>
    /// 躲藏阶段是否给抓捕者上全屏黑幕。
    ///
    /// 旧版是靠 PatchManager.ShowScreenOverlay 实现的，但那个方法会把传入的 alpha 覆盖成 0.5，
    /// 等于只加了一层灰纱，抓捕者照样能看清躲藏者往哪跑。
    /// 现在由 PropHuntOverlay 接管，真正做到不透明，并在黑幕上显示倒计时。
    /// </summary>
    public static readonly BoolConfiguration SeekerBlackout =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.seekerBlackout", true);

    /// <summary>
    /// 抓捕者是否在所有人眼里明牌（名字变红 + 头顶挂标签）。
    ///
    /// 道具躲猫猫里「谁是猎人」本来就不是秘密 —— 抓捕者从开局就拿着刀满地图跑，
    /// 藏着这个信息只会让躲藏方在第一次被砍之前都搞不清该躲谁。
    /// 默认开启。
    /// </summary>
    public static readonly BoolConfiguration SeekerNameTag =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.seekerNameTag", true);

    #endregion

    #region 升级项：终盘加成

    /// <summary>
    /// 追捕剩余多少秒时给抓捕者上加成。0 表示关闭。
    ///
    /// 与 <see cref="FinalCountdownTime"/> 是两件事：那个控制的是情报（Ping / 箭头），
    /// 这个控制的是机动力。默认 60 秒，通常比情报阶段更早开始，
    /// 让抓捕者先「跑得动」，再「看得见」。
    /// </summary>
    public static readonly FloatConfiguration SeekerBoostTime =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.seekerBoostTime", (0f, 180f, 10f), 60f,
            FloatConfigurationDecorator.Second);

    /// <summary>终盘的移动速度倍率。</summary>
    public static readonly FloatConfiguration SeekerBoostSpeed =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.seekerBoostSpeed", (1f, 2f, 0.05f), 1.25f,
            FloatConfigurationDecorator.Ratio, () => SeekerBoostTime.GetValue() > 0f);

    /// <summary>
    /// 终盘的冷却倍率。0.5 表示冷却只要一半时间。
    ///
    /// 实现走 PlayerAttributes.CooldownSpeed（系数 = 1 / 本值）。
    /// 选它而不是 KillButtonLikeHandler.SetCooldown 的理由：
    /// CooldownSpeed 会被 TimerImpl 和原版击杀计时器同时读取，
    /// 所以「正在走的那一次冷却」也会立刻加速，不必等下一次才生效。
    /// </summary>
    public static readonly FloatConfiguration SeekerBoostCooldownRatio =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.seekerBoostCooldownRatio", (0.25f, 1f, 0.05f), 0.5f,
            FloatConfigurationDecorator.Ratio, () => SeekerBoostTime.GetValue() > 0f);

    #endregion

    #region 升级项：躲藏者趣味技能

    /// <summary>趣味技能总开关。关掉之后下面所有躲藏者技能一并消失。</summary>
    public static readonly BoolConfiguration EnableHiderSkills =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.hiderSkills", true);

    // --- 队友视角 ---

    /// <summary>藏好之后能不能切到队友视角观战。</summary>
    public static readonly BoolConfiguration EnableSpectate =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.spectate", true, () => EnableHiderSkills);

    /// <summary>是否必须先变成道具才能观察队友。关掉就是随时可看。</summary>
    public static readonly BoolConfiguration SpectateNeedsDisguise =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.spectateNeedsDisguise", true,
            () => EnableHiderSkills && EnableSpectate);

    // --- 嘲讽 ---

    /// <summary>嘲讽：主动暴露自己的位置，换取追捕倒计时缩短。</summary>
    public static readonly BoolConfiguration EnableTaunt =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.taunt", true, () => EnableHiderSkills);

    /// <summary>嘲讽冷却。</summary>
    public static readonly FloatConfiguration TauntCooldown =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.tauntCooldown", (5f, 120f, 5f), 30f,
            FloatConfigurationDecorator.Second, () => EnableHiderSkills && EnableTaunt);

    /// <summary>每人可用的嘲讽次数。0 表示不限次。</summary>
    public static readonly IntegerConfiguration TauntCharges =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.tauntCharges", (0, 10, 1), 3,
            () => EnableHiderSkills && EnableTaunt);

    /// <summary>
    /// 一次嘲讽从追捕倒计时里扣掉的秒数。
    /// 这是躲藏方付出「暴露位置」之后拿到的回报，走的是与击空惩罚同一条 HostAdjust 通道。
    /// 设为 0 就是纯粹的整活，不影响胜负。
    /// </summary>
    public static readonly FloatConfiguration TauntTimeBonus =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.tauntTimeBonus", (0f, 30f, 1f), 5f,
            FloatConfigurationDecorator.Second, () => EnableHiderSkills && EnableTaunt);

    // --- 替身 ---

    /// <summary>替身：在原地留下一个假道具后自己溜走。</summary>
    public static readonly BoolConfiguration EnableDecoy =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.decoy", true, () => EnableHiderSkills);

    /// <summary>替身冷却。</summary>
    public static readonly FloatConfiguration DecoyCooldown =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.decoyCooldown", (10f, 180f, 5f), 45f,
            FloatConfigurationDecorator.Second, () => EnableHiderSkills && EnableDecoy);

    /// <summary>替身留在场上的时间。</summary>
    public static readonly FloatConfiguration DecoyDuration =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.decoyDuration", (5f, 120f, 5f), 30f,
            FloatConfigurationDecorator.Second, () => EnableHiderSkills && EnableDecoy);

    /// <summary>每人可用的替身次数。0 表示不限次。</summary>
    public static readonly IntegerConfiguration DecoyCharges =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.decoyCharges", (0, 10, 1), 2,
            () => EnableHiderSkills && EnableDecoy);

    #endregion

    #region 升级项：抓捕者技能

    /// <summary>
    /// 声呐：抓捕者主动扫描周围，Ping 出范围内所有躲藏者。
    ///
    /// 默认关闭。它存在的意义是让房主在打开躲藏者技能之后有一个对称的补偿旋钮，
    /// 而不是让抓捕方单方面吃瘪。
    /// </summary>
    public static readonly BoolConfiguration EnableSeekerSonar =
        NebulaAPI.Configurations.Configuration("options.hsg.propHunt.sonar", false);

    /// <summary>声呐冷却。</summary>
    public static readonly FloatConfiguration SonarCooldown =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.sonarCooldown", (10f, 180f, 5f), 45f,
            FloatConfigurationDecorator.Second, () => EnableSeekerSonar);

    /// <summary>声呐半径。</summary>
    public static readonly FloatConfiguration SonarRadius =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.propHunt.sonarRadius", (5f, 40f, 1f), 15f,
            FloatConfigurationDecorator.None, () => EnableSeekerSonar);

    #endregion

    #region 设置页装配

    /// <summary>本模式的设置容器。只在道具躲猫猫模式下显示。</summary>
    public static IConfigurationHolder? Holder { get; private set; }

    /// <summary>由 PropHuntSettingsLoader 在 FixStructure 阶段调用。</summary>
    internal static void BuildHolder()
    {
        var definition = PropHuntGameModeRegistration.Definition;
        if (definition == null)
        {
            HsgDebug.LogWarning("[HSG] 道具躲猫猫：模式未注册，跳过设置页装配。");
            return;
        }

        Holder = NebulaAPI.Configurations.Holder(
            NebulaAPI.GUI.LocalizedTextComponent("options.hsg.propHunt.holder.title"),
            NebulaAPI.GUI.LocalizedTextComponent("options.hsg.propHunt.holder.detail"),
            new[] { ConfigurationTab.Settings },
            new[] { definition });

        Holder
            .AppendConfiguration(EnablePropMechanics)
            .AppendConfiguration(SeekerCount)
            .AppendConfiguration(HidingTime)
            .AppendConfiguration(SeekerBlackout)
            .AppendConfiguration(SeekerNameTag)
            .AppendConfiguration(EscapeTime)
            .AppendConfiguration(FinalCountdownTime)
            .AppendConfiguration(SeekerKillCooldown)
            // 终盘加成
            .AppendConfiguration(SeekerBoostTime)
            .AppendConfiguration(SeekerBoostSpeed)
            .AppendConfiguration(SeekerBoostCooldownRatio)
            // 躲藏者趣味技能
            .AppendConfiguration(EnableHiderSkills)
            .AppendConfiguration(EnableSpectate)
            .AppendConfiguration(SpectateNeedsDisguise)
            .AppendConfiguration(EnableTaunt)
            .AppendConfiguration(TauntCooldown)
            .AppendConfiguration(TauntCharges)
            .AppendConfiguration(TauntTimeBonus)
            .AppendConfiguration(EnableDecoy)
            .AppendConfiguration(DecoyCooldown)
            .AppendConfiguration(DecoyDuration)
            .AppendConfiguration(DecoyCharges)
            // 抓捕者技能
            .AppendConfiguration(EnableSeekerSonar)
            .AppendConfiguration(SonarCooldown)
            .AppendConfiguration(SonarRadius)
            .AppendConfiguration(ShowDangerMeter)
            .AppendConfiguration(DangerWarnDistance)
            .AppendConfiguration(DangerAlertDistance)
            .AppendConfiguration(MissTimePenalty)
            .AppendConfiguration(MissKillCooldown)
            .AppendConfiguration(DestroyPropOnMiss)
            .AppendConfiguration(MissDestroyRadiusBonus)
            .AppendConfiguration(Infection)
            .AppendConfiguration(SeekerPings)
            .AppendConfiguration(SeekerFinalMap)
            .AppendConfiguration(SeekerLightMod)
            .AppendConfiguration(PropMoveSpeed)
            .AppendConfiguration(MaxPropDistance)
            .AppendConfiguration(PropSearchRadius);

        HsgDebug.Log("[HSG] 道具躲猫猫设置页装配完成。");
    }

    #endregion
}

/// <summary>
/// 设置页装配入口。
/// 必须晚于 PostLoadAddons —— 模式定义在那个阶段才被创建，而 Holder 需要绑定到模式上，
/// 否则设置页会出现在所有模式里。
/// </summary>
[NebulaPreprocess(PreprocessPhase.FixStructure)]
public static class PropHuntSettingsLoader
{
    public static void Preprocess(NebulaPreprocessor preprocessor) => PropHuntSettings.BuildHolder();
}
