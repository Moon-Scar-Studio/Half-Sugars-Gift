using NebulaN.Roles.Crewmate;
using Virial;
using Virial.Assignable;
using Virial.Game;

namespace NebulaN.Roles.Modifier;

public class PublicMiraclist : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private PublicMiraclist() : base(
        "publicmiraclist",
        "PM",
        Cor.Golden,
        null,false,false,false
    )
    { }
    bool DefinedAssignable.ShowOnHelpScreen => false;
    bool ISpawnable.IsSpawnable => false;
    public static PublicMiraclist MyRole = new();
    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        public Instance(GamePlayer player) : base(player) { }
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        bool RuntimeAssignable.CanBeAwareAssignment => false;

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                PatchManager.RpcFlashAll.Invoke(ColorHelper.ColorToHexRGB(Cor.Golden.ToUnityColor()));
            }
        }
        void ReflectRoleName(PlayerSetFakeRoleNameEvent ev)
        {
            if (ev.Player != MyPlayer) return;
            if (MyPlayer.Role.Role == Miraclist.MyRole)
                ev.Alternate(MyPlayer.Role.Role.DisplayColoredName);
            
        }
        void CantBeGuess(PlayerCanGuessPlayerLocalEvent ev)
        {
            if (Crewmate.Miraclist.CanBeGuessedAfterPublicRole) return;
            ev.CanGuess = false;
        }
    }
}