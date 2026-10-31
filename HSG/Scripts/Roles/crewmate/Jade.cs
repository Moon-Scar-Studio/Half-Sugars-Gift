namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 翡翠（船员）。
/// 技能为「宝石之盾」：给在场所有人上一个持续时间有限的一次性护盾，
/// 但自己看不到碎盾效果 —— 翡翠只知道「盾生效过」，不知道是谁被保护了。
/// </summary>
public class Jade : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration MaxUsesOption = NebulaAPI.Configurations.Configuration(
        "options.role.jade.maxUses", (1, 5), 1);
    static private readonly FloatConfiguration DurationOption = NebulaAPI.Configurations.Configuration(
        "options.role.jade.duration", (5f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.jade.cooldown", (10f, 90f, 2.5f), 40f, FloatConfigurationDecorator.Second);

    private Jade() : base(
        "jade", new Virial.Color(0.3f, 0.9f, 0.6f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [MaxUsesOption, DurationOption, CooldownOption])
    { }

    public static readonly Jade MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>给全场发盾。每个客户端只给自己挂修饰符，避免重复广播。</summary>
    static internal readonly RemoteProcess<(byte jadeId, int duration)> RpcCastShield =
        new("HSG.Jade.CastShield", (msg, _) =>
        {
            var local = GamePlayer.LocalPlayer;
            if (local == null || local.IsDead) return;
            local.AddModifierOnce<NebulaN.Roles.Modifier.GemShield.Instance>(
                NebulaN.Roles.Modifier.GemShield.MyRole, [msg.duration]);
        });

    /// <summary>某个人的盾被打爆了，通知所有在场的翡翠拿成就（不播特效）。</summary>
    /// <summary>
    /// 碎盾判定只在房主执行（PlayerCheckKilledEvent），所以必须广播，
    /// 否则只有「房主恰好是翡翠」时才拿得到成就。
    /// </summary>
    static internal void ReportBroken() => RpcShieldBroken.Invoke();

    static internal readonly RemoteProcess RpcShieldBroken = new("HSG.Jade.ShieldBroken", _ =>
    {
        foreach (var p in GamePlayer.AllPlayers)
            if (p.Role is Instance inst && p.AmOwner) inst.OnShieldBroken();
    });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int usesLeft;

        void RuntimeAssignable.OnActivated()
        {
            usesLeft = MaxUsesOption;
            if (!AmOwner) return;

            var cast = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "jade.shield", null,
                b => !b.IsInCooldown && usesLeft > 0,
                b => !MyPlayer.IsDead && usesLeft > 0, false);
            cast.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            cast.ShowUsesIcon(3, usesLeft.ToString());
            cast.OnClick = button =>
            {
                usesLeft--;
                cast.UpdateUsesIcon(usesLeft.ToString());
                RpcCastShield.Invoke((MyPlayer.PlayerId, (int)(float)DurationOption));
                button.StartCoolDown();
            };
        }

        public void OnShieldBroken()
        {
            if (AmOwner) new StaticAchievementToken("jade.shatter");
        }
    }
}
