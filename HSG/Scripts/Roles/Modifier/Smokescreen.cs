namespace NebulaN.Roles.Modifier;

/// <summary>
/// 烟幕（附加）。
/// 表格描述：「具有烟幕的玩家在会议中无法被技能影响（不怕赌不怕审不怕大神官特殊会议）」。
///
/// 实现分两路：
/// · 赌杀 → <see cref="PlayerCanGuessPlayerLocalEvent"/> 直接把自己从可赌名单里摘掉；
/// · 其它会议内技能击杀 → <see cref="PlayerCheckKilledEvent"/> 里凡是 IsMeetingKill 一律挡下。
/// 会议里的「标记 / 测谎」一类非击杀技能，由各角色自行调用 <see cref="IsProtected"/> 判断。
/// </summary>
public class Smokescreen : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier,
    RuntimeAssignableGenerator<RuntimeModifier>, HasCitation
{
    private Smokescreen() : base(
        "smokescreen", "~", new Virial.Color(0.6f, 0.6f, 0.65f),
        [], true, true, true)
    { }

    public Citation Citation => Citations.hvtXsvc_hsg;
    public static readonly Smokescreen MyRole = new();

    /// <summary>供其它角色查询：这个人在会议里是否处于烟幕保护中。</summary>
    public static bool IsProtected(GamePlayer? player)
        => player != null && player.TryGetModifier<Instance>(out _);

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        void RuntimeAssignable.OnActivated() { }

        // 该事件在「猜测者」本机触发（事件的 Player 是被猜目标）。
        // 原来的 [Local] 只在烟幕持有者本机执行，猜测者那边永远看不到拦截。
        [OnlyMyPlayer]
        void OnCanGuess(PlayerCanGuessPlayerLocalEvent ev) => ev.CanGuess = false;

        [OnlyMyPlayer, EventPriority(EventPriority.VeryHigh)]
        void OnCheckKilled(PlayerCheckKilledEvent ev)
        {
            if (!ev.IsMeetingKill) return;
            // IsMeetingKill 对「不留尸体」的击杀也为 true，这里只保护真正处在会议 / 放逐阶段的时候
            if (!MeetingHud.Instance && !ExileController.Instance) return;
            ev.Result = KillResult.Guard;
            if (AmOwner) new StaticAchievementToken("smokescreen.block");
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo || inEndScene)
                name += " ~".Color(new Color(0.6f, 0.6f, 0.65f));
        }
    }
}
