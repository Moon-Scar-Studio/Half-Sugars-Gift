namespace NebulaN.Roles.Neutral;

/// <summary>
/// 纯白（中立）。
/// 「中立阵容，拥有一把刀。技能为调色，需要对玩家使用后加入对方阵营，刀在加入阵营后才能解封。
/// 纯白可以看到选择加入的玩家的职业，但被选的人看不到纯白。」
/// 跟随所选择的人胜利。
/// </summary>
public class PureWhite : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration KillCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.pureWhite.killCooldown", (10f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);

    private PureWhite() : base(
        "pureWhite", new Virial.Color(1f, 1f, 1f), RoleCategory.NeutralRole, NebulaTeams.JackalTeam,
        [KillCooldownOption])
    { }

    public static readonly PureWhite MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool DefinedRole.IsKiller => true;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte pureWhiteId, byte targetId)> RpcJoin =
        new("HSG.PureWhite.Join", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.pureWhiteId)?.Role is Instance inst) inst.SetJoined(msg.targetId);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>已经调色加入的那个人。255 = 还是纯白。</summary>
        private byte joinedId = 255;
        public GamePlayer? Joined => joinedId == 255 ? null : GamePlayer.GetPlayer(joinedId);

        int[] RuntimeAssignable.RoleArguments => [joinedId];
        bool RuntimeRole.HasVanillaKillButton => false;
        bool RuntimeRole.CanUseVent => joinedId != 255;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            tracker.SetColor(new Virial.Color(1f, 1f, 1f));

            var tint = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, 0f, "pureWhite.tint", null,
                b => tracker.CurrentTarget != null && joinedId == 255,
                b => !MyPlayer.IsDead && joinedId == 255, false);
            tint.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                // 加入的阵营必须同步：胜利判定在其它客户端上读不到本机的 joinedId
                RpcJoin.Invoke((MyPlayer.PlayerId, target.PlayerId));

                if (target.Role.Role.Category == RoleCategory.NeutralRole
                    && !target.Role.Role.IsKiller)
                    new StaticAchievementToken("pureWhite.abstract");
                if (target.IsImpostor) new StaticAchievementToken("pureWhite.bloodied");
            };

            // 刀在加入阵营后才解封
            var killTracker = NebulaAPI.Modules.KillTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId && p.PlayerId != joinedId);
            var kill = NebulaAPI.Modules.KillButton(
                this, MyPlayer, true, VirtualKeyInput.Kill, null, KillCooldownOption,
                "kill", ModAbilityButton.LabelType.Impostor, null,
                (target, button) =>
                {
                    MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    button.StartCoolDown();
                },
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId && p.PlayerId != joinedId,
                b => joinedId != 255,
                b => !MyPlayer.IsDead && joinedId != 255);
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(kill.GetKillButtonLike());
        }

        /// <summary>纯白看得到自己加入对象的职业；对方看不到纯白。</summary>
        public void SetJoined(byte id) => joinedId = id;

        [Local]
        void OnDecorateName(PlayerDecorateNameEvent ev)
        {
            if (!AmOwner || joinedId == 255) return;
            if (ev.Player.PlayerId != joinedId) return;
            ev.Name += " " + ev.Player.Role.Role.DisplayColoredShort;
        }

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            var joined = Joined;
            if (joined == null) return;
            ev.SetWinIf(ev.LastWinners.Test(joined));
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
        {
            // 还没调色就别想跟着谁赢
            if (joinedId == 255) ev.IsBlocked = true;
        }
    }
}
