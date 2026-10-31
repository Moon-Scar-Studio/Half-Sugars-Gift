namespace NebulaN.Roles.Modifier;

/// <summary>
/// 奋战（附加）。
/// 表格描述：「具有奋战的玩家可以在一次 cd 结束后使用两次技能或击杀」。
///
/// 实现：每次冷却重置时交替 —— 第一次正常走冷却，紧接着的那次直接归零，
/// 于是「一轮冷却 = 两次出手」。与闪击（会议后一次归零）互不冲突。
/// </summary>
public class Fervor : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier,
    RuntimeAssignableGenerator<RuntimeModifier>, HasCitation
{
    private Fervor() : base(
        "fervor", "!", new Virial.Color(1f, 0.25f, 0.25f),
        [], true, true, true)
    { }

    public Citation Citation => Citations.hvtXsvc_hsg;
    public static readonly Fervor MyRole = new();

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>true = 下一次「出刀导致的」冷却重置直接归零，即这一轮冷却里的第二刀。</summary>
        private bool freeNext = true;
        /// <summary>只有出刀引起的冷却重置才计数（会议后、开局等重置不算）。</summary>
        private bool killAttempted = false;

        [Local, OnlyMyPlayer]
        void OnTryKill(PlayerTryVanillaKillLocalEventAbstractPlayerEvent ev) => killAttempted = true;

        void RuntimeAssignable.OnActivated() { }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev) => freeNext = true;

        [Local]
        void ResetKillCD(ResetKillCooldownLocalEvent ev)
        {
            if (!AmOwner || !killAttempted) return;
            killAttempted = false;
            if (freeNext)
            {
                ev.SetFixedCooldown(0.1f);
                freeNext = false;
            }
            else
            {
                freeNext = true;
            }
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo || inEndScene)
                name += " !".Color(new Color(1f, 0.25f, 0.25f));
        }
    }
}
