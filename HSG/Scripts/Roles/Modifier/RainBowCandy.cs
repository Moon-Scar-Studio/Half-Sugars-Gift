namespace NebulaN.Roles.Modifier;
public class RainbowCandy : 
    DefinedAllocatableModifierTemplate,
    DefinedAllocatableModifier, 
    RuntimeAssignableGenerator<RuntimeModifier>, 
    HasCitation
{
    private static FloatConfiguration changeInterval = NebulaAPI.Configurations.Configuration(
        "options.modifier.rainbowcandy.interval",
        (0.1f, 2f, 0.05f),
        0.3f,
        FloatConfigurationDecorator.Second
    );

    private RainbowCandy() : base(
        "rainbowcandy",
        "rb",
        new Virial.Color(1f, 0.5f, 0f),
        new IConfiguration[] { changeInterval }
    )
    { }

    public static RainbowCandy MyRole = new();

    public Citation Citation => Citations.hvtXsvc_hsg;

    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        private Coroutine? _colorRoutine;
        private bool _running;
        private const string OutfitTag = "HSG.RainBowCandy";
        // Palette.PlayerColors 的实际数量随游戏版本变化，运行时读取而不是写死 0~18
        private static int ColorCount => Palette.PlayerColors?.Length ?? 18;

        public Instance(GamePlayer player) : base(player) { }

        DefinedModifier RuntimeModifier.Modifier => (DefinedModifier)MyRole;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            _running = true;
            _colorRoutine = NebulaManager.Instance.StartCoroutine(ColorLoop().WrapToIl2Cpp());
        }

        private IEnumerator ColorLoop()
        {
            while (_running && !MyPlayer.IsDead)
            {
                int randomColorId = UnityEngine.Random.Range(0, ColorCount);
                // 通过 Nebula 的 Outfit 系统改颜色（AddOutfit 自带全网同步），
                // 原实现依赖 HostSendRpc.SetColor —— 那段代码因嵌套 Register 从未真正改过颜色。
                var outfit = new OutfitDefinition(MyPlayer.DefaultOutfit, true, overriddenColor: randomColorId);
                MyPlayer.AddOutfit(new OutfitCandidate(outfit, OutfitTag, OutfitPriority.Paint, true));
                yield return new WaitForSeconds(changeInterval);
            }
            if (AmOwner) MyPlayer.RemoveOutfitByTag(OutfitTag);
        }

        void IGameOperator.OnReleased()
        {
            _running = false;
            if (_colorRoutine != null)
            {
                NebulaManager.Instance.StopCoroutine(_colorRoutine);
                _colorRoutine = null;
            }
            if (AmOwner) MyPlayer.RemoveOutfitByTag(OutfitTag);
        }
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (!AmOwner) return;
            name += ColorHelper.Create(" |RainBow|", 0.5f, 0.05f, 0.9f, 1f);
        }
    }
}
