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

    /// <summary>房主生成任务点：人数不超过 3 时刷在大厅，否则全图房间随机。</summary>
    public static void Create()
    {
        if (created) return;
        created = true;

        var ship = ShipStatus.Instance;
        if (ship == null || ship.AllRooms == null) return;
        int deviceCount = Math.Max(1, GamePlayer.AllPlayers.Count());

        // 位置来源改为原版任务机：玩家必然可达，避免随机点卡出地图（Console.Room 字段原型：ShipExtension 的接线任务）
        var positions = UnityEngine.Object.FindObjectsOfType<Console>()
            .Where(c => deviceCount > 3 || c.Room == SystemTypes.Cafeteria)
            .Select(c => new Vector2(c.transform.position.x, c.transform.position.y))
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();

        // 兜底：无可用任务机时回退房间随机采样（人数不超过 3 固定大厅，否则随机房间）
        var room = deviceCount <= 3
            ? ship.AllRooms.FirstOrDefault(r => r.RoomId == SystemTypes.Cafeteria)
            : ship.AllRooms[UnityEngine.Random.Range(0, ship.AllRooms.Length)];
        for (int i = 0; i < deviceCount; i++)
        {
            var pos = positions.Count > 0 ? positions[i % positions.Count] : RandomPointInRoom(room);
            NebulaSyncObject.RpcInstantiate(CowboyDuelDevice.Tag, new float[] { pos.x, pos.y });
        }
        HsgDebug.Log($"[HSG] 牛仔对决已生成 {deviceCount} 个组装任务点。");
    }

    /// <summary>在房间可行走区域内随机取点，避免刷进墙里（原型：Jailer/AmongUsUtil 的 roomArea.OverlapPoint）。</summary>
    private static Vector2 RandomPointInRoom(PlainShipRoom? room)
    {
        if (room?.roomArea == null) return new Vector2(0f, 0f);
        var bounds = room.roomArea.bounds;
        for (int i = 0; i < 20; i++)
        {
            var pos = new Vector2(UnityEngine.Random.Range(bounds.min.x, bounds.max.x), UnityEngine.Random.Range(bounds.min.y, bounds.max.y));
            if (room.roomArea.OverlapPoint(pos)) return pos;
        }
        return bounds.center;
    }
}

/// <summary>枪械组装界面：把子弹拖到弹匣、弹匣拖到枪，按顺序完成组装。</summary>
public sealed class CowboyDuelMinigame : Minigame
{
    private int step;
    private readonly SpriteRenderer?[] parts = new SpriteRenderer?[3];
    private readonly Vector3[] homePositions = new Vector3[3];
    private int? dragging;
    private TextMeshPro? hint;
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
        // 参考 MovingPlatform 冻结玩家的完整配方：清速度、清队列、刚体设为 Kinematic
        var local = PlayerControl.LocalPlayer;
        if (local != null)
        {
            local.MyPhysics.ResetMoveState(true);
            local.moveable = false;
            local.NetTransform.SetPaused(true);
            local.NetTransform.ClearPositionQueues();
            local.SetKinematic(true);
        }
        var background = UnityHelper.CreateObject<SpriteRenderer>("MissionBackground", transform, Vector3.zero);
        background.sprite = NebulaAPI.AddonAsset.GetResource(CowboyDuelGameModeModule.MissionBackground)?.AsImage(100f)?.GetSprite();
        // 显式指定 sortingOrder，避免同层渲染顺序随机导致背景遮挡零件
        background.sortingOrder = 1;
        CreatePart(0, new Vector3(-2f, 0f, 0f));
        CreatePart(1, new Vector3(0f, 0f, 0f));
        CreatePart(2, new Vector3(2f, 0f, 0f));

