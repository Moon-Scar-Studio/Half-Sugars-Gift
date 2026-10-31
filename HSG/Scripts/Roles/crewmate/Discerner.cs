namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 明鉴者（船员）。
///
/// 表格描述：「自由行动阶段可标记一人，在此轮与下一轮中，若被标记人出刀则获得提示，
/// 并刷新标记 cd。若下一轮结束后仍未获得提示，清除标记并增加 cd 20s」。
/// </summary>
public class Discerner : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.discerner.cooldown", (5f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration PenaltyOption = NebulaAPI.Configurations.Configuration(
        "options.role.discerner.penalty", (0f, 40f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration MarkRoundsOption = NebulaAPI.Configurations.Configuration(
        "options.role.discerner.markRounds", (1, 4), 2);

    private Discerner() : base(
        "discerner", new Virial.Color(0.35f, 0.7f, 0.95f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [CooldownOption, PenaltyOption, MarkRoundsOption])
    { }

    public static readonly Discerner MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>被标记者出刀 → 通知明鉴者。</summary>
    static internal readonly RemoteProcess<byte> RpcNotify = new("HSG.Discerner.Notify", (discernerId, _) =>
    {
        var discerner = GamePlayer.GetPlayer(discernerId);
        if (discerner?.AmOwner != true) return;
        AmongUsUtil.PlayQuickFlash(MyRole.RoleColor);
        (discerner.Role as Instance)?.OnHit();
    });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private GamePlayer? marked = null;
        private int roundsLeft = 0;
        private float extraCooldown = 0f;
        private ModAbilityButton? markButton;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            tracker.SetColor(MyRole.RoleColor);

            markButton = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "discerner.mark", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown,
                b => !MyPlayer.IsDead, false);
            markButton.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            markButton.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                marked = target;
                roundsLeft = MarkRoundsOption;
                var timer = NebulaAPI.Modules.Timer(this, CooldownOption + extraCooldown).SetAsAbilityTimer();
                button.CoolDownTimer = timer.Start();
            };
        }

        /// <summary>命中：刷新冷却、清掉惩罚。</summary>
        public void OnHit()
        {
            new StaticAchievementToken("discerner.insight");
            extraCooldown = 0f;
            marked = null;
            roundsLeft = 0;
            if (markButton != null)
                markButton.CoolDownTimer = NebulaAPI.Modules.Timer(this, 0.1f).SetAsAbilityTimer().Start();
        }

        /// <summary>任何人出刀时检查：如果凶手正是被标记的人，就通知明鉴者。</summary>
        // 标记对象只存在于明鉴者本机；原来的 [OnlyHost] 读的是房主那份（永远为 null），命中永远不会触发。
        // PlayerKillPlayerEvent 会在所有客户端触发，直接在本机判定即可。
        [Local]
        void OnAnyoneKill(PlayerKillPlayerEvent ev)
        {
            if (marked == null || ev.Murderer != marked) return;
            AmongUsUtil.PlayQuickFlash(MyRole.RoleColor);
            OnHit();
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (marked == null) return;
            roundsLeft--;
            if (roundsLeft > 0) return;
            // 到期没命中 → 清除标记并加罚冷却
            marked = null;
            extraCooldown += PenaltyOption;
        }

        /// <summary>在明鉴者自己的视角里，给被标记的人加一个记号。</summary>
        [Local]
        void OnDecorateName(PlayerDecorateNameEvent ev)
        {
            if (!AmOwner || marked == null) return;
            if (ev.Player != marked) return;
            ev.Name += " ◉".Color(MyRole.RoleColor.ToUnityColor());
        }
    }
}
