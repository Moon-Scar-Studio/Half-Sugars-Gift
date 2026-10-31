namespace NebulaN.Roles.Modifier;

/// <summary>
/// 被加冕者（附加职业，由国王加冕）。
/// · 会议上多一票（通过 MeetingFixVoteHostEvent 把票权重改成 2）。
/// · 国王死亡时，由被加冕者替国王去死。
/// </summary>
public class Crowned : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Crowned() : base("crowned", new Virial.Color(0.9f, 0.8f, 0.2f), withConfigurationHolder: false) { }

    public static readonly Crowned MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte kingId;
        public byte KingId => kingId;

        public Instance(GamePlayer player, int kingId) : base(player) { this.kingId = (byte)kingId; }

        int[] RuntimeAssignable.RoleArguments => [kingId];
        void RuntimeAssignable.OnActivated() { }

        /// <summary>票权重 ×2。主机侧在集计前统一改写。</summary>
        [OnlyHost]
        void OnFixVote(MeetingFixVoteHostEvent ev)
        {
            if (MyPlayer.IsDead) return;
            ev.SetVote(MyPlayer, ev.GetVote(MyPlayer) + 1);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo || inEndScene)
                name += " ♛".Color(new Color(0.9f, 0.8f, 0.2f));
        }
    }
}
