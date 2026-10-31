namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>
/// 道具躲猫猫的开局兜底。修两个只在本模式出现的开局故障：
///
/// ─────────────────────────────────────────────────────────────
/// 【一】非房主躲藏者：不能走、所有冷却停摆，但 R 键能变身
/// ─────────────────────────────────────────────────────────────
/// 三个症状是同一件事。Nebula 里：
///   · 移动要求 PlayerControl.CanMove；
///   · 能力冷却（SetAsAbilityTimer）的推进条件第一条就是 CanMove；
///   · 击杀冷却看 IsKillTimerEnabled，原版里它同样以 CanMove 为前提；
///   · 而 R 键走的是 PropHuntInput 里的原始 Input.GetKeyDown，完全不看 CanMove。
/// 所以「能变身但走不动、CD 不转」= 本机 CanMove 恒为 false。
///
/// 这与沉浸梦想（ImmersionDream）角斗场 v1.8 用日志抓到的现象完全同构：
///   房主端 moveable=True；客户端 moveable=False，其余判据全部正常，且永不恢复。
/// moveable 是原版字段，房主走 CoStartGameHost、客户端走 CoStartGameClient，
/// 客户端那条协程里负责把它翻回 true 的步骤在 NoS 接管开局流程后走不到。
///
/// 为什么普通模式没事、只有躲猫猫和角斗场中招？两者的共同点是
/// 「本机玩家的原版任务列表是空的」：
///   · 角斗场：全员是船员职业，任务被清空 → 所有客户端都中招；
///   · 躲猫猫：任务被清空，但抓捕者是原版 Impostor，SpawnTaskHeader 会塞一条
///     「破坏并杀死所有人」的文本任务，列表不空 → 只有躲藏者中招。
/// 这正好对上「非房主 + 躲藏者」这个组合。原版协程的具体判据无法从反编译签名确认，
/// 所以这里不去赌它，而是照搬角斗场已经验证过的做法：
/// 开局流程走完后，只要「没有任何正当封锁源」却仍 moveable=false，就解一次卡。
///
/// 为什么不每帧无脑写 true：钻管道动画、梯子、移动平台、击杀动画都会合法地
/// 临时把 moveable 置假，无脑覆盖会破坏这些效果。所以要求卡住状态连续保持一段时间，
/// 且只在开局窗口内解一次。本模式自己的冻结走的是速度属性（GainSpeedAttribute(0f)），
/// 不碰 moveable，不受影响。
///
/// ─────────────────────────────────────────────────────────────
/// 【二】除第一张地图（Skeld）外，其它地图开局卡在空气墙外
/// ─────────────────────────────────────────────────────────────
/// 表现是玩家停在地图可行走区域之外，看得见地图却进不去。
/// 大厅里的玩家坐标都在原点附近，而原点恰好落在 Skeld 餐厅里 ——
/// 这就是「只有第一张图正常」的原因：本局的开局落点没有生效，
/// 玩家保留了大厅坐标（天空城则停在原版的 (-25, 40) 候场区）。
/// 在 Skeld 上这个坐标碰巧能走，在 Mira / Polus / Fungle / Airship 上就在墙外。
///
/// 修法同样不依赖原版内部判据：开局流程走完后检查本机玩家是否在任何一个
/// PlainShipRoom.roomArea 里（走廊、户外在原版里也都是房间，房间追踪器会显示
/// 「走廊」「室外」），不在就传送到本图的出生点。
/// 传送只能由位置的所有者发起（NetTransform 是本机权威），所以每个客户端各救各的，
/// 这也和 NoS 自己的 NebulaPreSpawnMinigame（本机 RpcSnapTo）一致。
///
/// ─────────────────────────────────────────────────────────────
/// 【三】诊断
/// ─────────────────────────────────────────────────────────────
/// 开局 20 秒内 CanMove 每次变化都打一行各判据，和角斗场的 DiagnoseMovement 一样。
/// 万一以后又出现「走不动」，Gift.log 里能直接看出是哪个条件卡住的。
/// </summary>
public static class PropHuntStartGuard
{
    #region 参数

    /// <summary>开局流程走完之后再等这么久才动手，给 NoS 出生点小游戏的收尾动画和 RpcSnapTo 留时间。</summary>
    private const float PostStartGrace = 0.6f;

