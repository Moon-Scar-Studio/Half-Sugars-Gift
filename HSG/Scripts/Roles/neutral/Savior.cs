using Nebula.Configuration;   // GroupConfiguration
using Virial.Helpers;   // Shuffle / Shuffled 扩展方法所在的命名空间，不在全局 using 里
namespace NebulaN.Roles.Neutral;

/// <summary>
/// 救命恩人（中立）。
///
/// 表格描述：「拥有『救援』技能，可复活任意一个尸体，复活后新增副职业『被救援者』
/// （被救援者死亡救援技能返还）。被救援者跟随救命恩人获胜。
/// 救命恩人拥有与末日预言家一样的预言技能，获得的职业数由房主设置，
/// 并且拥有与爬虫工程师一样的标记技能，标记仅救命恩人和被救援者可见。
/// 救命恩人死亡被救援者一起暴毙。」
///
/// 三个技能的落地方式：
/// · <b>救援</b>：走到尸体旁复活它，被救者挂上 <see cref="NebulaN.Roles.Modifier.Rescued"/>。
/// · <b>洞察</b>：与本家末日预言家 <see cref="Oracle"/> 同源 —— 行动阶段对玩家使用，
///   候选职业由 <c>OracleSystem.GetRoleCandidate</c> 生成（<b>其中必定有一个是他的真实职业</b>），
///   结果在<b>下一次会议开始时</b>才揭晓。
/// · <b>标记</b>：会议上把某人标成你认为的职业，标记只有救命恩人与被救援者看得到。
/// </summary>
public class Savior : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration MaxRescuesOption = NebulaAPI.Configurations.Configuration(
        "options.role.savior.maxRescues", (2, 8), 3);
    static private readonly FloatConfiguration RescueCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.savior.cooldown", (10f, 90f, 2.5f), 30f, FloatConfigurationDecorator.Second);

    /// <summary>洞察一次能看到几个候选职业。表格里的「获得的职业数由房主设置」。</summary>
    static private readonly IntegerConfiguration OracleCandidatesOption = NebulaAPI.Configurations.Configuration(
        "options.role.savior.oracleCandidates", (1, 6), 3);
    static private readonly IntegerConfiguration OracleUsesOption = NebulaAPI.Configurations.Configuration(
        "options.role.savior.oracleUses", (0, 10, 1), 2,
        null, num => num == 0 ? Language.Translate("options.noLimit") : num.ToString());
    static private readonly FloatConfiguration OracleCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.savior.oracleCooldown", (0f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);

    static private readonly IntegerConfiguration MarkUsesOption = NebulaAPI.Configurations.Configuration(
        "options.role.savior.markUses", (1, 5), 2);

    private Savior() : base(
        "savior", HalfSugarGift.Core.Patch.ExTeam.SaviorTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.SaviorTeam,
        [MaxRescuesOption, RescueCooldownOption,
         new GroupConfiguration("options.role.savior.group.oracle",
            [OracleCandidatesOption, OracleUsesOption, OracleCooldownOption],
            HalfSugarGift.Core.Patch.ExTeam.SaviorTeam.Color.ToUnityColor().RGBMultiplied(0.65f)),
         MarkUsesOption])
    {
        // 洞察直接用了末日预言家的候选池实现，把两边的设置页关联起来
        ConfigurationHolder?.ScheduleAddRelated(() => [Oracle.MyRole.ConfigurationHolder!]);
    }

    public static readonly Savior MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new AssignableDocumentReplacement("%NUM%", OracleCandidatesOption.GetValue().ToString());
    }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    // ───────────────────────── 救援 ─────────────────────────

    static internal readonly RemoteProcess<(byte saviorId, byte targetId, float x, float y)> RpcRescue =
        new("HSG.Savior.Rescue", (msg, _) =>
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            var savior = GamePlayer.GetPlayer(msg.saviorId);
            if (target == null) return;
            if (AmongUsClient.Instance.AmHost)
                target.Revive(savior, new Virial.Compat.Vector2(msg.x, msg.y), true);
            if (target.AmOwner)
                target.AddModifierOnce<NebulaN.Roles.Modifier.Rescued.Instance>(
                    NebulaN.Roles.Modifier.Rescued.MyRole, [msg.saviorId]);
        });

    /// <summary>救命恩人死亡 → 被救援者牵连暴毙。</summary>
    static internal readonly RemoteProcess<byte> RpcEntangle = new("HSG.Savior.Entangle", (targetId, _) =>
    {
        var target = GamePlayer.GetPlayer(targetId);
        if (target == null || target.IsDead) return;
        if (AmongUsClient.Instance.AmHost)
            target.Suicide(HalfSugarGift.Core.Patch.ExState.Entangled, EventDetail.Kill, KillParameter.NormalKill);
    });

    static internal readonly RemoteProcess<byte> RpcRefund = new("HSG.Savior.Refund", (id, _) =>
    {
        if (GamePlayer.GetPlayer(id)?.Role is Instance inst) inst.Refund();
    });

    // ───────────────────────── 标记 ─────────────────────────

    /// <summary>救命恩人的标记：saviorId → (被标记者 → 他被认为的职业)。</summary>
    static internal readonly Dictionary<byte, Dictionary<byte, DefinedRole>> Marks = new();

    static internal readonly RemoteProcess<(byte saviorId, byte targetId, int roleId)> RpcMark =
        new("HSG.Savior.Mark", (msg, _) =>
        {
            var role = Nebula.Roles.Roles.GetRole(msg.roleId);
            if (role == null) return;
            if (!Marks.TryGetValue(msg.saviorId, out var map))
                Marks[msg.saviorId] = map = new Dictionary<byte, DefinedRole>();
            map[msg.targetId] = role;
        });

    /// <summary>本地玩家是不是有资格看这位救命恩人的标记（本人或他的被救援者）。</summary>
    static internal bool CanSeeMarksOf(byte saviorId)
    {
        var local = GamePlayer.LocalPlayer;
        if (local == null) return false;
        if (local.PlayerId == saviorId) return true;
        return local.TryGetModifier<NebulaN.Roles.Modifier.Rescued.Instance>(out var r) && r.SaviorId == saviorId;
    }

    /// <summary>把所有本地可见的标记拼成一段附加文本，供名字显示使用。</summary>
    static internal string? GetVisibleMarkText(byte targetId)
    {
        foreach (var (saviorId, map) in Marks)
        {
            if (!CanSeeMarksOf(saviorId)) continue;
            if (map.TryGetValue(targetId, out var role))
                return " [" + role.DisplayColoredShort + "]";
        }
        return null;
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int rescuesLeft;
        private int oracleLeft;
        private int marksThisMeeting;

        /// <summary>已经揭晓的洞察结果：被洞察者 → 候选职业的显示文本。</summary>
        private readonly Dictionary<byte, (string longName, string shortName)> insightResults = new();
        /// <summary>还没到会议、暂时压着的洞察结果。</summary>
        private readonly Dictionary<byte, DefinedRole[]> pendingInsights = new();

        bool RuntimeRole.CanUseVent => true;

        void RuntimeAssignable.OnActivated()
        {
            Marks.Remove(MyPlayer.PlayerId); // 静态表，跨局不清会把上一局的标记带过来
            rescuesLeft = MaxRescuesOption;
            oracleLeft = OracleUsesOption == 0 ? 1000 : OracleUsesOption;
            if (!AmOwner) return;

            SetUpRescueButton();
            SetUpOracleButton();
        }

        // ── 救援 ──
        private void SetUpRescueButton()
        {
            // 公开 API 没有「尸体追踪器」，所以自己按距离找脚下最近的尸体
            var rescue = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, RescueCooldownOption, "savior.rescue", null,
                b => !b.IsInCooldown && rescuesLeft > 0 && NearestBody() != null,
                b => !MyPlayer.IsDead && rescuesLeft > 0, false);
            rescue.ShowUsesIcon(3, rescuesLeft.ToString());
            rescue.OnClick = button =>
            {
                var body = NearestBody();
                if (body == null || rescuesLeft <= 0) return;
                rescuesLeft--;
                rescue.UpdateUsesIcon(rescuesLeft.ToString());
                // ParentId 的最高位是标志位，主程序集取玩家 id 时都会 & 0x7F
                RpcRescue.Invoke((MyPlayer.PlayerId, (byte)(body.ParentId & 0x7F),
                    body.transform.position.x, body.transform.position.y));
                new StaticAchievementToken("savior.lifeFirst");
                button.StartCoolDown();
            };
        }

        /// <summary>脚下 1.2 米内最近的尸体。</summary>
        private DeadBody? NearestBody()
        {
            var pos = new Vector2(MyPlayer.TruePosition.x, MyPlayer.TruePosition.y);
            DeadBody? best = null;
            float bestDist = 1.2f;
            foreach (var body in Helpers.AllDeadBodies())
            {
                float d = Vector2.Distance(pos, body.transform.position);
                if (d > bestDist) continue;
                bestDist = d;
                best = body;
            }
            return best;
        }

        public void Refund() => rescuesLeft = Math.Min(rescuesLeft + 1, MaxRescuesOption);

        /// <summary>被救援者死亡 → 返还一次救援次数。</summary>
        [OnlyHost]
        void OnAnyoneDie(PlayerDieEvent ev)
        {
            if (!ev.Player.TryGetModifier<NebulaN.Roles.Modifier.Rescued.Instance>(out var m)) return;
            if (m.SaviorId != MyPlayer.PlayerId) return;
            RpcRefund.Invoke(MyPlayer.PlayerId);
        }

        // ── 洞察（与末日预言家同源） ──
        private void SetUpOracleButton()
        {
            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId
                     && !insightResults.ContainsKey(p.PlayerId) && !pendingInsights.ContainsKey(p.PlayerId));
            tracker.SetColor(MyRole.RoleColor);

            var oracle = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.SecondaryAbility, OracleCooldownOption,
                "savior.insight", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && oracleLeft > 0,
                b => !MyPlayer.IsDead && oracleLeft > 0, false);
            if (OracleUsesOption != 0) oracle.ShowUsesIcon(3, oracleLeft.ToString());
            oracle.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;

                // 候选池直接借用本家末日预言家的实现：result[0] 必定是真实职业
                var result = ModSingleton<OracleSystem>.Instance
                    .GetRoleCandidate(MyPlayer, target, OracleCandidatesOption);
                pendingInsights[target.PlayerId] = result.Shuffled();

                oracleLeft--;
                if (OracleUsesOption != 0) oracle.UpdateUsesIcon(oracleLeft.ToString());
                new StaticAchievementToken("savior.insight");
                button.StartCoolDown();
            };
        }

        /// <summary>洞察的结果压到下一次会议开始才揭晓。</summary>
        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            marksThisMeeting = 0;

            foreach (var (targetId, roles) in pendingInsights)
            {
                insightResults[targetId] = (
                    string.Join(", ", roles.Select(r => r.DisplayColoredName)),
                    string.Join(", ", roles.Select(r =>
                        roles.Length >= 2 ? r.GetRoleIconTagSmall() + " " + r.DisplayColoredShort : r.DisplayColoredName)));
            }
            if (pendingInsights.Count > 0)
            {
                AmongUsUtil.PlayQuickFlash(MyRole.RoleColor);
                pendingInsights.Clear();
            }

            SetUpMarkAction();
        }

        // ── 标记 ──
        private void SetUpMarkAction()
        {
            if (MyPlayer.IsDead) return;

            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(HalfSugarGift.Core.Patch.ExHelpers.MeetingIconOf(6),
                p =>
                {
                    var target = p.MyPlayer;
                    var candidates = (Nebula.Roles.Roles.AllRoles ?? new List<DefinedRole>())
                        .Where(r => !r.IsSystemRole && r.IsSpawnable)
                        .ToList();
                    if (candidates.Count == 0) return;

                    var tabs = new (string? tab, Predicate<DefinedRole>? predicate)[]
                    {
                        (null, _ => true),
                        ("options.role.savior.tab.crewmate", r => r.Category == RoleCategory.CrewmateRole),
                        ("options.role.savior.tab.impostor", r => r.Category == RoleCategory.ImpostorRole),
                        ("options.role.savior.tab.neutral",  r => r.Category == RoleCategory.NeutralRole),
                    };

                    MetaScreen? screen = null;
                    PatchManager.OpenRoleSelectWindowUsingTabs(
                        candidates, tabs, true,
                        Language.Translate("role.savior.markSelect").Replace("%NAME%", target.Name),
                        selected =>
                        {
                            screen?.CloseScreen();
                            marksThisMeeting++;
                            RpcMark.Invoke((MyPlayer.PlayerId, target.PlayerId, selected.Id));
                            new StaticAchievementToken("savior.mark");
                        },
                        ref screen, true);
                },
                p => marksThisMeeting < MarkUsesOption && !MyPlayer.IsDead && !p.MyPlayer.AmOwner
                     && !NebulaN.Roles.Modifier.Smokescreen.IsProtected(p.MyPlayer),
                false));
        }

        /// <summary>
        /// 名字上的两类信息：洞察结果替换职业名（只有救命恩人自己看得到），
        /// 标记则附加在后面（救命恩人与被救援者都看得到）。
        /// </summary>
        [Local]
        void ReflectRoleName(PlayerSetFakeRoleNameEvent ev)
        {
            if (insightResults.TryGetValue(ev.Player.PlayerId, out var insight))
                ev.Alternate(ev.InMeeting ? insight.longName : insight.shortName);

            var mark = GetVisibleMarkText(ev.Player.PlayerId);
            if (mark != null) ev.Append(mark);
        }

        void OnGameStart(GameStartEvent ev) => Marks.Clear();

        // ── 胜利条件 ──
        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.SaviorWin;

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.SaviorWin && !MyPlayer.IsDead);

        /// <summary>场上人数 ≤ 救命恩人阵营人数 → 获胜。</summary>
        [OnlyHost]
        void OnEndCriteriaMet(EndCriteriaMetEvent ev)
        {
            if (MyPlayer.IsDead) return;

            var camp = GamePlayer.AllPlayers
                .Where(p => !p.IsDead && (p == MyPlayer
                    || (p.TryGetModifier<NebulaN.Roles.Modifier.Rescued.Instance>(out var m)
                        && m.SaviorId == MyPlayer.PlayerId)))
                .ToList();
            if (camp.Count < HalfSugarGift.Core.Patch.ExHelpers.AlivePlayers.Count()) return;

            var winners = BitMasks.AsPlayer();
            foreach (var p in camp) winners.Add(p);
            ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.ExTeam.SaviorWin, 51,
                GameEndReason.Special, (int)winners.AsRawPattern);
        }
    }
}
