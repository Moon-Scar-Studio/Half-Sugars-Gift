namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 猎人（船员）。
/// 被投票放逐时，可以选择一名自己怀疑的目标一同带走。
/// 表格里 hvtXsvc 与蓝纹红针花各写了一版，这里取并集：放逐时选人 + 三个成就。
/// </summary>
public class Hunter : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration SelectTimeOption = NebulaAPI.Configurations.Configuration(
        "options.role.hunter.selectTime", (3f, 20f, 1f), 8f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration CanTakeDeadOption = NebulaAPI.Configurations.Configuration(
        "options.role.hunter.canTakeDead", false);

    private Hunter() : base(
        "hunter", new Virial.Color(0.4f, 0.55f, 0.2f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [SelectTimeOption, CanTakeDeadOption])
    { }

    public static readonly Hunter MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte hunterId, byte targetId)> RpcShoot =
        new("HSG.Hunter.Shoot", (msg, _) =>
        {
            var hunter = GamePlayer.GetPlayer(msg.hunterId);
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (hunter == null || target == null || target.IsDead) return;
            if (AmongUsClient.Instance.AmHost)
                // 猎人此时已被放逐（已死）且处在放逐场景：默认条件要求双方存活 + 任务阶段，必然被驳回
                hunter.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.MeetingKill, KillCondition.TargetAlive);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>本轮会议里选中的陪葬目标。</summary>
        private GamePlayer? chosen = null;
        /// <summary>最后一轮投票给猎人的伪装者们，用于「付出代价」挑战成就。</summary>
        private readonly List<byte> lastVoters = new();

        void RuntimeAssignable.OnActivated() { }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            chosen = null;
            lastVoters.Clear();
            if (MyPlayer.IsDead) return;

            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(HalfSugarGift.Core.Patch.ExHelpers.MeetingIconOf(1),
                p =>
                {
                    chosen = p.MyPlayer;
                    foreach (var state in buttonManager!.AllStates) state.SetSelect(state.MyPlayer == chosen);
                },
                p => !MyPlayer.IsDead && !p.MyPlayer.AmOwner
                     && (CanTakeDeadOption || !p.MyPlayer.IsDead)
                     && !NebulaN.Roles.Modifier.Smokescreen.IsProtected(p.MyPlayer),
                false));
        }

        [OnlyMyPlayer]
        void OnVotedForMe(PlayerVotedLocalEvent ev)
        {
            lastVoters.Clear();
            foreach (var v in ev.Voters) lastVoters.Add(v.PlayerId);
        }

        /// <summary>被放逐 → 带走选中的人。</summary>
        [Local, OnlyMyPlayer]
        void OnExiled(PlayerExiledEvent ev)
        {
            if (chosen == null || chosen.IsDead) return;

            RpcShoot.Invoke((MyPlayer.PlayerId, chosen.PlayerId));

            bool badGuy = chosen.IsImpostor
                || (chosen.Role.Role.Category == RoleCategory.NeutralRole && chosen.Role.Role.IsKiller);
            new StaticAchievementToken(badGuy ? "hunter.retribution" : "hunter.miss");

            // 挑战：带走最后一个投票给自己的伪装者
            if (chosen.IsImpostor && lastVoters.Contains(chosen.PlayerId)
                && !GamePlayer.AllPlayers.Any(p => !p.IsDead && p.IsImpostor && p != chosen))
                new StaticAchievementToken("hunter.challenge");
        }
    }
}
