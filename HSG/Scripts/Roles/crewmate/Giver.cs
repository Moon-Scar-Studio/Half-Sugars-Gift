using Virial.Helpers;

namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 给予者（船员）。
///
/// 表格描述：「可给予玩家别的职业的一次技能，给予有次数限制，给予内鬼会直接死亡」。
///
/// 实现说明：
/// 直接复用主程序集夺取者（Snatcher）使用的能力拆分接口 <see cref="DefinedRole.GetUsurpedAbility"/>。
/// 该接口返回职业「供他人使用的能力版本」（<see cref="IUsurpableAbility"/>），目标的职业不会被改动。
///
/// 「一次性」的判定（在目标本机执行）：
/// - 扫描属于该能力的按钮（RelatedAbility 指向它，或寿命链挂在它下面），包装 OnClick。
///   点击后若按钮进入冷却 / 进入效果 / 本身没有冷却，即视为「用过一次」。
/// - 用过后全体客户端对该能力调用 Usurp()（官方的「能力被废」状态，按钮再按会损坏），
///   等所有按钮的效果结束后再释放整个能力，避免打断进行中的效果（隐身、计时等）。
/// - 若直到下一次会议结束都没找到任何按钮（被动能力、只在会议中操作的能力），则在该会议结束后释放。
/// </summary>
public class Giver : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration MaxUsesOption = NebulaAPI.Configurations.Configuration(
        "options.role.giver.maxUses", (1, 5), 1);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.giver.cooldown", (5f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration CandidateCountOption = NebulaAPI.Configurations.Configuration(
        "options.role.giver.candidateCount", (1, 10), 4);
    static private readonly ValueConfiguration<int> AbilityPoolOption = NebulaAPI.Configurations.Configuration(
        "options.role.giver.abilityPool", new string[]
        {
            "options.role.giver.abilityPool.crewmate",
            "options.role.giver.abilityPool.all",
        }, 0);

    static readonly Virial.Color GiverColor = new(1f, 0.85f, 0.45f);

    private Giver() : base(
        "giver", GiverColor, RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [MaxUsesOption, CooldownOption, CandidateCountOption, AbilityPoolOption])
    { }

    public static readonly Giver MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    #region 可拆分能力的职业判定

    static readonly Dictionary<DefinedRole, bool> extractableCache = new();

    /// <summary>
    /// 判断职业是否提供「供他人使用的能力」。
    /// 与 NebulaAPI 中 GetUsurpedAbility 的两个默认实现一一对应：
    /// DefinedUsurpableAdvancedRoleTemplate&lt;,&gt; 一定可拆；
    /// DefinedSingleAbilityRole&lt;A&gt; 仅当 A 实现 IUsurpableAbility 时可拆。
    /// </summary>
    static public bool CanExtractAbility(DefinedRole role)
    {
        if (extractableCache.TryGetValue(role, out var cached)) return cached;
        bool result = false;
        try
        {
            var type = role.GetType();
            for (var t = type; t != null && !result; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(DefinedUsurpableAdvancedRoleTemplate<,>)) result = true;

            if (!result)
            {
                foreach (var i in type.GetInterfaces())
                {
                    if (!i.IsGenericType || i.GetGenericTypeDefinition() != typeof(DefinedSingleAbilityRole<>)) continue;
                    if (typeof(IUsurpableAbility).IsAssignableFrom(i.GetGenericArguments()[0])) { result = true; break; }
                }
            }
        }
        catch { result = false; }
        extractableCache[role] = result;
        return result;
    }

    static IEnumerable<DefinedRole> GetGiftableRoles() =>
        (Nebula.Roles.Roles.AllRoles ?? new List<DefinedRole>())
            .Where(r => !r.IsSystemRole && r != MyRole)
            .Where(r => AbilityPoolOption.GetValue() == 1 || r.Category == RoleCategory.CrewmateRole)
            .Where(CanExtractAbility);

    #endregion

    #region 赠予的能力

    static readonly Dictionary<(byte giver, int serial), GiftedAbility> gifts = new();

    public class GiftedAbility : IGameOperator
    {
        public readonly (byte giver, int serial) Key;
        public readonly GamePlayer Target;
        public readonly DefinedRole SourceRole;
        public readonly IUsurpableAbility Ability;
        public readonly FlexibleLifespan Life;

        bool consumed = false;
        bool foundAnyButton = false;
        bool releaseRequested = false;
        readonly HashSet<ModAbilityButtonImpl> wrapped = new();
        float scanTimer = 0f;

        public GiftedAbility((byte, int) key, GamePlayer target, DefinedRole role, IUsurpableAbility ability, FlexibleLifespan life)
        {
            Key = key; Target = target; SourceRole = role; Ability = ability; Life = life;
        }

        /// <summary>按钮是否属于本能力（含子能力）。</summary>
        bool BelongsToMe(ModAbilityButtonImpl button)
        {
            var mine = new HashSet<object>(Ability.SubAbilities.Cast<object>()) { Ability };
            if (button.RelatedAbility != null && mine.Contains(button.RelatedAbility)) return true;

            ILifespan? current = button;
            for (int depth = 0; depth < 8; depth++)
            {
                if (current is not DependentLifespan dl) break;
                current = dl.parentLifespan;
                if (current == null) break;
                if (mine.Contains(current)) return true;
            }
            return false;
        }

        void ScanButtons()
        {
            var manager = GameOperatorManager.Instance;
            if (manager == null) return;
            foreach (var op in manager.operatorsDic.Keys.ToArray())
            {
                if (op is not ModAbilityButtonImpl button) continue;
                if (wrapped.Contains(button)) continue;
                if (!BelongsToMe(button)) continue;

                wrapped.Add(button);
                foundAnyButton = true;
                var original = button.OnClick;
                button.OnClick = b =>
                {
                    original?.Invoke(b);
                    if (consumed) return;
                    var timer = b.CoolDownTimer;
                    bool noCooldown = timer == null || (timer is GameTimer gt && gt.Max <= 0.01f);
                    if (b.EffectActive || (timer?.IsProgressing ?? false) || noCooldown)
                    {
                        consumed = true;
                        RpcConsume.Invoke((Key.giver, Key.serial));
                    }
                };
            }
        }

        // GiftedAbility 不是 IBindPlayer，不能用 [Local]，改为手动判断 Target.AmOwner
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!Target.AmOwner) return;
            if (releaseRequested) return;

            if (!consumed)
            {
                scanTimer -= Time.deltaTime;
                if (scanTimer <= 0f) { scanTimer = 0.5f; ScanButtons(); }
                return;
            }

            // 用过后：等所有效果结束再释放
            if (wrapped.All(b => !b.EffectActive))
            {
                releaseRequested = true;
                RpcRelease.Invoke((Key.giver, Key.serial));
            }
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (!Target.AmOwner || releaseRequested) return;
            // 被动 / 会议型能力：撑过一场会议即视为用掉
            if (!foundAnyButton)
            {
                releaseRequested = true;
                RpcRelease.Invoke((Key.giver, Key.serial));
            }
        }

        internal void OnConsumedAllClients()
        {
            consumed = true;
            try { Ability.Usurp(); } catch { }
        }
    }

    /// <summary>全体客户端：在目标身上生成赠予的能力。</summary>
    static internal readonly RemoteProcess<(byte giverId, int serial, byte targetId, int roleId)> RpcGive =
        new("HSG.Giver.GiveAbility", (msg, _) =>
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (target == null || target.IsDead) return;
            var role = Nebula.Roles.Roles.GetRole(msg.roleId);
            if (role == null) return;

            var life = new FlexibleLifespan(target.Role);
            IUsurpableAbility? ability;
            try { ability = role.GetUsurpedAbility(target, []); }
            catch (Exception e) { Debug.LogError("[HSG.Giver] " + e); return; }
            if (ability == null) return;
            ability.Register(life);

            var gift = new GiftedAbility((msg.giverId, msg.serial), target, role, ability, life);
            gift.Register(life);
            gifts[(msg.giverId, msg.serial)] = gift;

            if (target.AmOwner)
            {
                AmongUsUtil.PlayQuickFlash(GiverColor);
                NebulaAPI.CurrentGame?.GetModule<TitleShower>()?.SetText(
                    Language.Translate("role.giver.received").Replace("%ROLE%", role.DisplayColoredName),
                    GiverColor, 3f, true);
            }
        });

    static internal readonly RemoteProcess<(byte giverId, int serial)> RpcConsume =
        new("HSG.Giver.Consume", (msg, _) =>
        {
            if (gifts.TryGetValue((msg.giverId, msg.serial), out var gift)) gift.OnConsumedAllClients();
        });

    static internal readonly RemoteProcess<(byte giverId, int serial)> RpcRelease =
        new("HSG.Giver.Release", (msg, _) =>
        {
            if (gifts.Remove((msg.giverId, msg.serial), out var gift)) gift.Life.Release();
        });

    #endregion

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int usesLeft;
        private int serial = 0;
        private ModAbilityButton? giveButton;

        void RuntimeAssignable.OnActivated()
        {
            usesLeft = MaxUsesOption;
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            tracker.SetColor(MyRole.RoleColor);

            giveButton = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "giver.give", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && usesLeft > 0,
                b => !MyPlayer.IsDead && usesLeft > 0, false);
            giveButton.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            giveButton.ShowUsesIcon(3, usesLeft.ToString());

            giveButton.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;

                // 给予内鬼 → 当场暴毙，次数照样消耗
                if (target.IsImpostor)
                {
                    usesLeft--;
                    giveButton!.UpdateUsesIcon(usesLeft.ToString());
                    new StaticAchievementToken("giver.badEye");
                    MyPlayer.Suicide(PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    return;
                }

                OpenGiftWindow(target, button);
            };
        }

        /// <summary>弹出可赠送能力的职业列表（仅列出能拆出能力的职业）。</summary>
        private void OpenGiftWindow(GamePlayer target, ModAbilityButton button)
        {
            var candidates = GetGiftableRoles()
                .OrderBy(_ => Guid.NewGuid())
                .Take(CandidateCountOption)
                .ToList();
            if (candidates.Count == 0) return;

            var tabs = new (string? tab, Predicate<DefinedRole>? predicate)[] { (null, _ => true) };
            MetaScreen? screen = null;
            PatchManager.OpenRoleSelectWindowUsingTabs(
                candidates, tabs, false,
                Language.Translate("role.giver.select"),
                selected =>
                {
                    screen?.CloseScreen();
                    if (target.IsDead || MyPlayer.IsDead) return;
                    usesLeft--;
                    giveButton!.UpdateUsesIcon(usesLeft.ToString());
                    RpcGive.Invoke((MyPlayer.PlayerId, serial++, target.PlayerId, selected.Id));
                    new StaticAchievementToken("giver.gift");
                    button.StartCoolDown();
                },
                ref screen, true);
        }
    }
}
