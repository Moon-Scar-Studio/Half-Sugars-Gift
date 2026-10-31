namespace NebulaN.Roles.Modifier;

/// <summary>
/// 中砂者（附加职业，由朱砂点砂）。
/// · 与朱砂构成单向链子：中砂者能看到自己和朱砂之间的爱心，朱砂能看到所有中砂者。
/// · 朱砂可以随时按死中砂者；朱砂死亡时所有中砂者一并毒发身亡。
/// · 活着的中砂者可以跟随朱砂获胜，但人数上仍然算船员。
/// </summary>
public class Cinnabarred : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Cinnabarred() : base("cinnabarred", new Virial.Color(0.85f, 0.18f, 0.2f), withConfigurationHolder: false) { }

    public static readonly Cinnabarred MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte cinnabarId;
        public byte CinnabarId => cinnabarId;

        public Instance(GamePlayer player, int cinnabarId) : base(player) { this.cinnabarId = (byte)cinnabarId; }

        int[] RuntimeAssignable.RoleArguments => [cinnabarId];
        void RuntimeAssignable.OnActivated() { }

        /// <summary>跟随朱砂（内鬼）获胜；原本阵营的胜利也保留，所以不 Block。</summary>
        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev)
        {
            if (ev.GameEnd != NebulaGameEnd.ImpostorWin) return;
            var cinnabar = GamePlayer.GetPlayer(cinnabarId);
            ev.SetWinIf(!MyPlayer.IsDead && cinnabar != null);
        }

        /// <summary>朱砂死亡 → 全体中砂者毒发。</summary>
        [OnlyHost]
        void OnPlayerDie(PlayerDieEvent ev)
        {
            if (ev.Player.PlayerId != cinnabarId) return;
            if (MyPlayer.IsDead) return;
            Impostor.Cinnabar.RpcPoison.Invoke(MyPlayer.PlayerId);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            bool visible = AmOwner || canSeeAllInfo || inEndScene || (local != null && local.PlayerId == cinnabarId);
            if (visible) name += " ♡".Color(new Color(0.85f, 0.18f, 0.2f));
        }
    }
}
