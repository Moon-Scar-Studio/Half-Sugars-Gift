using hvtXsvc.GameMode.Framework;
using VVector2 = Virial.Compat.Vector2;

namespace hvtXsvc.GameMode.CowboyDuel;

/// <summary>牛仔对决模式模块。</summary>
public sealed class CowboyDuelGameModeModule : GameModeModuleBase
{
    public const string MissionBackground = "GameMode/Mission_bg.jpg";
    public const string BulletResource = "GameMode/ZiDan.png";
    public const string MagazineResource = "GameMode/DanXia.png";
    public const string GunResource = "GameMode/Gun.jpg";
    public const string GunSceneResource = "GameMode/Gun_Scene.png";

    // 大厅桌面固定坐标，模式开始时由运行时同步到本地玩家。
    public static readonly Vector2 LeftTablePosition = new(-4.1f, 0.1f);
    public static readonly Vector2 RightTablePosition = new(4.1f, 0.1f);
    public static readonly GameEnd CowboyDuelWin = NebulaAPI.Preprocessor.CreateEnd("hsg.cowboyDuel.win", Cor.Yellow, 100);

    public override bool AllowSpecialGameEnd => true;
    public override bool ShowStatistics => false;

    /// <summary>每局开始时由 GameStartEvent 调用（见 PatchManager），不依赖模块实例化。</summary>
    public static void StartRound()
    {
        CowboyDuelState.Reset();
        CowboyDuelInteraction.Reset();
        if (AmongUsClient.Instance.AmHost)
        {
            CowboyDuelInteraction.Create();

            byte index = 0;
            foreach (var player in GamePlayer.AllPlayers.Where(p => !p.IsDead))
            {
                PatchManager.MovePlayer(player, index++ % 2 == 0 ? LeftTablePosition : RightTablePosition);
            }
        }
        HsgDebug.Log("[HSG] 牛仔对决已开始。");
    }

    /// <summary>记录玩家按顺序组装枪械，并返回是否组装完成。</summary>
    public static bool Assemble(byte playerId, CowboyDuelAssemblyStep step)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            // 非房主：本地记录进度以便通过射击检查，同时请求房主同步并切换 Sniper
            bool localCompleted = CowboyDuelState.AdvanceAssembly(playerId, step);
            CowboyDuelRpc.RequestAssembly.Invoke((playerId, step));
            return localCompleted;
        }

        bool completed = CowboyDuelState.AdvanceAssembly(playerId, step);
        if (completed)
        {
            var player = GamePlayer.GetPlayer(playerId);
            if (player != null)
                PlayerModInfo.RpcSetAssignable.Invoke((playerId, ((DefinedRole)Sniper.MyRole).Id, Array.Empty<int>(), RoleType.Role, RoleAssignType.Standard));
        }
        return completed;
    }

    /// <summary>判断玩家是否位于左右任务桌入口。</summary>
    public static bool IsAtAssemblyTable(GamePlayer player)
    {
        if (player == null || player.IsDead) return false;
        return player.TruePosition.Distance((VVector2)LeftTablePosition) < 1.5f ||
               player.TruePosition.Distance((VVector2)RightTablePosition) < 1.5f;
    }

    /// <summary>使用 Sniper 同样的扇形射线规则寻找目标并执行击杀。</summary>
    public static bool Shoot(GamePlayer shooter, GamePlayer? expectedTarget = null, float width = 1f, float range = 25f)
    {
        if (shooter == null || shooter.IsDead || !CowboyDuelState.IsReady(shooter.PlayerId)) return false;
        var origin = shooter.TruePosition;
        var direction = shooter.VanillaPlayer.transform.eulerAngles.z;
        GamePlayer? target = expectedTarget;
        float nearest = range;
        if (target == null)
        {
            foreach (var candidate in GamePlayer.AllPlayers)
            {
                if (candidate.IsDead || candidate.AmOwner || candidate.IsInvisible || candidate.IsDived) continue;
                var diff = (candidate.TruePosition - origin).Rotate(-direction);
                if (diff.x > 0f && diff.x < nearest && Mathn.Abs(diff.y) < width * 0.5f)
                {
                    target = candidate;
                    nearest = diff.x;
                }
            }
        }
        if (target == null || target == shooter || target.IsDead || target.IsInvisible || target.IsDived) return false;
        if (GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(shooter, target, new(IsKillInteraction: true))).IsCanceled ?? false)
            return false;
        if (!AmongUsClient.Instance.AmHost)
        {
            CowboyDuelRpc.RequestShot.Invoke((shooter.PlayerId, target.PlayerId));
            return true;
        }
        shooter.MurderPlayer(target, PlayerState.Sniped, EventDetail.Kill, KillParameter.RemoteKill);
        var winners = BitMasks.AsPlayer();
        winners.Add(shooter);
        NebulaGameEnd.RpcSendGameEnd(
            CowboyDuelGameModeModule.CowboyDuelWin,
            (int)winners.AsRawPattern,
            0,
            GameEndReason.Special,
            CowboyDuelGameModeModule.CowboyDuelWin,
            GameEndReason.Special
        );
        return true;
    }

    [NebulaRPCHolder]
    private static class CowboyDuelRpc
    {
        public static RemoteProcess<(byte playerId, CowboyDuelAssemblyStep step)> RequestAssembly = new("HSG_CowboyDuelAssembly", (message, _) =>
        {
            if (AmongUsClient.Instance.AmHost)
                Assemble(message.playerId, message.step);
        });

        public static RemoteProcess<(byte shooterId, byte targetId)> RequestShot = new("HSG_CowboyDuelShot", (message, _) =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            var shooter = GamePlayer.GetPlayer(message.shooterId);
            var target = GamePlayer.GetPlayer(message.targetId);
            if (shooter == null || target == null || target.IsDead) return;
            Shoot(shooter, target);
        });
    }
}

