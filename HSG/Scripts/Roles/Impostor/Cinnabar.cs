namespace NebulaN.Roles.Impostor;

/// <summary>
/// 朱砂（内鬼）。
/// 「除了普通刀还有欺骗和下毒的杀人方式。技能为点砂，被点砂的船员成为中砂者，
/// 和朱砂构成单向链子；朱砂可以随时按死中砂者，朱砂死了所有的中砂者死亡，死因为毒发。
/// 活着的中砂者可以跟随朱砂赢，但算船员人数。」
/// </summary>
public class Cinnabar : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration MaxMarksOption = NebulaAPI.Configurations.Configuration(
        "options.role.cinnabar.maxMarks", (1, 5), 2);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.cinnabar.cooldown", (10f, 90f, 2.5f), 30f, FloatConfigurationDecorator.Second);

    private Cinnabar() : base(
        "cinnabar", new Virial.Color(0.85f, 0.18f, 0.2f), RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [MaxMarksOption, CooldownOption])
    { }

    public static readonly Cinnabar MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte cinnabarId, byte targetId)> RpcMark =
        new("HSG.Cinnabar.Mark", (msg, _) =>
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (target?.AmOwner != true) return;
            target.AddModifierOnce<NebulaN.Roles.Modifier.Cinnabarred.Instance>(
                NebulaN.Roles.Modifier.Cinnabarred.MyRole, [msg.cinnabarId]);
        });

    /// <summary>毒发：朱砂按死中砂者，或朱砂自己死了牵连全体。</summary>
    static internal readonly RemoteProcess<byte> RpcPoison = new("HSG.Cinnabar.Poison", (targetId, _) =>
    {
        var target = GamePlayer.GetPlayer(targetId);
        if (target == null || target.IsDead) return;
        if (AmongUsClient.Instance.AmHost)
            target.Suicide(HalfSugarGift.Core.Patch.ExState.Poisoned, EventDetail.Kill, KillParameter.NormalKill);
    });

    /// <summary>当前所有中砂者。</summary>
    static internal IEnumerable<GamePlayer> MarkedOf(byte cinnabarId)
        => GamePlayer.AllPlayers.Where(p =>
            p.TryGetModifier<NebulaN.Roles.Modifier.Cinnabarred.Instance>(out var m) && m.CinnabarId == cinnabarId);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int marksLeft;

        void RuntimeAssignable.OnActivated()
        {
            marksLeft = MaxMarksOption;
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.IsCrewmate && p.PlayerId != MyPlayer.PlayerId
                     && !p.TryGetModifier<NebulaN.Roles.Modifier.Cinnabarred.Instance>(out _));
            tracker.SetColor(MyRole.RoleColor);

            var mark = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "cinnabar.mark", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && marksLeft > 0,
                b => !MyPlayer.IsDead && marksLeft > 0, false);
            mark.SetLabelType(ModAbilityButton.LabelType.Impostor);
            mark.ShowUsesIcon(3, marksLeft.ToString());
            mark.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                marksLeft--;
                mark.UpdateUsesIcon(marksLeft.ToString());
                RpcMark.Invoke((MyPlayer.PlayerId, target.PlayerId));
                button.StartCoolDown();
            };

            // 随时按死所有中砂者
            var press = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.SecondaryAbility, 0f, "cinnabar.press", null,
                b => MarkedOf(MyPlayer.PlayerId).Any(p => !p.IsDead),
                b => !MyPlayer.IsDead && MarkedOf(MyPlayer.PlayerId).Any(p => !p.IsDead), false);
            press.SetLabelType(ModAbilityButton.LabelType.Impostor);
            press.OnClick = button =>
            {
                foreach (var p in MarkedOf(MyPlayer.PlayerId).Where(p => !p.IsDead).ToList())
                    RpcPoison.Invoke(p.PlayerId);
            };
        }

        /// <summary>「危险恋情」：带着活的中砂者获胜。</summary>
        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            if (!AmOwner) return;
            if (ev.GameEnd == NebulaGameEnd.ImpostorWin && MarkedOf(MyPlayer.PlayerId).Any(p => !p.IsDead))
                new StaticAchievementToken("cinnabar.romance");
        }

        /// <summary>「特大投毒案」：朱砂之死连带两名以上中砂者。</summary>
        [OnlyMyPlayer, OnlyHost]
        void OnDie(PlayerDieEvent ev)
        {
            int alive = MarkedOf(MyPlayer.PlayerId).Count(p => !p.IsDead);
            if (alive >= 2) RpcMassPoison.Invoke(MyPlayer.PlayerId);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo || inEndScene)
                name += " ❦".Color(new Color(0.85f, 0.18f, 0.2f));
        }
    }

    static internal readonly RemoteProcess<byte> RpcMassPoison = new("HSG.Cinnabar.MassPoison", (id, _) =>
    {
        if (GamePlayer.GetPlayer(id)?.AmOwner == true) new StaticAchievementToken("cinnabar.massPoison");
    });
}
