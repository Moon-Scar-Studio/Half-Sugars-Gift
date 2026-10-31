namespace NebulaN.Roles.Modifier;

/// <summary>
/// 宝石之盾（附加职业，由翡翠授予）。
/// 一次性护盾，持续时间有限；挡下一次击杀后立刻消失。
/// 授予者（翡翠）看不到碎盾效果 —— 碎盾闪光只发给被保护的人。
/// </summary>
public class GemShield : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private GemShield() : base("gemShield", new Virial.Color(0.3f, 0.9f, 0.6f), withConfigurationHolder: false) { }

    public static readonly GemShield MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 30));

    /// <summary>翡翠本人需要知道「有人碎盾了」来拿成就，但不播放碎盾特效。</summary>
    static internal readonly RemoteProcess<byte> RpcShieldBroken = new("HSG.GemShield.Broken", (jadeId, _) =>
    {
        if (GamePlayer.GetPlayer(jadeId)?.Role is NebulaN.Roles.Crewmate.Jade.Instance jade)
            jade.OnShieldBroken();
    });

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private float timeLeft;
        private bool consumed = false;

        public Instance(GamePlayer player, int duration) : base(player) { timeLeft = duration; }

        int[] RuntimeAssignable.RoleArguments => [(int)timeLeft];
        void RuntimeAssignable.OnActivated() { }

        [OnlyMyPlayer, EventPriority(EventPriority.High)]
        void OnCheckKilled(PlayerCheckKilledEvent ev)
        {
            if (consumed) return;
            if (ev.Result != KillResult.Kill) return;
            if (ev.Killer == MyPlayer) return; // 自杀 / 技能反噬不属于「被攻击」
            consumed = true;
            ev.Result = KillResult.ObviousGuard;
            // 通知翡翠：盾成功挡了一次
            NebulaN.Roles.Crewmate.Jade.ReportBroken();
            // PlayerCheckKilledEvent 只在房主触发，原来的 AmOwner 判断会让非房主的盾永远不被移除
            MyPlayer.RemoveModifier(MyRole);
        }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner) return;
            timeLeft -= ev.DeltaTime;
            if (timeLeft <= 0f) MyPlayer.RemoveModifier(MyRole);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            // 翡翠自己看不到盾的状态，所以只给持盾者本人显示
            if (AmOwner && !inEndScene) name += " ◇".Color(new Color(0.3f, 0.9f, 0.6f));
        }
    }
}
