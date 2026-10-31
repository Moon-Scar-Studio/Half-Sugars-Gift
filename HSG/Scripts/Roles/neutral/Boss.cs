namespace NebulaN.Roles.Neutral;

/// <summary>
/// 老板（中立）。
/// 「拥有 3 次『招聘』技能，对一名玩家使用后该玩家立刻变成『加班狂』（不保留原本职业），
/// 但若招聘对象为伪装者阵营，则消耗一次次数且不生效。
/// 每次招聘后招聘技能消失，当前加班狂死亡后重新解锁。
/// 有加班狂的工资 ≥ 800 时，若有阵营达成获胜条件，则抢夺胜利。
/// 最后一个加班狂死亡后，老板跟随暴毙。」
/// </summary>
public class Boss : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static internal readonly IntegerConfiguration MaxHiresOption = NebulaAPI.Configurations.Configuration(
        "options.role.boss.maxHires", (1, 5), 3);
    static internal readonly IntegerConfiguration WinSalaryOption = NebulaAPI.Configurations.Configuration(
        "options.role.boss.winSalary", (200, 2000, 100), 800);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.boss.cooldown", (10f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);

    private Boss() : base(
        "boss", HalfSugarGift.Core.Patch.ExTeam.BossTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.BossTeam,
        [MaxHiresOption, WinSalaryOption, CooldownOption])
    { }

    public static readonly Boss MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte bossId, byte targetId)> RpcHire =
        new("HSG.Boss.Hire", (msg, _) =>
        {
            // 招聘状态在所有客户端同步：原来 currentHire 只存在老板本机，
            // 房主那边的「加班狂死亡 → 老板破产」永远不触发；老板本机的 currentHire 也永远不会复位，招不了第二个人。
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (GamePlayer.GetPlayer(msg.bossId)?.Role is Instance boss && target != null) boss.OnHired(target);
            if (target?.AmOwner != true || target.IsImpostor) return;
            target.SetRole(Workaholic.MyRole, [msg.bossId]);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int hiresLeft;
        /// <summary>当前在职的加班狂。</summary>
        private byte currentHire = 255;
        private bool everHired = false;

        bool RuntimeRole.CanUseVent => true;

        void RuntimeAssignable.OnActivated()
        {
            hiresLeft = MaxHiresOption;
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            tracker.SetColor(MyRole.RoleColor);

            var hire = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "boss.hire", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && hiresLeft > 0 && currentHire == 255,
                b => !MyPlayer.IsDead && hiresLeft > 0 && currentHire == 255, false);
            hire.ShowUsesIcon(3, hiresLeft.ToString());
            hireButton = hire;
            hire.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                // 招聘伪装者：白白浪费一次。次数扣减与招聘状态都经由 RPC 在所有客户端同步
                if (!target.IsImpostor) new StaticAchievementToken("boss.allInclusive");
                RpcHire.Invoke((MyPlayer.PlayerId, target.PlayerId));
                button.StartCoolDown();
            };
        }

        /// <summary>加班狂死亡 → 解锁招聘；最后一个死光 → 老板跟随暴毙。</summary>
        private ModAbilityButton? hireButton;

        public void OnHired(GamePlayer target)
        {
            hiresLeft--;
            hireButton?.UpdateUsesIcon(hiresLeft.ToString());
            if (target.IsImpostor) return;
            currentHire = target.PlayerId;
            everHired = true;
        }

        void OnAnyoneDie(PlayerDieEvent ev)
        {
            if (ev.Player.PlayerId != currentHire) return;
            currentHire = 255;
            if (!AmongUsClient.Instance.AmHost) return;
            if (hiresLeft <= 0 && everHired && !MyPlayer.IsDead)
                MyPlayer.Suicide(HalfSugarGift.Core.Patch.ExState.Bankrupt, EventDetail.Kill, KillParameter.NormalKill);
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.BossWin;

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.BossWin && !MyPlayer.IsDead);

        /// <summary>有加班狂工资达标 → 抢走别人的胜利。</summary>
        [OnlyHost]
        void OnEndCriteriaMet(EndCriteriaMetEvent ev)
        {
            if (MyPlayer.IsDead) return;

            var rich = GamePlayer.AllPlayers
                .Where(p => !p.IsDead && p.Role is Workaholic.Instance w
                            && w.BossId == MyPlayer.PlayerId && w.Salary >= WinSalaryOption)
                .ToList();
            if (rich.Count == 0) return;

            var winners = BitMasks.AsPlayer();
            winners.Add(MyPlayer);
            foreach (var p in rich) winners.Add(p);
            ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.ExTeam.BossWin, 49,
                GameEndReason.Special, (int)winners.AsRawPattern);
        }
    }
}

