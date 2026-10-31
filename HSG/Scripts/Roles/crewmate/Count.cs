namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 伯爵 &amp; 继承者（船员）。
///
/// 表格描述：「伯爵完成任务。完成自己的另一个任务，伯爵死亡时在下一回合复活，
/// 并且将转变为继承者，可以在会议上处死任意一个人」。
///
/// 实现：
/// · 开局额外获得一个「继承任务」（在普通任务之外多加一个配额）。
/// · 完成全部任务后死亡 → 下一次会议结束时原地复活并转为继承者。
/// · 继承者在会议上获得一个「处死」按钮，全局限一次。
/// </summary>
public class Count : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration ExtraTaskOption = NebulaAPI.Configurations.Configuration(
        "options.role.count.extraTasks", (1, 5), 1);
    static private readonly BoolConfiguration ReviveNeedsTasksOption = NebulaAPI.Configurations.Configuration(
        "options.role.count.reviveNeedsTasks", true);
    static private readonly BoolConfiguration ExecuteOnlyOnceOption = NebulaAPI.Configurations.Configuration(
        "options.role.count.executeOnlyOnce", true);

    private Count() : base(
        "count", new Virial.Color(0.55f, 0.35f, 0.75f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [ExtraTaskOption, ReviveNeedsTasksOption, ExecuteOnlyOnceOption])
    { }

    public static readonly Count MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>复活伯爵并转为继承者。由房主发起，全客户端同步状态。</summary>
    static internal readonly RemoteProcess<byte> RpcInherit = new("HSG.Count.Inherit", (countId, _) =>
    {
        var player = GamePlayer.GetPlayer(countId);
        if (player?.Role is not Instance inst) return;
        inst.BecomeHeir();
    });

    static internal readonly RemoteProcess<(byte heirId, byte targetId)> RpcExecute = new("HSG.Count.Execute", (msg, _) =>
    {
        var heir = GamePlayer.GetPlayer(msg.heirId);
        var target = GamePlayer.GetPlayer(msg.targetId);
        if (heir == null || target == null || target.IsDead) return;
        if (AmongUsClient.Instance.AmHost)
            // 默认的 KillCondition.NormalKill 带 InTaskPhase，会议中会被驳回，必须显式放宽
            heir.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.MeetingKill, KillCondition.BothAlive);
    });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private bool isHeir = false;
        private bool pendingInherit = false;
        private bool executeUsed = false;

        int[] RuntimeAssignable.RoleArguments => [isHeir ? 1 : 0];

        void RuntimeAssignable.OnActivated() { }

        /// <summary>转为继承者：原地复活，拿到会议处死权。</summary>
        public void BecomeHeir()
        {
            isHeir = true;
            // Revive 自身会广播 RPC；本方法在所有客户端执行，只能由房主调用一次
            if (MyPlayer.IsDead && AmongUsClient.Instance.AmHost)
                MyPlayer.Revive(null, MyPlayer.TruePosition, true);
            if (AmOwner) new StaticAchievementToken("count.inherit");
        }

        /// <summary>伯爵多背一份「继承任务」。</summary>
        [Local]
        void SetTask(PlayerTasksTrySetLocalEvent ev)
        {
            if (ev.Player.Role.Role != MyRole) return;
            ev.AddExtraQuota(ExtraTaskOption);
        }

        /// <summary>伯爵死亡 → 记下「下一回合复活」。</summary>
        [OnlyHost, OnlyMyPlayer]
        void OnDie(PlayerDieEvent ev)
        {
            if (isHeir) return;
            if (ReviveNeedsTasksOption && !MyPlayer.Tasks.IsCompletedCurrentTasks) return;
            pendingInherit = true;
        }

        [OnlyHost]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (!pendingInherit) return;
            // 这一轮会议被票出去的话，继承作废
            if (ev.Exiled.Any(p => p == MyPlayer)) { pendingInherit = false; return; }
            pendingInherit = false;
            RpcInherit.Invoke(MyPlayer.PlayerId);
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (!isHeir || MyPlayer.IsDead) return;
            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(HalfSugarGift.Core.Patch.ExHelpers.MeetingIcon,
                p =>
                {
                    executeUsed = true;
                    RpcExecute.Invoke((MyPlayer.PlayerId, p.MyPlayer.PlayerId));
                    if (p.MyPlayer.IsImpostor) new StaticAchievementToken("count.precise");
                },
                p => !(ExecuteOnlyOnceOption && executeUsed)
                     && !p.MyPlayer.IsDead && !p.MyPlayer.AmOwner && !MyPlayer.IsDead
                     && !NebulaN.Roles.Modifier.Smokescreen.IsProtected(p.MyPlayer),
                ExecuteOnlyOnceOption));
        }

        string? RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort, bool canSeeAllInfo)
            => isHeir ? Language.Translate(isShort ? "role.count.heir.short" : "role.count.heir.name") : null;
    }
}
