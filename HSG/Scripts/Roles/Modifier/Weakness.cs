using HalfSugarGift.Roles.Impostor;
namespace HalfSugarGift.Roles.Modifier;
public class Weakness : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier,
    RuntimeAssignableGenerator<RuntimeModifier>, HasCitation
{
    Weakness() : base(
        "weakness", "WK", new Virial.Color(0.5f, 0.8f, 1f),
        []
    )
    { }
    public Citation Citation => Citations.hvtXsvc_hsg;
    public static readonly Weakness MyRole = new();
    bool ISpawnable.IsSpawnable => false;
    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        public Instance(GamePlayer player) : base(player) { }
        bool RuntimeAssignable.CanBeAwareAssignment => false;
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        int _remainingRounds;

        void RuntimeAssignable.OnActivated()
        {
            _remainingRounds = Mage.WeaknessExpiryRounds;
        }
        [Local]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent ev)
        {
            if (ev.Player != MyPlayer || !AmOwner || MyPlayer.IsDead) return;
            MyPlayer.Suicide(Mage.WeaknessState, EventDetail.Kill, KillParameter.NormalKill);
        }
        [Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (!AmOwner || MyPlayer.IsDead) return;
            if (ev.Murderer != MyPlayer) return;
            if (ev.Murderer == ev.Dead) return;
            NebulaManager.Instance.StartDelayAction(0.1f, () =>
            {
                if (!MyPlayer.IsDead)
                    MyPlayer.Suicide(Mage.WeaknessState, EventDetail.Kill, KillParameter.NormalKill);
            });
        }
        [OnlyMyPlayer]
        [Local]
        void OnVentEnter(PlayerVentEnterEvent ev)
        {
            if (!AmOwner || MyPlayer.IsDead) return;
            MyPlayer.Suicide(Mage.WeaknessState, EventDetail.Kill, KillParameter.NormalKill);
        }
        [Local]
        void OnPlayerDie(PlayerDieEvent ev)
        {
            if (!AmOwner || ev.Player != MyPlayer) return;
            MyPlayer.RemoveModifier(MyRole);
        }
        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (!AmOwner || MyPlayer.IsDead) return;
            if (!Mage.WeaknessEnableRoundExpiry) return;
            _remainingRounds--;
            if (_remainingRounds <= 0)
                MyPlayer.RemoveModifier(MyRole);
        }
    }
}
[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
internal static class WeaknessPatch
{
    static Harmony? harmony;

    /// <summary>
    /// 已知风险：直接补丁 ModAbilityButtonImpl.DoClick / DoSubClick 在其他 addon 改变预处理顺序时
    /// 曾有 RuntimeDetour 硬崩溃的案例（见 FairyStarFlight 源码注释）。Nebula 没有通用的"按钮点击"事件，
    /// 暂时保留此实现，但把补丁包在 try/catch 里，失败时只是"弱点"对技能按钮不生效，不影响 addon 其余部分。
    /// </summary>
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        try
        {
            harmony = new Harmony("HSG.Weakness");

            var doClick = typeof(ModAbilityButtonImpl).GetMethod("DoClick");
            if (doClick != null)
                harmony.Patch(doClick, prefix: new HarmonyMethod(typeof(WeaknessPatch).GetMethod(nameof(BeforeClick))));

            var doSubClick = typeof(ModAbilityButtonImpl).GetMethod("DoSubClick");
            if (doSubClick != null)
                harmony.Patch(doSubClick, prefix: new HarmonyMethod(typeof(WeaknessPatch).GetMethod(nameof(BeforeSubClick))));
        }
        catch (Exception e)
        {
            HsgDebug.LogException("[Weakness] 按钮补丁应用失败，弱点将只对任务/击杀/通风生效", e);
        }
    }
    public static bool BeforeClick(ModAbilityButtonImpl __instance)
    {
        return TrySuicideWeakness();
    }

    public static bool BeforeSubClick(ModAbilityButtonImpl __instance)
    {
        return TrySuicideWeakness();
    }

    static bool TrySuicideWeakness()
    {
        var player = GamePlayer.LocalPlayer;
        if (player == null || player.IsDead) return true;
        if (!player.Modifiers.Any(m => m.Modifier == Weakness.MyRole)) return true;
        player.Suicide(Mage.WeaknessState, EventDetail.Kill, KillParameter.NormalKill);
        return false;
    }
}
