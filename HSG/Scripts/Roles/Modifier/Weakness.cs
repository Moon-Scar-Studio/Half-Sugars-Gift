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

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        harmony = new Harmony("hsg.weakness.detect");

        var doClick = typeof(ModAbilityButtonImpl).GetMethod("DoClick");
        if (doClick != null)
            harmony.Patch(doClick, prefix: new HarmonyMethod(typeof(WeaknessPatch).GetMethod(nameof(BeforeClick))));

        var doSubClick = typeof(ModAbilityButtonImpl).GetMethod("DoSubClick");
        if (doSubClick != null)
            harmony.Patch(doSubClick, prefix: new HarmonyMethod(typeof(WeaknessPatch).GetMethod(nameof(BeforeSubClick))));
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