    /// <summary>开局流程最长等待。超过就当作已经走完，避免某个判据永远不满足时兜底完全不生效。</summary>
    private const float StartPhaseTimeout = 15f;

    /// <summary>moveable=false 且没有正当封锁源的状态要连续保持这么久才解卡，避开各种合法的短暂定身。</summary>
    private const float StuckConfirmTime = 0.75f;

    /// <summary>解卡与诊断只在开局这段时间内生效。</summary>
    private const float WatchWindow = 20f;

    #endregion

    #region 状态

    private static bool active;
    private static float sinceGameStart;
    private static bool startPhaseDone;
    private static float graceLeft;

    private static bool spawnChecked;
    private static bool movementFixed;
    private static float stuckTimer;
    private static float watchElapsed;

    private static bool? lastCanMove;
    private static float diagTimer;

    #endregion

    #region 生命周期

    /// <summary>开局时调用（PropHuntRuntime.OnGameStart）。</summary>
    public static void Begin()
    {
        Reset();
        active = true;
        HsgDebug.Log("[HSG] 道具躲猫猫开局兜底已就位。");
    }

    /// <summary>结束 / 中途退出时调用。重复调用安全。</summary>
    public static void Reset()
    {
        active = false;
        sinceGameStart = 0f;
        startPhaseDone = false;
        graceLeft = PostStartGrace;
        spawnChecked = false;
        movementFixed = false;
        stuckTimer = 0f;
        watchElapsed = 0f;
        lastCanMove = null;
        diagTimer = 0f;
    }

    #endregion

    #region 每帧

    /// <summary>由 PropHuntRuntime.OnUpdate 调用。</summary>
    public static void Update(float deltaTime)
    {
        if (!active) return;
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;

        try
        {
            UpdateInner(deltaTime);
        }
        catch (Exception exception)
        {
            // 兜底逻辑本身绝不能把对局拖崩。出错就停用本局兜底。
            active = false;
            HsgDebug.LogWarning($"[HSG] 道具躲猫猫：开局兜底异常，本局已停用：{exception}");
        }
    }

    private static void UpdateInner(float deltaTime)
    {
        var pc = PlayerControl.LocalPlayer;
        if (pc == null) return;

        sinceGameStart += deltaTime;

        // ---- 第一步：等开局流程（揭示动画 → 出生点小游戏 → 渐显）走完 ----
        if (!startPhaseDone)
        {
            if (!IsStartSequenceFinished() && sinceGameStart < StartPhaseTimeout) return;

            graceLeft -= deltaTime;
            if (graceLeft > 0f) return;

            startPhaseDone = true;
            HsgDebug.Log($"[HSG] 道具躲猫猫：开局流程结束（用时 {sinceGameStart:0.0}s），开始检查落点与移动状态。" +
                         $" 当前 {DescribeMovement(pc)}");
        }

        // ---- 第二步：落点检查，只做一次 ----
        if (!spawnChecked)
        {
            spawnChecked = true;
            RescueFromOutsideMap(pc);
        }

        if (watchElapsed > WatchWindow) return;
        watchElapsed += deltaTime;

        // ---- 第三步：诊断 + 解卡 ----
        DiagnoseMovement(pc, deltaTime);
        EnsureCameraOnSelf(pc);
        UnstickMovement(pc, deltaTime);
    }

    /// <summary>
    /// 开局流程是否已经走完。
    /// Nebula 的 HudManager.CoShowIntro 顺序是：揭示动画 → ModPreSpawnIn（出生点小游戏）→ 渐显 → IsIntroDisplayed = false。
    /// </summary>
    private static bool IsStartSequenceFinished()
    {
        if (IntroCutscene.Instance != null) return false;
        if (Minigame.Instance != null) return false;
        if (HudManager.InstanceExists && HudManager.Instance.IsIntroDisplayed) return false;
        return true;
    }

    #endregion

    #region 【二】卡在地图外

