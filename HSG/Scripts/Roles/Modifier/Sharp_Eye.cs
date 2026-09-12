namespace NebulaN.Roles.Modifier;
public class Sharp_Eye : DefinedAllocatableModifierTemplate,
    DefinedAllocatableModifier, 
    RuntimeAssignableGenerator<RuntimeModifier>, 
    HasCitation
{


    private Sharp_Eye() : base(
        "sharpeye",
        "SE",
        new Virial.Color(0.2f, 0.8f, 0.2f),
        new IConfiguration[] { }
    )
    { }
    public static Sharp_Eye MyRole = new();

    public Citation Citation => Citations.hvtXsvc_hsg;

    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier,HasCitation
    {
        public Instance(GamePlayer player) : base(player) { }

        public Citation Citation => Citations.hvtXsvc_hsg;

        DefinedModifier RuntimeModifier.Modifier => (DefinedModifier)MyRole;

        // 原实现用 [OnlyHost] 置一个静态标记，非房主持有者永远不会闪光；改为实例内标记
        bool _gameStarted = false;

        void RuntimeAssignable.OnActivated() { }

        void OnGameStarted(GameStartEvent ev) => _gameStarted = true;

        [Local]
        void RoleChange(PlayerRoleSetEvent ev)
        {
            if (!_gameStarted) return;
            AmongUsUtil.PlayQuickFlash(Cor.blue);
        }
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (!AmOwner) return;
            name += " O".Color(new UnityEngine.Color(0.2f, 0.8f, 0.2f));
        }
    }

}