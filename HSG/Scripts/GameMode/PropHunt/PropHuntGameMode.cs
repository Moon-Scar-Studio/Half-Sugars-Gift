using Nebula.Roles.Assignment;

namespace HalfSugarGift.GameMode.PropHunt;

#region 模式容器

/*
 * 关于模式容器类型的选择
 * ----------------------
 * 最初的设计是自定义一个 IGameModePropHunt 接口 + 自己的 AbstractModuleContainer 子类，
 * 这样 DIManager 注入时不会带进任何外部规则模块，能拿到最干净的容器。
 *
 * 但这条路编译不过：
 *   CS0060: Inconsistent accessibility: base class 'AbstractModuleContainer'
 *           is less accessible than class 'PropHuntGameModeImpl'
 *
 * AbstractModuleContainer 是 NebulaAPI 程序集里的 internal 类型。
 * addon 的 --use-hidden-members 只是关掉了「访问检查」，允许我们**使用**这些成员；
 * 但 C# 对「基类可访问性不得低于派生类」的一致性检查（CS0060）依然生效，
 * 而其它程序集的 internal 在本程序集里不存在任何等价或更低的可访问性等级，
 * 所以无论把派生类声明成 internal 还是 private 嵌套类都无法通过。
 * 同理 IModuleContainer.AddModule 是 internal 接口成员，自己实现接口也不可靠。
 *
 * 因此改用 IGameModeStandard，代价是会被注入这些模块：
 *   ImpostorGameRule / CrewmateGameRule / LoversCriteria / JackalCriteria
 * 它们唯一的副作用是调用 TriggerGameEnd 触发不属于本模式的胜负判定
 * （内鬼数×2≥存活数、任务全清、破坏倒计时……）。
 *
 * 这些统统走 CriteriaManager，而 CriteriaManager 在正式结算前会跑一次
 * EndCriteriaPreMetEvent 并丢弃被 Reject 的条目 —— 这是公开 API。
 * 所以在 PropHuntRuntime 里拦下所有外来结算即可，见 PropHuntRuntime.OnEndCriteriaPreMet。
 *
 * 本模式自己的胜负走 NebulaGameEnd.RpcSendGameEnd，完全绕开 CriteriaManager，
 * 不会被自己的拦截器误伤。
 */

#endregion

#region 角色分配

/// <summary>
/// 道具躲猫猫的角色分配器。
///
/// 本模式不使用 Nebula 的职业系统：抓捕者一律是原版 Impostor，道具一律是原版 Crewmate。
/// 这样击杀、视野、通风口等原版机制可以直接复用，同时不会牵扯任何 mod 职业。
/// </summary>
public class PropHuntRoleAllocator : IRoleAllocator
{
    public void Assign(List<byte> impostors, List<byte> others)
    {
        // 分配是否被调用的直接证据：若日志缺失，说明模式识别或注册环节出了问题
        HsgDebug.Log($"[HSG] 道具躲猫猫分配开始：内鬼候选 {impostors.Count} 人，其余 {others.Count} 人。");
        var table = new RoleTable();

        var all = new List<byte>(impostors);
        all.AddRange(others);

        if (all.Count == 0) return;

        // 抓捕者人数：以设置为准，至少 1 人，且至少留 1 个道具。
        int wanted = Mathf.Clamp(PropHuntSettings.SeekerCount.GetValue(), 1, Mathf.Max(1, all.Count - 1));

        // 优先沿用原版已经选出的内鬼，不足则从其余玩家里随机补，多出则随机裁掉。
        var seekers = new List<byte>(impostors);
        var pool = new List<byte>(others);

        while (seekers.Count > wanted)
        {
            int index = UnityEngine.Random.Range(0, seekers.Count);
            pool.Add(seekers[index]);
            seekers.RemoveAt(index);
        }
        while (seekers.Count < wanted && pool.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            seekers.Add(pool[index]);
            pool.RemoveAt(index);
        }

        foreach (var playerId in all)
        {
            table.SetRole(playerId,
                seekers.Contains(playerId)
                    ? Nebula.Roles.Impostor.Impostor.MyRole
                    : Nebula.Roles.Crewmate.Crewmate.MyRole);
        }

        // 记录开局抓捕者名单，供 PropHuntState 的相关演出与判定使用
        PropHuntState.SetSeekersOnAssign(seekers);

        table.Determine();
    }
}

#endregion

#region 模式注册

