namespace NebulaN.Roles.Modifier;

/// <summary>
/// 麻醉（附加职业，由麻醉师施加）。
/// 会议结束后开始发作：移动上下左右颠倒；一旦报告尸体或召开紧急会议就会死亡。
/// </summary>
public class Anesthetized : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Anesthetized() : base("anesthetized", new Virial.Color(0.5f, 0.75f, 0.85f), withConfigurationHolder: false) { }

    public static readonly Anesthetized MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte sourceId;
        /// <summary>麻醉是「会议之后」才发作的。</summary>
        private bool active = false;

        public Instance(GamePlayer player, int sourceId) : base(player) { this.sourceId = (byte)sourceId; }

        int[] RuntimeAssignable.RoleArguments => [sourceId];
        void RuntimeAssignable.OnActivated() { }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev) => active = true;

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner || !active || MyPlayer.IsDead) return;
            // 属性是每帧续期语义
            MyPlayer.GainAttribute(PlayerAttributes.FlipXY, 0.25f, false, 0, "anesthetized:flip");
        }

        /// <summary>报告 / 召集会议 → 药效发作致死。</summary>
        [Local]
        void OnReport(ReportDeadBodyEvent ev)
        {
            if (!AmOwner || !active || MyPlayer.IsDead) return;
            if (ev.Reporter != MyPlayer) return;
            MyPlayer.Suicide(HalfSugarGift.Core.Patch.ExState.Anesthetized, EventDetail.Kill, KillParameter.NormalKill);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            if (canSeeAllInfo || inEndScene || (local != null && local.PlayerId == sourceId))
                name += " ⊗".Color(new Color(0.5f, 0.75f, 0.85f));
        }
    }
}
