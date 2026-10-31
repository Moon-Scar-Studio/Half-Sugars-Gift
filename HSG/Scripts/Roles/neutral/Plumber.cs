namespace NebulaN.Roles.Neutral;

/// <summary>
/// 水管工（中立）。
/// 「拥有跳管的技能，但是你跳管道会来到地图外面，你可以在地图外面呆 x 秒，
/// 该技能每回合可使用 x 次。所有人一开始知道水管工的存在，
/// 如果水管工没被击杀，则所有人无法获胜。
/// 如果水管工在仅剩三人的情况下被杀，则船员内鬼中立共同获胜。」
/// </summary>
public class Plumber : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration OutsideDurationOption = NebulaAPI.Configurations.Configuration(
        "options.role.plumber.outsideDuration", (3f, 30f, 1f), 8f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration UsesPerRoundOption = NebulaAPI.Configurations.Configuration(
        "options.role.plumber.usesPerRound", (1, 5), 2);
    static private readonly IntegerConfiguration EveryoneWinThresholdOption = NebulaAPI.Configurations.Configuration(
        "options.role.plumber.everyoneWinThreshold", (2, 5), 3);

    private Plumber() : base(
        "plumber", HalfSugarGift.Core.Patch.ExTeam.PlumberTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.PlumberTeam,
        [OutsideDurationOption, UsesPerRoundOption, EveryoneWinThresholdOption])
    { }

    public static readonly Plumber MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>水管工死在「只剩三人」的局面 → 全员共同获胜。</summary>
    static internal readonly RemoteProcess<byte> RpcEveryoneWin = new("HSG.Plumber.EveryoneWin", (_, __) => { });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int usesLeft;
        private float outsideLeft = 0f;
        private Vector2 returnPos;

        bool RuntimeRole.CanUseVent => false;

        void RuntimeAssignable.OnActivated()
        {
            usesLeft = UsesPerRoundOption;
            if (!AmOwner) return;

            var dive = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, 0f, "plumber.dive", null,
                b => usesLeft > 0 && outsideLeft <= 0f && MyPlayer.CanMove,
                b => !MyPlayer.IsDead, false);
            dive.OnClick = button =>
            {
                usesLeft--;
                returnPos = new Vector2(MyPlayer.TruePosition.x, MyPlayer.TruePosition.y);
                outsideLeft = OutsideDurationOption;
                // 「地图外面」：挪到地图边界之外，同时隐身避免被隔墙看到
                PatchManager.MovePlayer(MyPlayer, returnPos + new Vector2(0f, 60f));
                new StaticAchievementToken("plumber.newWorld");
            };
        }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner || outsideLeft <= 0f) return;
            outsideLeft -= ev.DeltaTime;
            MyPlayer.GainAttribute(PlayerAttributes.Invisible, 0.25f, false, 0, "plumber:outside");
            if (outsideLeft <= 0f)
            {
                outsideLeft = 0f;
                PatchManager.MovePlayer(MyPlayer, returnPos);
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (AmOwner && outsideLeft > 0f)
            {
                outsideLeft = 0f;
                PatchManager.MovePlayer(MyPlayer, returnPos);
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev) => usesLeft = UsesPerRoundOption;

        /// <summary>所有人都知道水管工的存在（身份公开）。</summary>
        [OnlyMyPlayer]
        void OnCheckRoleVisibility(PlayerCheckRoleInfoVisibilityLocalEvent ev) => ev.CanSeeRole = true;

        /// <summary>水管工还活着 → 谁都别想赢。</summary>
        [OnlyHost]
        void OnEndCriteriaPreMet(EndCriteriaPreMetEvent ev)
        {
            if (MyPlayer.IsDead) return;
            ev.Reject();
        }

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            // 自己活着时局面被卡住，所以水管工只能靠「全员共赢」或跟随结算拿分
            ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.PlumberEveryoneWin);
        }

        /// <summary>只剩三人时被杀 → 船员 / 内鬼 / 中立共同获胜。</summary>
        [OnlyHost, OnlyMyPlayer]
        void OnDie(PlayerDieEvent ev)
        {
            int alive = HalfSugarGift.Core.Patch.ExHelpers.AlivePlayers.Count();
            if (alive > EveryoneWinThresholdOption) return;

            var winners = BitMasks.AsPlayer();
            foreach (var p in GamePlayer.AllPlayers) winners.Add(p);
            NebulaAPI.CurrentGame?.TriggerGameEnd(
                HalfSugarGift.Core.Patch.ExTeam.PlumberEveryoneWin,
                GameEndReason.Special, winners);
        }
    }
}
