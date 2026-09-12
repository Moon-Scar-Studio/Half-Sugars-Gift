
namespace HalfSugarGift.Core.Patch;

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
[NebulaRPCHolder]
public class FunModes: AbstractModule<Game>, IGameOperator
{

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.DIManager.RegisterModule<Game>(() => new FunModes());
    }

    protected override void OnInjected(Game container) => this.Register(container);

    // 三分钟鼎力巨作
    public static BoolConfiguration EnableMiniMode = NebulaAPI.Configurations.Configuration(
        "options.hsg.fm.enableMiniMode", false
        );
    public static FloatConfiguration MiniScale = NebulaAPI.Configurations.Configuration(
        "options.hsg.fm.miniScale",(10f,99f,3f),50f,FloatConfigurationDecorator.Percentage,()=>EnableMiniMode
        );
    [OnlyHost]
    void OnGameStart(GameStartEvent ev)
    {
        if (!EnableMiniMode) return;
        float scaleCount = MiniScale.GetValue();
        foreach (var player in GamePlayer.AllPlayers)
        {
            player.GainSizeAttribute(new Vector2(scaleCount / 100, scaleCount / 100), float.MaxValue, true, 114514,"HsgFunModeMini");
        }
    }
    [NebulaRPC]
    private static void RpcClearMiniMode()
    {
        if (PlayerControl.LocalPlayer == null) return;
        var player = GamePlayer.AllPlayers.FirstOrDefault(p => p.AmOwner);
        player?.RemoveAttributeByTag("HsgFunModeMini");
    }

    [OnlyHost]
    void OnGameEnd(GameEndEvent ev)
    {
        if (!EnableMiniMode) return;
        RpcClearMiniMode();
    }
}