        // 步骤提示文字（原型：NebulaPreSpawnMinigame 实例化 StandardTextPrefab）
        hint = GameObject.Instantiate(VanillaAsset.StandardTextPrefab, transform);
        hint.transform.localPosition = new Vector3(0f, -2.2f, 0f);
        hint.transform.localScale = Vector3.one;
        hint.fontSizeMax = 2.5f;
        hint.fontSizeMin = 1f;
        hint.fontSize = 2.5f;
        hint.rectTransform.sizeDelta = new Vector2(6f, 0.8f);
        UpdateHint();
    }

    /// <summary>创建可拖拽零件：显式 sortingOrder 保证图层顺序，碰撞体用于拖拽与落点判定。</summary>
    private void CreatePart(int index, Vector3 position)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("AssemblyPart" + index, transform, position);
        renderer.sprite = NebulaAPI.AddonAsset.GetResource(resources[index])?.AsImage(100f)?.GetSprite();
        renderer.sortingOrder = index + 2;
        var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
        if (renderer.sprite != null) collider.size = renderer.sprite.bounds.size;
        parts[index] = renderer;
        homePositions[index] = position;
    }

    /// <summary>界面销毁时恢复移动（ESC 关闭与组装完成两条路径都会走到这里）。</summary>
    private void OnDestroy()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null) return;
        local.moveable = true;
        local.NetTransform.SetPaused(false);
        local.SetKinematic(false);
        local.NetTransform.Halt();
    }

    private void Update()
    {
        if (step > 1) return;
        if (dragging == null)
        {
            // 当前步骤对应的零件才可拖动
            var part = parts[step];
            if (Input.GetMouseButtonDown(0) && part != null &&
                part.GetComponent<Collider2D>().OverlapPoint(GetMouseWorld()))
                dragging = step;
        }
        else
        {
            var part = parts[dragging.Value]!;
            var world = GetMouseWorld();
            // 保留原 z：界面整体渲染在相机前 -50 处，直接用屏幕点的 z 会跑到近平面
            part.transform.position = new Vector3(world.x, world.y, part.transform.position.z);
            if (Input.GetMouseButtonUp(0))
            {
                Drop(dragging.Value);
                dragging = null;
            }
        }
    }

    /// <summary>松手：落在目标零件上则安装（零件消失），否则弹回原位。</summary>
    private void Drop(int index)
    {
        var part = parts[index]!;
        var target = parts[index + 1];
        if (target != null && target.GetComponent<Collider2D>().OverlapPoint(part.transform.position))
        {
            part.gameObject.SetActive(false);
            Assemble(index);
        }
        else part.transform.localPosition = homePositions[index];
    }

    private void Assemble(int index)
    {
        if (index == 0) CowboyDuelGameModeModule.Assemble(PlayerControl.LocalPlayer.PlayerId, CowboyDuelAssemblyStep.BulletIntoMagazine);
        if (index == 1)
        {
            CowboyDuelGameModeModule.Assemble(PlayerControl.LocalPlayer.PlayerId, CowboyDuelAssemblyStep.MagazineIntoGun);
            if (CowboyDuelGameModeModule.Assemble(PlayerControl.LocalPlayer.PlayerId, CowboyDuelAssemblyStep.GunAssembly))
            {
                // 换成狙击手后不带冷却：清零原版与 Mod 按钮的击杀冷却（原型：Snatcher.RewindKillCooldown）
                NebulaAPI.CurrentGame!.KillButtonLikeHandler.SetCooldown(0f);
                // RpcSetAssignable 广播到达时 SetRole 会重置冷却，持续清零补偿时序
                NebulaManager.Instance.StartCoroutine(CoClearKillCooldown().WrapToIl2Cpp());
                // 组装完成：全屏标题提示（原型：Avenger 的 TitleShower.SetText）
                NebulaAPI.CurrentGame?.GetModule<TitleShower>()?.SetText(
                    Language.Translate("task.hsg.cowboyDuel.ready"), Cor.Yellow.ToUnityColor(), 1f, true);
                MinigameHelper.CloseInternal(this);
            }
        }
        step++;
        UpdateHint();
    }

    /// <summary>更新底部步骤提示文字。</summary>
    private void UpdateHint()
    {
        if (hint == null) return;
        if (step == 0) hint.text = Language.Translate("task.hsg.cowboyDuel.loadBullet");
        else if (step == 1) hint.text = Language.Translate("task.hsg.cowboyDuel.loadMagazine");
        else hint.text = "";
    }

    /// <summary>持续清零击杀冷却 3 秒，补偿 RpcSetAssignable 广播往返到达时 SetRole 的冷却重置。</summary>
    private static System.Collections.IEnumerator CoClearKillCooldown()
    {
        for (float time = 0f; time < 3f; time += Time.deltaTime)
        {
            NebulaAPI.CurrentGame!.KillButtonLikeHandler.SetCooldown(0f);
            yield return null;
        }
    }

    private static Vector3 GetMouseWorld() => Camera.main!.ScreenToWorldPoint(Input.mousePosition);
}