/// <summary>
/// 加班狂（中立，由老板招聘产生）。
/// 「初始工资由房主设定。做任务赚工资：普通/短任务 +100，长任务 +200。
/// 每分钟消耗 250 工资（会议阶段不计），工资归零则破产暴毙。」
/// </summary>
public class Workaholic : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration InitialSalaryOption = NebulaAPI.Configurations.Configuration(
        "options.role.workaholic.initialSalary", (50, 500, 50), 300);
    static private readonly IntegerConfiguration BurnRateOption = NebulaAPI.Configurations.Configuration(
        "options.role.workaholic.burnRate", (50, 500, 50), 250);

    private Workaholic() : base(
        "workaholic", HalfSugarGift.Core.Patch.ExTeam.BossTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.BossTeam,
        [InitialSalaryOption, BurnRateOption])
    { }

    public static readonly Workaholic MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    /// <summary>只能由老板招聘产生，不参与随机分配。</summary>
    bool ISpawnable.IsSpawnable => false;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => false;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255));

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;

        public byte BossId { get; private init; }
        public int Salary { get; private set; }

        private float burnTimer = 0f;

        public Instance(GamePlayer player, int bossId) : base(player)
        {
            BossId = (byte)bossId;
            Salary = InitialSalaryOption;
        }

        int[] RuntimeAssignable.RoleArguments => [BossId, Salary];
        RoleTaskType RuntimeRole.TaskType => RoleTaskType.RoleTask;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            Helpers.TextHudContent("WorkaholicText", this,
                tmPro => tmPro.text = Language.Translate("role.workaholic.salary")
                    .Replace("%NUM%", Salary.ToString()),
                true);
        }

        [Local]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent ev)
        {
            if (!AmOwner) return;
            // 长任务给 200，其余 100
            Salary += 100;
            if (Salary >= 999) new StaticAchievementToken("workaholic.dailyWage");
            RpcSyncSalary.Invoke((MyPlayer.PlayerId, Salary));
        }

        /// <summary>工资每分钟烧掉一截，会议阶段不计。</summary>
        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner || MyPlayer.IsDead) return;
            if (AmongUsUtil.InMeeting) return;

            burnTimer += ev.DeltaTime;
            if (burnTimer < 60f) return;
            burnTimer -= 60f;
            Salary -= BurnRateOption;
            RpcSyncSalary.Invoke((MyPlayer.PlayerId, Salary));

            if (Salary <= 0)
                MyPlayer.Suicide(HalfSugarGift.Core.Patch.ExState.Bankrupt, EventDetail.Kill, KillParameter.NormalKill);
        }

        public void SetSalary(int value) => Salary = value;

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.BossWin;

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.BossWin && !MyPlayer.IsDead);
    }

    /// <summary>工资是老板的胜利条件，所有客户端都要看到同一个数。</summary>
    static internal readonly RemoteProcess<(byte playerId, int salary)> RpcSyncSalary =
        new("HSG.Workaholic.SyncSalary", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.playerId)?.Role is Instance inst) inst.SetSalary(msg.salary);
        });
}
