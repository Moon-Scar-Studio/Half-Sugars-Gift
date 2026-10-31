namespace NebulaN.Roles.Impostor;

/// <summary>
/// 毒血杀手（内鬼）。
/// 「每次击杀都会解锁一次『下毒』技能。下毒可对某一位船员使用（对象不可为伪装者），
/// 使用后对象一旦对某位玩家使用技能，则显示为使用对象击杀了被使用技能的对象。
/// 若使用对象为警长并且执法对象为船员，船员也会死亡。」
/// </summary>
public class Toxin : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration MaxPoisonsOption = NebulaAPI.Configurations.Configuration(
        "options.role.toxin.maxPoisons", (1, 5), 3);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.toxin.cooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);

    private Toxin() : base(
        "toxin", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [MaxPoisonsOption, CooldownOption])
    { }

    public static readonly Toxin MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte toxinId, byte targetId)> RpcPoison =
        new("HSG.Toxin.Poison", (msg, _) =>
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (target?.AmOwner != true) return;
            target.AddModifierOnce<NebulaN.Roles.Modifier.Poisoned.Instance>(
                NebulaN.Roles.Modifier.Poisoned.MyRole, [msg.toxinId]);
        });

    /// <summary>毒发成功：回报毒血杀手，顺便判成就。</summary>
    static internal readonly RemoteProcess<(byte toxinId, byte poisonedId, byte victimId)> RpcPoisonWorked =
        new("HSG.Toxin.PoisonWorked", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.toxinId)?.AmOwner != true) return;
            new StaticAchievementToken("toxin.venom");
            var poisoned = GamePlayer.GetPlayer(msg.poisonedId);
            if (poisoned != null && poisoned.Role.Role.InternalName == "sheriff")
                new StaticAchievementToken("toxin.misfire");
            if (GamePlayer.GetPlayer(msg.toxinId)?.Role is Instance inst) inst.MarkSuspect(msg.poisonedId);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>解锁的下毒次数 —— 每杀一个人 +1，上限由配置决定。</summary>
        private int charges = 0;
        private int usedTotal = 0;
        private ModAbilityButton? poisonButton;
        private byte lastSuspect = 255;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && !p.IsImpostor && p.PlayerId != MyPlayer.PlayerId
                     && !p.TryGetModifier<NebulaN.Roles.Modifier.Poisoned.Instance>(out _));
            tracker.SetColor(Cor.impRed);

            poisonButton = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "toxin.poison", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && charges > 0,
                b => !MyPlayer.IsDead, false);
            poisonButton.SetLabelType(ModAbilityButton.LabelType.Impostor);
            poisonButton.ShowUsesIcon(3, charges.ToString());
            poisonButton.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null || charges <= 0) return;
                charges--;
                usedTotal++;
                poisonButton!.UpdateUsesIcon(charges.ToString());
                RpcPoison.Invoke((MyPlayer.PlayerId, target.PlayerId));
                button.StartCoolDown();
            };
        }

        /// <summary>每次击杀解锁一次下毒。</summary>
        [Local]
        void OnKill(PlayerKillPlayerEvent ev)
        {
            if (!AmOwner || ev.Murderer != MyPlayer || ev.Dead == MyPlayer) return;
            if (usedTotal + charges >= MaxPoisonsOption) return;
            charges++;
            poisonButton?.UpdateUsesIcon(charges.ToString());
        }

        public void MarkSuspect(byte id) => lastSuspect = id;

        /// <summary>「太空毒液画作」：被栽赃的人当轮会议被放逐。</summary>
        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (!AmOwner || lastSuspect == 255) return;
            if (ev.Exiled.Any(p => p.PlayerId == lastSuspect))
                new StaticAchievementToken("toxin.masterpiece");
            lastSuspect = 255;
        }
    }
}
