namespace NebulaN.Roles.Modifier;

/// <summary>
/// 被救援者（附加职业，由救命恩人复活获得）。
/// · 跟随救命恩人获胜。
/// · 救命恩人死亡时一起暴毙。
/// · 场上有人死亡时会收到提示，死亡位置在地图上被标记（仅救命恩人与被救援者可见）。
/// </summary>
public class Rescued : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Rescued() : base("rescued", new Virial.Color(0.4f, 1f, 0.75f), withConfigurationHolder: false) { }

    public static readonly Rescued MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte saviorId;
        public byte SaviorId => saviorId;

        public Instance(GamePlayer player, int saviorId) : base(player) { this.saviorId = (byte)saviorId; }

        int[] RuntimeAssignable.RoleArguments => [saviorId];

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner) new StaticAchievementToken("savior.rescued");
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.SaviorWin;

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.SaviorWin && !MyPlayer.IsDead);

        /// <summary>救命恩人死亡 → 被救援者一起暴毙。</summary>
        [OnlyHost]
        void OnPlayerDie(PlayerDieEvent ev)
        {
            if (ev.Player.PlayerId != saviorId || MyPlayer.IsDead) return;
            Neutral.Savior.RpcEntangle.Invoke(MyPlayer.PlayerId);
        }

        /// <summary>有人死亡 → 屏幕闪光提示。</summary>
        [Local]
        void OnAnyoneDie(PlayerDieEvent ev)
        {
            if (!AmOwner || ev.Player == MyPlayer) return;
            AmongUsUtil.PlayQuickFlash(HalfSugarGift.Core.Patch.ExTeam.SaviorTeam.Color);
        }

        /// <summary>
        /// 救命恩人打的职业标记，被救援者同样看得到。
        /// 显示逻辑与救命恩人那边共用 <see cref="Neutral.Savior.GetVisibleMarkText"/>，
        /// 可见性由 CanSeeMarksOf 统一把关。
        /// </summary>
        [Local]
        void ReflectMark(PlayerSetFakeRoleNameEvent ev)
        {
            if (!AmOwner) return;
            var mark = Neutral.Savior.GetVisibleMarkText(ev.Player.PlayerId);
            if (mark != null) ev.Append(mark);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            bool visible = AmOwner || canSeeAllInfo || inEndScene
                || (local != null && (local.PlayerId == saviorId || local.TryGetModifier<Instance>(out _)));
            if (visible) name += " ✚".Color(new Color(0.4f, 1f, 0.75f));
        }
    }
}