    private static void RescueFromOutsideMap(PlayerControl pc)
    {
        var ship = ShipStatus.Instance;
        if (ship == null) return;
        if (pc.Data == null || pc.Data.IsDead) return;

        Vector2 truePosition = pc.GetTruePosition();
        if (!TryIsInsideAnyRoom(ship, truePosition, out bool inside))
        {
            HsgDebug.LogWarning("[HSG] 道具躲猫猫：本图没有可用的房间碰撞体，跳过落点检查。");
            return;
        }

        if (inside)
        {
            HsgDebug.Log($"[HSG] 道具躲猫猫：落点正常 ({truePosition.x:0.00}, {truePosition.y:0.00})。");
            return;
        }

        Vector2 colliderOffset = pc.Collider != null ? pc.Collider.offset : Vector2.zero;
        if (!TryFindSpawnPosition(ship, pc, colliderOffset, out var target))
        {
            HsgDebug.LogError($"[HSG] 道具躲猫猫：本机玩家位于地图外 ({truePosition.x:0.00}, {truePosition.y:0.00})，" +
                              "但找不到任何位于房间内的出生点，无法自动救回。");
            return;
        }

        pc.NetTransform.RpcSnapTo(target);
        HsgDebug.LogWarning($"[HSG] 道具躲猫猫：本机玩家开局停在地图外 ({truePosition.x:0.00}, {truePosition.y:0.00})，" +
                            $"已传送到出生点 ({target.x:0.00}, {target.y:0.00})。地图 ID：{SafeMapId()}");
    }

    /// <summary>
    /// 该点是否在任何一个房间的碰撞区域内。
    /// 返回 false 表示本图拿不到任何房间碰撞体，这时无法判断，调用方应放弃处理。
    /// </summary>
    private static bool TryIsInsideAnyRoom(ShipStatus ship, Vector2 point, out bool inside)
    {
        inside = false;

        var rooms = ship.AllRooms;
        if (rooms == null) return false;

        bool anyArea = false;
        foreach (var room in rooms)
        {
            if (room == null) continue;
            var area = room.roomArea;
            if (area == null) continue;

            anyArea = true;
            if (area.OverlapPoint(point))
            {
                inside = true;
                return true;
            }
        }
        return anyArea;
    }

    /// <summary>
    /// 找一个落在房间内的出生点。候选顺序：
    ///   1. 原版 ShipStatus.SpawnPlayer 的算法（InitialSpawnCenter 为圆心、按 PlayerId 均分一圈），
    ///      和正常开局的站位完全一致，大家仍然一起出现在出生区；
    ///   2. 出生点圆心本身；
    ///   3. NoS 的 NebulaPreSpawnLocation 点位表（出生点选择界面用的那一份，天然都在可通行区域，
    ///      沉浸梦想的角斗场选点用的也是它）。天空城的原版出生点就是靠这一档兜住的。
    /// 候选坐标是身体坐标（与 RpcSnapTo 的参数一致），判定时加上碰撞体偏移换成脚下坐标。
    /// </summary>
    private static bool TryFindSpawnPosition(ShipStatus ship, PlayerControl pc, Vector2 colliderOffset, out Vector2 result)
    {
        foreach (var candidate in SpawnCandidates(ship, pc))
        {
            if (!TryIsInsideAnyRoom(ship, candidate + colliderOffset, out bool inside) || !inside) continue;
            result = candidate;
            return true;
        }

        result = Vector2.zero;
        return false;
    }

    private static IEnumerable<Vector2> SpawnCandidates(ShipStatus ship, PlayerControl pc)
    {
        // 与原版 SpawnPlayer 相同：Vector2.up 逆时针旋转 (PlayerId - 1) * (360 / 人数) 度，乘以半径，再抬高 0.3636。
        int count = Mathf.Max(1, PlayerControl.AllPlayerControls.Count);
        float radians = (pc.PlayerId - 1) * (360f / count) * Mathf.Deg2Rad;
        var direction = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
        var lift = new Vector2(0f, 0.3636f);

        yield return ship.InitialSpawnCenter + direction * ship.SpawnRadius + lift;
        yield return ship.InitialSpawnCenter + lift;
        yield return ship.InitialSpawnCenter;

        byte mapId = SafeMapId();
        var table = NebulaPreSpawnLocation.Locations;
        if (table == null || mapId >= table.Length || table[mapId] == null) yield break;

        // 打乱一下，免得所有被救回来的人都叠在同一个点上。
        foreach (var location in table[mapId].OrderBy(_ => UnityEngine.Random.value))
        {
            var positions = location?.Positions;
            if (positions == null) continue;
            foreach (var position in positions) yield return position;
        }
    }

    private static byte SafeMapId()
    {
        try { return NebulaAPI.AmongUs.MapId; }
        catch { return 0; }
    }

    #endregion

    #region 【一】移动解卡

