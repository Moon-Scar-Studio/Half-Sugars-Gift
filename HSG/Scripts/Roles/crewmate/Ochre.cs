namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 赭石（船员）。
///
/// 表格描述：「技能为点染，被点染的玩家可以看到随机一个同阵营玩家（可以包括赭石自己），
/// 他们将成为『挚友』，挚友可以看到彼此但看不到赭石，赭石可以看到所有挚友。
/// 所有中立视为共边。如果阵营变动，那么点染失效。」
/// </summary>
public class Ochre : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration MaxUsesOption = NebulaAPI.Configurations.Configuration(
        "options.role.ochre.maxUses", (1, 5), 2);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.ochre.cooldown", (10f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);

    private Ochre() : base(
        "ochre", new Virial.Color(0.72f, 0.42f, 0.2f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [MaxUsesOption, CooldownOption])
    { }

    public static readonly Ochre MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>本局所有的挚友配对（a↔b）。赭石看得到全部，挚友只看得到自己那一对。</summary>
    static internal readonly List<(byte a, byte b, byte ochreId)> Bonds = new();

    static internal readonly RemoteProcess<(byte ochreId, byte a, byte b)> RpcBond =
        new("HSG.Ochre.Bond", (msg, _) =>
        {
            Bonds.Add((msg.a, msg.b, msg.ochreId));
            var local = GamePlayer.LocalPlayer;
            if (local != null && (local.PlayerId == msg.a || local.PlayerId == msg.b))
                AmongUsUtil.PlayQuickFlash(MyRole.RoleColor);
        });

    /// <summary>判定两人是否算「同阵营」。所有中立互相视为共边。</summary>
    static internal bool SameSide(GamePlayer x, GamePlayer y)
    {
        if (x.IsImpostor && y.IsImpostor) return true;
        bool xn = x.Role.Role.Category == RoleCategory.NeutralRole;
        bool yn = y.Role.Role.Category == RoleCategory.NeutralRole;
        if (xn && yn) return true;
        if (xn || yn) return false;
        if (x.IsImpostor || y.IsImpostor) return false;
        return true; // 都是船员
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int usesLeft;

        void RuntimeAssignable.OnActivated()
        {
            usesLeft = MaxUsesOption;
            Bonds.Clear();
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            tracker.SetColor(MyRole.RoleColor);

            var dye = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "ochre.dye", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && usesLeft > 0,
                b => !MyPlayer.IsDead && usesLeft > 0, false);
            dye.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            dye.ShowUsesIcon(3, usesLeft.ToString());
            dye.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;

                // 随机挑一个与目标同阵营的人（可以是赭石自己）
                var friend = GamePlayer.AllPlayers
                    .Where(p => !p.IsDead && p.PlayerId != target.PlayerId && SameSide(target, p))
                    .PickRandom();
                if (friend == null) return;

                usesLeft--;
                dye.UpdateUsesIcon(usesLeft.ToString());
                RpcBond.Invoke((MyPlayer.PlayerId, target.PlayerId, friend.PlayerId));
                new StaticAchievementToken("ochre.freehand");
                button.StartCoolDown();
            };
        }

        /// <summary>
        /// 名字上的挚友标记：
        /// · 赭石看得到所有挚友；
        /// · 挚友只看得到自己那一对的另一半，看不到赭石的身份。
        /// </summary>
        void OnDecorateName(PlayerDecorateNameEvent ev)
        {
            var local = GamePlayer.LocalPlayer;
            if (local == null) return;

            bool mark = false;
            if (local.Role is Instance)
            {
                mark = Bonds.Any(b => b.ochreId == local.PlayerId
                                      && (b.a == ev.Player.PlayerId || b.b == ev.Player.PlayerId));
            }
            else
            {
                mark = Bonds.Any(b =>
                    (b.a == local.PlayerId && b.b == ev.Player.PlayerId) ||
                    (b.b == local.PlayerId && b.a == ev.Player.PlayerId));
            }

            if (mark) ev.Name += " ❖".Color(MyRole.RoleColor.ToUnityColor());
        }

        /// <summary>阵营变动 → 该条点染失效。</summary>
        void OnRoleSet(PlayerRoleSetEvent ev)
        {
            Bonds.RemoveAll(b =>
            {
                var a = GamePlayer.GetPlayer(b.a);
                var c = GamePlayer.GetPlayer(b.b);
                return a == null || c == null || !SameSide(a, c);
            });
        }
    }
}
