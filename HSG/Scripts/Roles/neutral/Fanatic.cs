namespace NebulaN.Roles.Neutral;

/// <summary>
/// 狂信徒（中立）。
/// 「开局可使用技能跟随玩家，跟随之后会获得把刀，请帮助跟随玩家活到最后，
/// 被跟随玩家无法知晓你跟随了他」。
/// 跟随对象获胜时狂信徒一起获胜；房主也可以把它设成「独立获胜」。
/// </summary>
public class Fanatic : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly BoolConfiguration IndependentWinOption = NebulaAPI.Configurations.Configuration(
        "options.role.fanatic.independentWin", false);
    static private readonly FloatConfiguration KillCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.fanatic.killCooldown", (10f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);

    private Fanatic() : base(
        "fanatic", HalfSugarGift.Core.Patch.ExTeam.FanaticTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.FanaticTeam,
        [IndependentWinOption, KillCooldownOption])
    { }

    public static readonly Fanatic MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool DefinedRole.IsKiller => true;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte fanaticId, byte targetId)> RpcFollow =
        new("HSG.Fanatic.Follow", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.fanaticId)?.Role is Instance inst) inst.SetFollow(msg.targetId);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private byte followId = 255;
        public GamePlayer? Followed => followId == 255 ? null : GamePlayer.GetPlayer(followId);

        int[] RuntimeAssignable.RoleArguments => [followId];
        bool RuntimeRole.HasVanillaKillButton => false;
        bool RuntimeRole.CanUseVent => true;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            tracker.SetColor(MyRole.RoleColor);

            var follow = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, 0f, "fanatic.follow", null,
                b => tracker.CurrentTarget != null && followId == 255,
                b => !MyPlayer.IsDead && followId == 255, false);
            follow.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                // 跟随对象必须同步：胜利判定在其它客户端上读不到本机的 followId
                RpcFollow.Invoke((MyPlayer.PlayerId, target.PlayerId));
                new StaticAchievementToken("fanatic.messenger");
            };

            // 跟随之后才解锁的刀
            var killTracker = NebulaAPI.Modules.KillTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId && p.PlayerId != followId);
            var kill = NebulaAPI.Modules.KillButton(
                this, MyPlayer, true, VirtualKeyInput.Kill, null, KillCooldownOption,
                "kill", ModAbilityButton.LabelType.Impostor, null,
                (target, button) =>
                {
                    MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    button.StartCoolDown();
                },
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId && p.PlayerId != followId,
                b => followId != 255,
                b => !MyPlayer.IsDead && followId != 255);
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(kill.GetKillButtonLike());
        }

        /// <summary>只有狂信徒自己看得到跟随标记 —— 被跟随者不知情。</summary>
        public void SetFollow(byte id) => followId = id;

        [Local]
        void OnDecorateName(PlayerDecorateNameEvent ev)
        {
            if (!AmOwner || followId == 255) return;
            if (ev.Player.PlayerId == followId)
                ev.Name += " ✝".Color(MyRole.RoleColor.ToUnityColor());
        }

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            var followed = Followed;
            if (followed == null) return;

            if (IndependentWinOption)
            {
                // 独立获胜：跟随对象活到最后，且自己也活着
                ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.FanaticWin
                            && !MyPlayer.IsDead && !followed.IsDead);
            }
            else
            {
                // 跟随获胜：被跟随者赢，自己就赢
                ev.SetWinIf(ev.LastWinners.Test(followed));
            }
        }

        [OnlyHost]
        void OnEndCriteriaMet(EndCriteriaMetEvent ev)
        {
            if (!IndependentWinOption) return;
            var followed = Followed;
            if (followed == null || followed.IsDead || MyPlayer.IsDead) return;

            var winners = BitMasks.AsPlayer();
            winners.Add(MyPlayer);
            winners.Add(followed);
            ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.ExTeam.FanaticWin, 59,
                GameEndReason.Special, (int)winners.AsRawPattern);
        }
    }
}
