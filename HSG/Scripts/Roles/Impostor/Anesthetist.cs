namespace NebulaN.Roles.Impostor;

/// <summary>
/// 麻醉师（内鬼）。
/// 「击杀替换为麻醉，自由阶段使用。对一名玩家使用后，该玩家将在当轮会议过后移动上下颠倒、
/// 左右颠倒，报告后死亡。麻醉冷却必定在每次出刀后 +2.5 秒。」
/// </summary>
public class Anesthetist : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration BaseCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.anesthetist.cooldown", (10f, 30f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration CooldownGrowthOption = NebulaAPI.Configurations.Configuration(
        "options.role.anesthetist.growth", (0f, 10f, 0.5f), 2.5f, FloatConfigurationDecorator.Second);

    private Anesthetist() : base(
        "anesthetist", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [BaseCooldownOption, CooldownGrowthOption])
    { }

    public static readonly Anesthetist MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte anesthetistId, byte targetId)> RpcAnesthetize =
        new("HSG.Anesthetist.Anesthetize", (msg, _) =>
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (target?.AmOwner != true) return;
            target.AddModifierOnce<NebulaN.Roles.Modifier.Anesthetized.Instance>(
                NebulaN.Roles.Modifier.Anesthetized.MyRole, [msg.anesthetistId]);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator, IPlayerAbility
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>麻醉取代原版击杀。</summary>
        bool RuntimeRole.HasVanillaKillButton => false;
        bool IPlayerAbility.HideKillButton => true;

        private float currentCooldown;
        private int totalUses = 0;

        void RuntimeAssignable.OnActivated()
        {
            currentCooldown = BaseCooldownOption;
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.KillTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            tracker.SetColor(Cor.impRed);

            var shot = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, true, false, VirtualKeyInput.Kill, null,
                currentCooldown, "anesthetist.shot", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown,
                b => !MyPlayer.IsDead, false);
            shot.SetLabelType(ModAbilityButton.LabelType.Impostor);
            shot.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;

                RpcAnesthetize.Invoke((MyPlayer.PlayerId, target.PlayerId));
                totalUses++;
                if (totalUses == 1) new StaticAchievementToken("anesthetist.dizzy");
                if (totalUses >= 100) new StaticAchievementToken("anesthetist.numb");

                // 每用一次，冷却固定变长
                currentCooldown += CooldownGrowthOption;
                button.CoolDownTimer = NebulaAPI.Modules.Timer(this, currentCooldown).SetAsKillCoolTimer().Start();
            };
        }
    }
}
