namespace hvtXsvc.GameMode.CowboyDuel;

/// <summary>牛仔对决的枪械组装装置：同步对象 + 原版 SystemConsole（原版交互系统原生支持发现与使用）。</summary>
public class CowboyDuelDevice : NebulaSyncStandardObject
{
    public static string Tag => "HSG_CowboyDuelGunScene";

    static CowboyDuelDevice() => RegisterInstantiater("HSG_CowboyDuelGunScene", args => new CowboyDuelDevice(new(args[0], args[1])));

    public CowboyDuelDevice(Vector2 pos) : base(pos, ZOption.Just, true, LoadSprite())
    {
        // 参考 ItemSupplierManager：创建 Minigame 预制体，交给 SystemConsolize 生成原版交互台
        var prefabObject = new GameObject("CowboyDuelGunMinigame");
        var prefab = prefabObject.AddComponent<CowboyDuelMinigame>();
        SystemConsolize(MyRenderer.gameObject, MyRenderer, ImageNames.UseButton, prefab, 1.2f);
    }

    private static Sprite? LoadSprite() => NebulaAPI.AddonAsset.GetResource(CowboyDuelGameModeModule.GunSceneResource)?.AsImage(200f)?.GetSprite();
}

/// <summary>牛仔对决任务点的创建入口。</summary>
public static class CowboyDuelInteraction
{
    private static bool created;

    /// <summary>每局开始时重置创建标志。</summary>
    public static void Reset() => created = false;

    /// <summary>房主根据人数在食堂随机位置广播生成任务点。</summary>
    public static void Create()
    {
        if (created) return;
        created = true;

        int deviceCount = Math.Max(1, GamePlayer.AllPlayers.Count());
        for (int i = 0; i < deviceCount; i++)
        {
            NebulaSyncObject.RpcInstantiate(CowboyDuelDevice.Tag, new float[]
            {
                UnityEngine.Random.Range(-6.5f, 6.5f),
                UnityEngine.Random.Range(1.5f, 4.5f)
            });
        }
        HsgDebug.Log($"[HSG] 牛仔对决已生成 {deviceCount} 个组装任务点。");
    }
}

public sealed class CowboyDuelMinigame : Minigame
{
    private int step;
    private static readonly string[] resources =
    {
        CowboyDuelGameModeModule.BulletResource,
        CowboyDuelGameModeModule.MagazineResource,
        CowboyDuelGameModeModule.GunResource
    };

    static CowboyDuelMinigame() => Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<CowboyDuelMinigame>();
    public CowboyDuelMinigame(IntPtr ptr) : base(ptr) { }
    public CowboyDuelMinigame() : base(Il2CppInterop.Runtime.Injection.ClassInjector.DerivedConstructorPointer<CowboyDuelMinigame>())
    {
        Il2CppInterop.Runtime.Injection.ClassInjector.DerivedConstructorBody(this);
    }

    public override void Begin(PlayerTask task)
    {
        MinigameHelper.BeginInternal(this, task);
        var background = UnityHelper.CreateObject<SpriteRenderer>("MissionBackground", transform, Vector3.zero);
        background.sprite = NebulaAPI.AddonAsset.GetResource(CowboyDuelGameModeModule.MissionBackground)?.AsImage(100f)?.GetSprite();
        CreateButton(0, new Vector3(-2f, 0f, 0f));
        CreateButton(1, new Vector3(0f, 0f, 0f));
        CreateButton(2, new Vector3(2f, 0f, 0f));
    }

    private void CreateButton(int index, Vector3 position)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("AssemblyPart", transform, position);
        renderer.sprite = NebulaAPI.AddonAsset.GetResource(resources[index])?.AsImage(100f)?.GetSprite();
        var button = renderer.gameObject.SetUpButton();
        button.OnClick.AddListener(() => Assemble(index));
    }

    private void Assemble(int index)
    {
        if (index != step) return;
        if (index == 0) CowboyDuelGameModeModule.Assemble(PlayerControl.LocalPlayer.PlayerId, CowboyDuelAssemblyStep.BulletIntoMagazine);
        if (index == 1) CowboyDuelGameModeModule.Assemble(PlayerControl.LocalPlayer.PlayerId, CowboyDuelAssemblyStep.MagazineIntoGun);
        if (index == 2 && CowboyDuelGameModeModule.Assemble(PlayerControl.LocalPlayer.PlayerId, CowboyDuelAssemblyStep.GunAssembly))
            MinigameHelper.CloseInternal(this);
        step++;
    }
}