/// <summary>牛仔对决组装步骤。</summary>
public enum CowboyDuelAssemblyStep
{
    BulletIntoMagazine,
    MagazineIntoGun,
    GunAssembly
}

/// <summary>牛仔对决的最小同步状态。</summary>
public static class CowboyDuelState
{
    private static readonly Dictionary<byte, CowboyDuelAssemblyStep> Assembly = new();
    private static readonly HashSet<byte> ReadyPlayers = new();

    public static void Reset()
    {
        Assembly.Clear();
        ReadyPlayers.Clear();
    }

    public static bool AdvanceAssembly(byte playerId, CowboyDuelAssemblyStep step)
    {
        if (step == CowboyDuelAssemblyStep.BulletIntoMagazine)
        {
            Assembly[playerId] = step;
            return false;
        }

        if (!Assembly.TryGetValue(playerId, out var previous))
            return false;

        if (step == CowboyDuelAssemblyStep.MagazineIntoGun && previous != CowboyDuelAssemblyStep.BulletIntoMagazine)
            return false;
        if (step == CowboyDuelAssemblyStep.GunAssembly && previous != CowboyDuelAssemblyStep.MagazineIntoGun)
            return false;

        if (step == CowboyDuelAssemblyStep.GunAssembly)
            ReadyPlayers.Add(playerId);
        Assembly[playerId] = step;
        return step == CowboyDuelAssemblyStep.GunAssembly;
    }

    public static bool IsReady(byte playerId) => ReadyPlayers.Contains(playerId);
}

/// <summary>牛仔对决模式注册。</summary>
[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
public static class CowboyDuelGameModeRegistration
{
    public static IGameModeRegistration? Registration { get; private set; }

    public static void Preprocess(NebulaPreprocessor _)
    {
        try
        {
            // 应用牛仔对决的 Harmony 补丁（清空原版任务、覆盖任务面板、开局初始化）
            var harmony = new Harmony("HSG.CowboyDuel");
            harmony.PatchAll(typeof(CowboyDuelGameModeRegistration).Assembly);
            CowboyDuelGameStartPatch.Apply(harmony);

            // 触发同步装置的静态构造，向 Nebula 注册生成器
            HsgDebug.Log($"[HSG] 注册同步装置：{CowboyDuelDevice.Tag}");

            Registration = GameModeBuilder.Create()
                .WithTranslationKey("gamemode.hsg.cowboyDuel")
                .WithMinPlayers(2)
                .WithModuleType(typeof(IGameModeFreePlay))
                .WithAllocator(GameModeAllocators.FreePlay)
                .WithoutRoleSettings()
                .Register();
            HsgDebug.Log($"[HSG] 牛仔对决模式注册成功，模式数量：{GameModes.AllGameModes.Count()}");
        }
        catch (Exception exception)
        {
            HsgDebug.LogError($"[HSG] 牛仔对决模式注册失败：{exception}");
        }
    }
}
