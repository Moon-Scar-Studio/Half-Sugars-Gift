namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 农场主（船员）。
///
/// 表格描述：「可以招募仆从帮助你做任务，你本身无法做任务，但是你有任务，
/// 在指定轮数内不做完会死」。
///
/// 实现：
/// · 农场主自己的任务面板是只读的 —— 他没法亲手完成任务（<see cref="RuntimeAssignable.InvalidateCrewmateTask"/>
///   不适用，这里直接在本地拦截任务小游戏）。
/// · 招募的仆从每完成一个任务，就替农场主推进一格进度。
/// · 到期未完成 → 暴毙。
/// </summary>
public class Plantation : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration TaskCountOption = NebulaAPI.Configurations.Configuration(
        "options.role.plantation.taskCount", (1, 10), 4);
    static private readonly IntegerConfiguration DeadlineOption = NebulaAPI.Configurations.Configuration(
        "options.role.plantation.deadline", (1, 10), 3);
    static private readonly IntegerConfiguration MaxServantsOption = NebulaAPI.Configurations.Configuration(
        "options.role.plantation.maxServants", (1, 3), 1);
    static private readonly FloatConfiguration RecruitCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.plantation.recruitCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);

    private Plantation() : base(
        "plantation", new Virial.Color(0.55f, 0.75f, 0.35f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [TaskCountOption, DeadlineOption, MaxServantsOption, RecruitCooldownOption])
    { }

    public static readonly Plantation MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte masterId, byte servantId)> RpcRecruit =
        new("HSG.Plantation.Recruit", (msg, _) =>
        {
            var servant = GamePlayer.GetPlayer(msg.servantId);
            if (servant?.AmOwner != true) return;
            servant.AddModifier(NebulaN.Roles.Modifier.Servant.MyRole, [msg.masterId]);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int recruited = 0;
        private int done = 0;
        private int roundsLeft;

        /// <summary>农场主自己不做任务，进度全靠仆从。</summary>
        RoleTaskType RuntimeRole.TaskType => RoleTaskType.RoleTask;
        bool RuntimeRole.HaveNormalTask => false;

        void RuntimeAssignable.OnActivated()
        {
            roundsLeft = DeadlineOption;
            if (!AmOwner) return;

            Helpers.TextHudContent("PlantationText", this,
                tmPro => tmPro.text = Language.Translate("role.plantation.progress")
                    .Replace("%DONE%", done.ToString())
                    .Replace("%TOTAL%", ((int)TaskCountOption).ToString())
                    .Replace("%ROUNDS%", roundsLeft.ToString()),
                true);

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId
                     && !p.TryGetModifier<NebulaN.Roles.Modifier.Servant.Instance>(out _));
            tracker.SetColor(MyRole.RoleColor);

            var recruit = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, RecruitCooldownOption, "plantation.recruit", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && recruited < MaxServantsOption,
                b => !MyPlayer.IsDead && recruited < MaxServantsOption, false);
            recruit.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            recruit.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                recruited++;
                RpcRecruit.Invoke((MyPlayer.PlayerId, target.PlayerId));
                new StaticAchievementToken("plantation.recruit");
                button.StartCoolDown();
            };
        }

        /// <summary>仆从完成了一个任务。</summary>
        public void OnServantTaskDone()
        {
            done++;
            if (done >= TaskCountOption && AmOwner)
                new StaticAchievementToken("plantation.harvest");
        }

        // 倒计时在所有客户端同步递减（原来只在房主递减，农场主自己 HUD 上的剩余回合永远不变）；
        // 死亡只由房主发起。
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (MyPlayer.IsDead) return;
            if (done >= TaskCountOption) return;
            roundsLeft--;
            if (roundsLeft <= 0 && AmongUsClient.Instance.AmHost)
                MyPlayer.Suicide(HalfSugarGift.Core.Patch.ExState.Overdue, EventDetail.Kill, KillParameter.NormalKill);
        }
    }
}
