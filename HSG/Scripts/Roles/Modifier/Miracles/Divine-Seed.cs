using Virial;
using Virial.Assignable;
using Virial.Game;

namespace NebulaN.Roles.Modifier;

public class DivineSeed : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private DivineSeed() : base(
        "divineseed",
        "ds",
        Cor.Golden,
        null, false, false, false
    )
    { }
    bool ISpawnable.IsSpawnable => false;
    public static DivineSeed MyRole = new();

    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        public Instance(GamePlayer player) : base(player) { }
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner) AmongUsUtil.PlayQuickFlash(Cor.Golden);
        }
        void DecorateMiracleRelation(PlayerDecorateNameEvent ev)
        {
            if (!AmOwner)  return;
             
            if (!MyPlayer.TryGetModifier<DivineSeed.Instance>(out _))  return;
             
            if (ev.Player.TryGetModifier<Miracle.Instance>(out _))
            {
                ev.Name += " M".Color(Cor.lightYellow.ToUnityColor());
            }
        }
    }
}