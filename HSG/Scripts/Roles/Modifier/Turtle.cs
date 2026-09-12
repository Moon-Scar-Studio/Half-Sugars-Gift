namespace NebulaN.Roles.Modifier;
public class Turtle : DefinedAllocatableModifierTemplate,DefinedAllocatableModifier, RuntimeAssignableGenerator<RuntimeModifier>, HasCitation

{
    Turtle() : base(
        "turtle",
        "tt",
        new Virial.Color(0.8f, 0.2f, 0.2f),
        new IConfiguration[] {  },
        allocateToCrewmate: true,
        allocateToImpostor: true,
        allocateToNeutral: true
    )
    { }

    public static Turtle MyRole = new();

    public Citation Citation => Citations.hvtXsvc_hsg;

    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        const string SpeedTag = "Turtle";

        public Instance(GamePlayer player) : base(player) { }

        DefinedModifier RuntimeModifier.Modifier => (DefinedModifier)MyRole;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            MyPlayer.GainSpeedAttribute(0.75f, float.MaxValue, true, 50, SpeedTag); // 必须带 tag，OnReleased 才能按 tag 移除
        }

        void IGameOperator.OnReleased()=>MyPlayer.RemoveAttributeByTag(SpeedTag);
    }
}