using Virial.Events.Role;

namespace NebulaN.Roles.Modifier;

/// <summary>
/// bug 虫（附加职业，由代码员种下）。
/// 宿主的下一次出刀 / 对玩家使用的技能会被判为无效，之后本附加职业自动解除。
///
/// 旧版监听的是 PlayerCheckCanKillLocalEvent —— 那是击杀追踪器「筛选目标」用的，每帧对附近每个人都会问一次，
/// 并不是出刀事件。结果是 bug 虫一靠近任何人就被触发并移除，真正出刀时早已不在，击杀照常生效。
///
/// 现在分三层拦截：
/// 1. 原版击杀键（内鬼刀 / 大部分中立刀）：宿主本机 PlayerTryVanillaKillLocalEvent → Cancel，并吃惩罚冷却。
/// 2. 走 CheckInteraction 的技能（InteractButton 及大量主程序集技能）：宿主本机 PlayerInteractPlayerLocalEvent → IsCanceled。
/// 3. 兜底：直接调用 MurderPlayer 的技能（如警长）不经过上面两个事件，
///    由房主在 PlayerCheckKilledEvent 中把「宿主作为凶手的击杀」判为 Rejected（警长走火自尽同样作废）。
/// 修饰符通过 AddModifier 同步到所有客户端，所以房主侧也有这个实例。
/// </summary>
public class Bugged : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Bugged() : base("bugged", new Virial.Color(0.35f, 0.85f, 0.35f), withConfigurationHolder: false) { }

    public static readonly Bugged MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255), arguments.Get(1, 25));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte coderId;
        private readonly int penalty;
        private bool consumed = false;

        public Instance(GamePlayer player, int coderId, int penalty) : base(player)
        {
            this.coderId = (byte)coderId;
            this.penalty = penalty;
        }

        int[] RuntimeAssignable.RoleArguments => [coderId, penalty];
        void RuntimeAssignable.OnActivated() { }

        // ---- 第 1 层：原版击杀键 ----
        [Local, OnlyMyPlayer]
        void OnTryVanillaKill(PlayerTryVanillaKillLocalEventAbstractPlayerEvent ev)
        {
            if (consumed) return;
            ev.Cancel(false);
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.SetCooldown(penalty);
            Consume();
        }

        // ---- 第 2 层：对玩家使用的技能 ----
        [Local, OnlyMyPlayer]
        void OnInteract(PlayerInteractPlayerLocalEvent ev)
        {
            if (consumed || ev.IsCanceled) return;
            ev.IsCanceled = true; // 按钮自身会按 ResetCooldownEvenIfFailed 进入冷却
            Consume();
        }

        // ---- 第 3 层：房主兜底 ----
        [OnlyHost]
        void OnCheckKilled(PlayerCheckKilledEvent ev)
        {
            if (consumed) return;
            if (ev.Killer != MyPlayer) return;
            if (ev.Result != KillResult.Kill) return;

            bool isSelf = ev.Player == MyPlayer;
            // 自己死的情况里，只拦警长走火这类「技能失败的代价」，其它自尽（被动效果等）照常
            if (isSelf && ev.PlayerState != PlayerState.Misfired) return;

            ev.Result = KillResult.Rejected;
            Consume();
        }

        private void Consume()
        {
            if (consumed) return;
            consumed = true;
            if (MyPlayer.AmOwner) AmongUsUtil.PlayQuickFlash(new Virial.Color(0.35f, 0.85f, 0.35f));
            NebulaN.Roles.Impostor.Coder.RpcBugTriggered.Invoke(coderId);
            MyPlayer.RemoveModifier(MyRole);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            if (canSeeAllInfo || inEndScene || (local != null && local.PlayerId == coderId))
                name += " ␣".Color(new Color(0.35f, 0.85f, 0.35f));
        }
    }
}
