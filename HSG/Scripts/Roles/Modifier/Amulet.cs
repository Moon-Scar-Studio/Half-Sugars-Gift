using HalfSugarGift.Roles.Neutral;

namespace HalfSugarGift.Roles.Modifier;

public class Amulet : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier,
    RuntimeAssignableGenerator<RuntimeModifier>, HasCitation
{
    private Amulet() : base(
        "amulet", "符", new Virial.Color(0.8f, 0.7f, 0.2f),
        []
    )
    { }

    public Citation Citation => Citations.Hellos497;
    public static readonly Amulet MyRole = new();
    bool ISpawnable.IsSpawnable => false;

    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        public Instance(GamePlayer player) : base(player) { }

        bool RuntimeAssignable.CanBeAwareAssignment => true;
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private bool _killBlockSubscribed;

        private void EnsureKillBlockSubscribed()
        {
            if (_killBlockSubscribed || GameOperatorManager.Instance == null) return;
            _killBlockSubscribed = true;
            GameOperatorManager.Instance.Subscribe<PlayerTryVanillaKillLocalEventAbstractPlayerEvent>(ev =>
            {
                if (ev.Target is not GamePlayer target || target != MyPlayer) return;
                if (MyPlayer.IsDead) return;

                GamePlayer? taoist = null;
                foreach (var p in GamePlayer.AllPlayerlikes)
                {
                    if (p is GamePlayer gp && gp.Role?.Role == Taoist.MyRole && !gp.IsDead)
                    {
                        taoist = gp;
                        break;
                    }
                }
                if (taoist == null) return; // 道士已死，护身符失效

                var killer = ev.Player;
                if (killer == null) return;

                ev.Cancel();
                Taoist.RpcTaoistSacrifice.Invoke((taoist.PlayerId, killer.PlayerId));

                if (MyPlayer.AmOwner)
                {
                    var title = NebulaAPI.CurrentGame?.GetModule<TitleShower>();
                    if (title != null)
                        title.SetText(Language.Translate("role.taoist.amuletSaved"),
                            new Virial.Color(0.8f, 0.7f, 0.2f), 2f, false);
                }
            }, this);
        }

        void RuntimeAssignable.OnActivated()
        {
            EnsureKillBlockSubscribed();

            if (AmOwner)
            {
                var title = NebulaAPI.CurrentGame?.GetModule<TitleShower>();
                if (title != null)
                    title.SetText(Language.Translate("role.taoist.amuletApplied"),
                        new Virial.Color(0.8f, 0.7f, 0.2f), 2f, false);
            }
        }

        [Local]
        void OnPlayerDie(PlayerDieEvent ev)
        {
            if (!AmOwner || ev.Player != MyPlayer) return;
            MyPlayer.RemoveModifier(MyRole);
        }
    }
}
