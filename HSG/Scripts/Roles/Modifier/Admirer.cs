namespace NebulaN.Roles.Modifier;

/// <summary>
/// 粉丝（附加职业，由画师赠画后在剩余指定人数时转化）。
/// 与画师站在同一战线，跟随画师获胜。
/// </summary>
public class Admirer : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Admirer() : base("admirer", HalfSugarGift.Core.Patch.ExTeam.ArtistTeam.Color, withConfigurationHolder: false) { }

    public static readonly Admirer MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte artistId;
        public byte ArtistId => artistId;

        public Instance(GamePlayer player, int artistId) : base(player) { this.artistId = (byte)artistId; }

        int[] RuntimeAssignable.RoleArguments => [artistId];
        void RuntimeAssignable.OnActivated() { }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.ArtistWin;

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.ArtistWin && !MyPlayer.IsDead);

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            bool visible = AmOwner || canSeeAllInfo || inEndScene
                || (local != null && (local.PlayerId == artistId || local.TryGetModifier<Instance>(out _)));
            if (visible) name += " ✎".Color(HalfSugarGift.Core.Patch.ExTeam.ArtistTeam.Color.ToUnityColor());
        }
    }
}