/// <summary>
/// 道具躲猫猫的模式注册。
///
/// 不依赖 hvtXsvc.GameMode.Framework 的 GameModeBuilder（那套是牛仔对决的试验性脚手架），
/// 这里自己完成三件事：注册 DI 容器 → 构造 GameModeDefinition → 应用 Harmony 补丁。
/// </summary>
[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
public static class PropHuntGameModeRegistration
{
    /// <summary>本模式的翻译键。</summary>
    public const string TranslationKey = "gamemode.hsg.propHunt";

    /// <summary>本模式创建房间所需的最少人数。原版 PropHunt 把躲猫猫的最低人数从 4 降到 2。</summary>
    public const int MinPlayers = 2;

    /// <summary>注册成功后持有的模式定义。为 null 表示注册失败，所有补丁都会自动短路。</summary>
    public static GameModeDefinition? Definition { get; private set; }

    /// <summary>当前是否正处于道具躲猫猫模式。所有补丁的第一道闸门。</summary>
    public static bool IsPropHuntMode =>
        Definition != null &&
        Nebula.Configuration.GeneralConfigurations.CurrentGameMode == Definition;

    /// <summary>模式已启用，且道具机制开关为开。</summary>
    public static bool IsPropHuntActive => IsPropHuntMode && PropHuntSettings.EnablePropMechanics;

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        try
        {
            // 1. 构造模式定义。
            Definition = CreateDefinition();
            if (Definition == null)
            {
                HsgDebug.LogError("[HSG] 道具躲猫猫：模式定义构造失败，模式未注册。");
                return;
            }

            // 2. 应用补丁。
            //    注意这里没有用 PatchAll：牛仔对决已经对整个程序集 PatchAll 过一次，
            //    再来一次会让带特性的补丁被应用两遍。详见 PropHuntHarmony 的注释。
            PropHuntHarmony.Apply(new Harmony("HSG.PropHunt"));

            // 打印本模式在列表中的索引：模式选择按索引同步，索引因机器上 addon 集合不同而漂移
            var modes = GameModes.AllGameModes.ToList();
            HsgDebug.Log($"[HSG] 道具躲猫猫模式注册成功，索引 {modes.IndexOf(Definition)}，模式总数：{modes.Count}");
        }
        catch (Exception exception)
        {
            HsgDebug.LogError($"[HSG] 道具躲猫猫模式注册失败：{exception}");
        }
    }

    /// <summary>
    /// 构造 Nebula 的模式定义。
    ///
    /// 我们需要 withRoleSettings = false（躲猫猫不该出现职业分配设置页），
    /// 但 Nebula 只把带这个参数的构造函数开成 private，所以先反射调用；
    /// 万一签名变了，退回到公开的 4 参构造函数（代价只是多出一个无用的职业设置页）。
    /// </summary>
    private static GameModeDefinition? CreateDefinition()
    {
        Func<IRoleAllocator> allocator = static () => new PropHuntRoleAllocator();

        // 容器类型选择的理由见文件顶部的说明。
        var moduleType = typeof(IGameModeStandard);

        var implType = typeof(GameModeDefinitionImpl);

        var fullConstructor = implType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            new[]
            {
                typeof(string), typeof(int), typeof(Type), typeof(Func<IRoleAllocator>),
                typeof(Func<bool, IEnumerator>), typeof(bool), typeof(bool)
            },
            null);

        if (fullConstructor != null)
        {
            return (GameModeDefinition)fullConstructor.Invoke(new object?[]
            {
                TranslationKey, MinPlayers, moduleType, allocator,
                null,   // alternativeRoutine：使用标准开局流程
                false,  // withRoleSettings：隐藏职业设置
                false   // shouldNotAdd：false 表示自动加入模式列表
            });
        }

        HsgDebug.LogWarning("[HSG] 道具躲猫猫：找不到完整构造函数，退回公开构造函数（会多出职业设置页）。");
        return new GameModeDefinitionImpl(TranslationKey, MinPlayers, moduleType, allocator);
    }
}

#endregion

#region 胜利条件

/// <summary>道具躲猫猫的两个结算条件。</summary>
[NebulaPreprocess(PreprocessPhase.PreFixStructure)]
public static class PropHuntGameEnds
{
    /// <summary>抓捕者胜利：在时间耗尽前抓完所有道具。</summary>
    public static GameEnd SeekerWin { get; private set; } = null!;

    /// <summary>道具胜利：撑到时间耗尽。</summary>
    public static GameEnd PropWin { get; private set; } = null!;

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        // CreateEnd 内部会自动给翻译键加 "end." 前缀（见 NebulaAPI GameEnd 构造函数），
        // 这里传不带前缀的名字，语言文件里写 "end.hsg.propHunt.xxx" 即可
        SeekerWin = NebulaAPI.Preprocessor!.CreateEnd("hsg.propHunt.seekerWin", Cor.impRed, 100);
        PropWin = NebulaAPI.Preprocessor!.CreateEnd("hsg.propHunt.propWin", Cor.cyan, 100);
    }
}

#endregion
