using NebulaN.Roles.Impostor;

namespace NebulaN.Roles.Modifier;

public class ObjectorPublicRole : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private ObjectorPublicRole() : base(
        "ObjectorPublicRole",
        "OPR",
        Cor.Golden,
        null, false, false, false
    )
    { }
    bool ISpawnable.IsSpawnable => false;
    public static ObjectorPublicRole MyRole = new();
    bool DefinedAssignable.ShowOnHelpScreen => false;
    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        public Instance(GamePlayer player) : base(player) { }
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        bool RuntimeAssignable.CanBeAwareAssignment => false;

        void RuntimeAssignable.OnActivated()
        {
            SayHello();
            // 你好。
        }
        int SayHello()
        {
            return 0;
        }
        [Local]
        void ShowRole(PlayerSetFakeRoleNameEvent ev)
        {
            if (Objector.ObjectorConsequence.GetValue() != 0) return;
            var objector = MyPlayer.Role as Objector.Instance;
            if (objector == null || !objector._NeedShowRole) return;
            ev.Alternate(MyPlayer.Role.Role.DisplayColoredName);
        }
    }
}