    private static void UnstickMovement(PlayerControl pc, float deltaTime)
    {
        if (movementFixed) return;

        if (pc.moveable || HasLegitimateMovementLock(pc))
        {
            stuckTimer = 0f;
            return;
        }

        stuckTimer += deltaTime;
        if (stuckTimer < StuckConfirmTime) return;

        pc.moveable = true;
        movementFixed = true;
        HsgDebug.LogWarning("[HSG] 道具躲猫猫：检测到本机 moveable 被开局流程遗留为 false，已解锁。" +
                            $"（房主={AmongUsClient.Instance.AmHost}，抓捕者={GamePlayer.LocalPlayer?.IsImpostor}）" +
                            $" 解锁后 {DescribeMovement(pc)}");
    }

    /// <summary>当前是否有「本来就该不能动」的正当理由。有就绝不碰 moveable。</summary>
    private static bool HasLegitimateMovementLock(PlayerControl pc)
    {
        if (pc.inVent || pc.walkingToVent) return true;
        if (pc.onLadder || pc.inMovingPlat) return true;
        if (pc.isKilling) return true;
        if (Minigame.Instance != null) return true;
        if (MeetingHud.Instance != null) return true;
        if (ExileController.Instance != null) return true;
        if (IntroCutscene.Instance != null) return true;

        var local = GamePlayer.LocalPlayer;
        if (local != null && (local.WillDie || local.IsTeleporting)) return true;

        return false;
    }

    /// <summary>
    /// 相机目标不是本人时，Nebula 的 PlayerCanMovePatch 会让 CanMove 恒为 false。
    /// 本模式唯一会移走相机的是「队友视角」，不在观察中却发现相机不在自己身上，说明状态残留了，拉回来。
    /// 只管活人：死人可能正在用 NoS 自带的观战功能，那是合法的。
    /// </summary>
    private static void EnsureCameraOnSelf(PlayerControl pc)
    {
        if (PropHuntSpectator.IsWatching) return;
        if (pc.Data == null || pc.Data.IsDead) return;
        if (!HudManager.InstanceExists) return;

        var cam = HudManager.Instance.PlayerCam;
        if (cam == null || cam.Target == null) return;
        if (cam.Target.Pointer == pc.Pointer) return;

        AmongUsUtil.SetCamTarget();
        HsgDebug.LogWarning("[HSG] 道具躲猫猫：相机目标残留在别处（未处于队友视角），已拉回本人。");
    }

    #endregion

    #region 【三】诊断

    private static void DiagnoseMovement(PlayerControl pc, float deltaTime)
    {
        diagTimer -= deltaTime;
        if (diagTimer > 0f) return;
        diagTimer = 0.5f;

        bool canMove = pc.CanMove;
        if (lastCanMove == canMove) return;
        lastCanMove = canMove;

        if (canMove)
        {
            HsgDebug.Log("[HSG] 道具躲猫猫：本机 CanMove 已恢复。");
            return;
        }

        HsgDebug.LogWarning("[HSG] 道具躲猫猫：本机 CanMove = false，移动与所有冷却都会停住。" + DescribeMovement(pc));
    }

    private static string DescribeMovement(PlayerControl pc)
    {
        try
        {
            bool camIsMe = false;
            if (HudManager.InstanceExists && HudManager.Instance.PlayerCam != null && HudManager.Instance.PlayerCam.Target != null)
                camIsMe = HudManager.Instance.PlayerCam.Target.Pointer == pc.Pointer;

            var position = pc.GetTruePosition();
            return $"CanMove={pc.CanMove} moveable={pc.moveable} inVent={pc.inVent} " +
                   $"Minigame={Minigame.Instance != null} MeetingHud={MeetingHud.Instance != null} " +
                   $"Exile={ExileController.Instance != null} Intro={IntroCutscene.Instance != null} " +
                   $"CamIsMe={camIsMe} Watching={PropHuntSpectator.IsWatching} " +
                   $"Host={AmongUsClient.Instance.AmHost} Seeker={GamePlayer.LocalPlayer?.IsImpostor} " +
                   $"Pos=({position.x:0.00},{position.y:0.00}) Map={SafeMapId()} Tasks={pc.myTasks?.Count}";
        }
        catch (Exception exception)
        {
            return $"(状态读取失败：{exception.Message})";
        }
    }

    #endregion
}
