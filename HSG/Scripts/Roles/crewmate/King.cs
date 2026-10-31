namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 国王（船员）。
///
/// 表格描述：「加冕船员使其多一票（加冕非船员则因刺杀死亡）。死亡时被加冕人替代死亡。
/// 被加冕人死亡后国王明牌不怕赌并可第二次加冕」。
/// </summary>
public class King : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.king.cooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration SecondCrownOption = NebulaAPI.Configurations.Configuration(
        "options.role.king.secondCrown", true);

    private King() : base(
        "king", new Virial.Color(0.9f, 0.8f, 0.2f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [CooldownOption, SecondCrownOption])
    { }

    public static readonly King MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte kingId, byte targetId)> RpcCrown =
        new("HSG.King.Crown", (msg, _) =>
        {
            var king = GamePlayer.GetPlayer(msg.kingId);
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (king?.Role is not Instance inst || target == null) return;
            inst.RegisterCrowned(target.PlayerId);
            if (target.AmOwner)
                target.AddModifier(NebulaN.Roles.Modifier.Crowned.MyRole, [msg.kingId]);
        });

    /// <summary>国王明牌（被加冕者死后）。</summary>
    static internal readonly RemoteProcess<byte> RpcReveal = new("HSG.King.Reveal", (kingId, _) =>
    {
        if (GamePlayer.GetPlayer(kingId)?.Role is Instance inst) inst.Reveal();
    });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private byte crownedId = 255;
        private int crownsLeft = 1;
        private bool revealed = false;

        public GamePlayer? Crowned => crownedId == 255 ? null : GamePlayer.GetPlayer(crownedId);

        int[] RuntimeAssignable.RoleArguments => [crownedId, crownsLeft, revealed ? 1 : 0];

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            tracker.SetColor(MyRole.RoleColor);

            var crown = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "king.crown", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && crownsLeft > 0 && crownedId == 255,
                b => !MyPlayer.IsDead && crownsLeft > 0, false);
            crown.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            crown.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                crownsLeft--;

                // 加冕非船员 → 刺杀，国王当场死亡
                if (!target.IsCrewmate || target.IsMadmate)
                {
                    new StaticAchievementToken("king.foolish");
                    MyPlayer.Suicide(HalfSugarGift.Core.Patch.ExState.Assassinated, EventDetail.Kill, KillParameter.NormalKill);
                    return;
                }

                new StaticAchievementToken("king.reward");
                RpcCrown.Invoke((MyPlayer.PlayerId, target.PlayerId));
                button.StartCoolDown();
            };
        }

        public void RegisterCrowned(byte id) => crownedId = id;

        public void Reveal() => revealed = true;

        /// <summary>国王被杀时，由被加冕者替死。</summary>
        [OnlyMyPlayer, EventPriority(EventPriority.High)]
        void OnCheckKilled(PlayerCheckKilledEvent ev)
        {
            if (ev.Result != KillResult.Kill) return;
            if (ev.Killer == MyPlayer) return; // 加冕错人被刺杀是国王自己的代价，不能让被加冕者顶替
            var crowned = Crowned;
            if (crowned == null || crowned.IsDead) return;
            ev.Result = KillResult.ObviousGuard;
            if (AmongUsClient.Instance.AmHost)
                crowned.Suicide(HalfSugarGift.Core.Patch.ExState.Substitute, EventDetail.Kill, KillParameter.NormalKill);
        }

        /// <summary>被加冕者死亡 → 国王明牌，并解锁第二次加冕。</summary>
        [OnlyHost]
        void OnAnyoneDie(PlayerDieEvent ev)
        {
            if (ev.Player.PlayerId != crownedId) return;
            RpcReveal.Invoke(MyPlayer.PlayerId);
        }

        [OnlyHost]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (crownedId == 255) return;
            if (Crowned is { IsDead: true }) RpcReveal.Invoke(MyPlayer.PlayerId);
        }

        /// <summary>明牌之后所有人都能看见国王的身份，且不怕赌。</summary>
        [OnlyMyPlayer]
        void OnCheckRoleVisibility(PlayerCheckRoleInfoVisibilityLocalEvent ev)
        {
            if (revealed) ev.CanSeeRole = true;
        }

        // 该事件在猜测者本机触发，不能用 [Local]（同烟幕）
        [OnlyMyPlayer]
        void OnCanGuess(PlayerCanGuessPlayerLocalEvent ev)
        {
            if (revealed) ev.CanGuess = false;
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (revealed && !inEndScene) name += " ♚".Color(new Color(0.9f, 0.8f, 0.2f));
        }

        /// <summary>明牌后解锁第二次加冕。</summary>
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner || !revealed) return;
            if (SecondCrownOption && crownedId != 255 && Crowned is { IsDead: true })
            {
                crownedId = 255;
                crownsLeft = Math.Max(crownsLeft, 1);
            }
        }
    }
}
