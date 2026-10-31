using Virial.Events.Role;

namespace NebulaN.Roles.Modifier;

/// <summary>
/// 中毒（附加职业，由毒血杀手下毒）。
///
/// 效果：被下毒者接下来对某位玩家使用的一次技能，会变成一刀 —— 目标死亡，凶手记在被下毒者头上。
/// 用掉一次后自动解除。
///
/// 旧版只在 PlayerCheckCanKillLocalEvent 里 SetAsCanKillForcedly —— 那个事件只影响击杀追踪器能选中谁，
/// 对没有刀的船员毫无作用，警长也不会因此打死船员。现在改为：
/// - 走 CheckInteraction 的技能（PlayerInteractPlayerLocalEvent）：非击杀类技能 → 取消原技能，改为由自己击杀目标；
///   击杀类技能 → 照常放行。
/// - 警长：主程序集的 SheriffCheckKillEvent 把 CanKill 强制设为 true，于是执法船员也会打死对方，而不是走火。
/// - 原版击杀键本来就是击杀，放行即可。
/// 以上任一击杀发生后（PlayerKillPlayerEvent）解除中毒。
///
/// 表格里「若技能不需要对目标使用，则使用者立刻暴毙」这一支没有实现：
/// 无目标技能没有统一事件，只能像给予者那样逐个包装按钮，暂未做。
/// </summary>
public class Poisoned : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Poisoned() : base("poisoned", new Virial.Color(0.4f, 0.8f, 0.3f), withConfigurationHolder: false) { }

    public static readonly Poisoned MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte toxinId;
        public byte ToxinId => toxinId;

        public Instance(GamePlayer player, int toxinId) : base(player) { this.toxinId = (byte)toxinId; }

        int[] RuntimeAssignable.RoleArguments => [toxinId];
        void RuntimeAssignable.OnActivated() { }

        private bool converting = false;

        /// <summary>对玩家使用的非击杀技能 → 改成一刀。</summary>
        [Local, OnlyMyPlayer]
        void OnInteract(PlayerInteractPlayerLocalEvent ev)
        {
            if (ev.IsCanceled || converting) return;
            if (ev.Parameters.IsKillInteraction) return; // 本来就是击杀，放行
            var target = ev.Target?.RealPlayer;
            if (target == null || target.IsDead || target == MyPlayer) return;

            ev.IsCanceled = true;
            converting = true;
            MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
            // 若这一刀被护盾等挡下，毒不应就此卡死：稍后允许再次触发
            NebulaManager.Instance.StartDelayAction(2f, () => converting = false);
        }

        /// <summary>警长：无论对象是谁，这一枪都算执法成功。</summary>
        [Local, OnlyMyPlayer]
        void OnSheriffCheck(SheriffCheckKillEvent ev) => ev.CanKill = true;

        /// <summary>毒发完成 —— 解除中毒，并回报毒血杀手。</summary>
        [Local]
        void OnKill(PlayerKillPlayerEvent ev)
        {
            if (!AmOwner || ev.Murderer != MyPlayer) return;
            NebulaN.Roles.Impostor.Toxin.RpcPoisonWorked.Invoke((toxinId, MyPlayer.PlayerId, ev.Dead.PlayerId));
            MyPlayer.RemoveModifier(MyRole);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            if (canSeeAllInfo || inEndScene || (local != null && local.PlayerId == toxinId))
                name += " ☠".Color(new Color(0.4f, 0.8f, 0.3f));
        }
    }
}
