namespace NebulaN.Roles.Impostor;

/// <summary>
/// 猫头鹰（内鬼）。
///
/// 「拥有催眠技能，被催眠者会带一次性的刀，并且人数上算内鬼。
/// 如果刀了一个人以后将会变回正常，并且会议上所有人都能知道被刀的人的死因。
/// 如果不开刀，规定时间过了以后被催眠者自杀。
/// 催眠期间猫头鹰拥有『噬魂』技能，使用后被催眠者死亡。」
/// </summary>
public class Owl : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration DurationOption = NebulaAPI.Configurations.Configuration(
        "options.role.owl.duration", (15f, 120f, 5f), 45f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.owl.cooldown", (10f, 90f, 2.5f), 35f, FloatConfigurationDecorator.Second);
    static private readonly BoolConfiguration BlackoutOnlyOption = NebulaAPI.Configurations.Configuration(
        "options.role.owl.blackoutOnly", true);

    private Owl() : base(
        "owl", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [DurationOption, CooldownOption, BlackoutOnlyOption])
    { }

    public static readonly Owl MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>被催眠者手里那把一次性刀的存活时长，供 Hypnotized 修饰符读取。</summary>
    public static float HypnosisDuration => DurationOption;

    static internal readonly RemoteProcess<(byte owlId, byte targetId)> RpcHypnotize =
        new("HSG.Owl.Hypnotize", (msg, _) =>
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (target?.AmOwner != true) return;
            target.AddModifierOnce<NebulaN.Roles.Modifier.Hypnotized.Instance>(
                NebulaN.Roles.Modifier.Hypnotized.MyRole, [msg.owlId]);
            AmongUsUtil.PlayQuickFlash(new Virial.Color(0.45f, 0.3f, 0.6f));
        });

    /// <summary>噬魂：直接让被催眠者暴毙。</summary>
    static internal readonly RemoteProcess<byte> RpcDevour = new("HSG.Owl.Devour", (targetId, _) =>
    {
        var target = GamePlayer.GetPlayer(targetId);
        if (target == null || target.IsDead) return;
        if (AmongUsClient.Instance.AmHost)
            target.Suicide(HalfSugarGift.Core.Patch.ExState.SoulEaten, EventDetail.Kill, KillParameter.NormalKill);
    });

    /// <summary>被催眠者出刀了 → 全场在下次会议看到死因。</summary>
    static internal readonly RemoteProcess<(byte hypnotizedId, byte victimId)> RpcHypnosisKilled =
        new("HSG.Owl.HypnosisKilled", (msg, _) =>
        {
            PublicDeaths[msg.victimId] = msg.hypnotizedId;
        });

    /// <summary>被催眠者造成的死亡：victimId → 凶手。会议上公开。</summary>
    static internal readonly Dictionary<byte, byte> PublicDeaths = new();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private byte hypnotized = 255;

        void RuntimeAssignable.OnActivated()
        {
            PublicDeaths.Clear(); // 静态表，跨局不清会把上一局的死因带过来
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId && !p.IsImpostor);
            tracker.SetColor(Cor.impRed);

            var hypnotize = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "owl.hypnotize", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && hypnotized == 255 && BlackoutOk(),
                b => !MyPlayer.IsDead, false);
            hypnotize.SetLabelType(ModAbilityButton.LabelType.Impostor);
            hypnotize.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                hypnotized = target.PlayerId;
                RpcHypnotize.Invoke((MyPlayer.PlayerId, target.PlayerId));
                new StaticAchievementToken("owl.nightOwl");
                button.StartCoolDown();
            };

            // 噬魂：催眠生效期间随时可用，会议里也能用
            var devour = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.SecondaryAbility, 0f, "owl.devour", null,
                b => hypnotized != 255,
                b => !MyPlayer.IsDead && hypnotized != 255, false);
            devour.SetLabelType(ModAbilityButton.LabelType.Impostor);
            devour.OnClick = button =>
            {
                if (hypnotized == 255) return;
                RpcDevour.Invoke(hypnotized);
                new StaticAchievementToken("owl.nightOwl");
                hypnotized = 255;
            };
        }

        /// <summary>只在黑灯期间可以催眠（可关）。</summary>
        static bool BlackoutOk() => !BlackoutOnlyOption || AmongUsUtil.InElecSab;

        void OnAnyoneDie(PlayerDieEvent ev)
        {
            if (ev.Player.PlayerId == hypnotized) hypnotized = 255;
        }

        /// <summary>被催眠者干掉的人，死因对所有人公开。</summary>
        void OnDecorateName(PlayerDecorateNameEvent ev)
        {
            if (!PublicDeaths.TryGetValue(ev.Player.PlayerId, out var killerId)) return;
            var killer = GamePlayer.GetPlayer(killerId);
            if (killer == null) return;
            ev.Name += " (" + Language.Translate("role.owl.killedBy").Replace("%NAME%", killer.Name) + ")";
        }
    }
}
