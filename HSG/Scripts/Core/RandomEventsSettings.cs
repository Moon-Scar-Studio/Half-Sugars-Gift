using System;
using System.Collections.Generic;
using System.Linq;
using Virial.Configuration;
using Virial.Events.Game;

namespace HalfSugarGift.Core.Settings;

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
[NebulaRPCHolder]
public class RandomEventSettings : AbstractModule<Game>, IGameOperator
{
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.DIManager.RegisterModule<Game>(() => new RandomEventSettings());
    }

    public static bool NeedCheck = false;
    private float timer;
    
    protected override void OnInjected(Game container) => this.Register(container);

    public static BoolConfiguration EnableRandomEventsSettings =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.EnableRES",
            false
        );

    public static BoolConfiguration EnableChangeRandomTime =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.EnableCRT",
            true,
            () => EnableRandomEventsSettings
        );

    public static FloatConfiguration RandomTime =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.RandomTime",
            (15f, 600f, 10f),
            60f,
            FloatConfigurationDecorator.Second,
            () => EnableRandomEventsSettings && EnableChangeRandomTime
        );


    public static IntegerConfiguration MeetingWeight =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.weight.meeting",
            (0, 100),
            10
        );

    public static IntegerConfiguration KillWeight =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.weight.kill",
            (0, 100),
            10
        );

    public static IntegerConfiguration SwapWeight =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.weight.swap",
            (0, 100),
            10
        );

    public static IntegerConfiguration FogWeight =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.weight.fog",
            (0, 100),
            10
        );

    public static IntegerConfiguration StoneWeight =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.weight.stone",
            (0, 100),
            10
        );

    public static IntegerConfiguration PartyWeight =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.weight.party",
            (0, 100),
            0
        );

    public static IntegerConfiguration GetKeyWeight =
        NebulaAPI.Configurations.Configuration(
            "options.hsg.res.weight.getkey",
            (0, 100),
            0
        );
    // 随机事件只能由房主掷骰并执行：MurderPlayer / MovePlayer / GainAttribute 内部都会广播 RPC，
    // 如果每个客户端各自执行，N 个玩家就会产生 N 次随机杀人和 N² 次属性 RPC。
    // 视觉反馈通过 PatchManager.RpcShowOverlayAll 广播给所有人。
    [OnlyHost]
    void OnUpdate(GameUpdateEvent ev)
    {
        if (!EnableRandomEventsSettings || !NeedCheck)
            return;
        if (MeetingHud.Instance != null || ExileController.Instance != null)
            return;

        timer -= ev.DeltaTime;
        if (timer > 0)
            return;

        timer = CheckTime();
        DoActionForRES();
    }

    void StartUpdate(GameStartEvent ev)
    {
        NeedCheck = true;
        timer = CheckTime();
    }

    void CloseUpdate(GameEndEvent ev)
    {
        NeedCheck = false;
    }

    static void OverlayAll(float duration, bool pulse, int pulseCount)
        => PatchManager.RpcShowOverlayAll.Invoke(("#FFFFFF", duration, pulse, pulseCount));

    float CheckTime()
    {
        if (EnableChangeRandomTime)
            return RandomTime;
        return UnityEngine.Random.Range(15f, 600f);
    }

    void DoActionForRES()
    {
        int totalWeight =
            MeetingWeight +
            KillWeight +
            SwapWeight +
            FogWeight +
            StoneWeight +
            PartyWeight +
            GetKeyWeight;

        if (totalWeight <= 0) return; // 所有权重为 0：什么都不发生

        int index = UnityEngine.Random.Range(0, totalWeight);

        if ((index -= MeetingWeight) < 0) { DoMeeting(); return; }
        if ((index -= KillWeight) < 0) { DoKill(); return; }
        if ((index -= SwapWeight) < 0) { DoSwap(); return; }
        if ((index -= FogWeight) < 0) { DoFog(); return; }
        if ((index -= StoneWeight) < 0) { DoStone(); return; }
        if ((index -= PartyWeight) < 0) { DoParty(); return; }
        if ((index -= GetKeyWeight) < 0) { DoGetKey(); return; }
    }

    static List<GamePlayer> AlivePlayers()
        => GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected).ToList();

    void DoMeeting()
    {
        var players = AlivePlayers();
        if (players.Count == 0) return;
        var target = players[UnityEngine.Random.Range(0, players.Count)];
        target.RequestEmergencyMeeting(true, false);
        OverlayAll(3f, true, 2);
    }

    void DoKill()
    {
        var players = AlivePlayers();
        if (players.Count < 2) return;
        var killer = players[UnityEngine.Random.Range(0, players.Count)];
        var victims = players.Where(p => p != killer).ToList();
        var victim = victims[UnityEngine.Random.Range(0, victims.Count)];
        killer.MurderPlayer(victim, PlayerStates.Dead, EventDetail.Kill, KillParameter.NormalKill);
        OverlayAll(3f, true, 2);
    }

    void DoSwap()
    {
        // 随机挑 3 人轮换位置（原来 Take(3) 固定取前三名玩家）
        var players = AlivePlayers().OrderBy(_ => UnityEngine.Random.value).Take(3).ToList();
        if (players.Count < 3) return;
        var pos1 = players[0].TruePosition;
        var pos2 = players[1].TruePosition;
        var pos3 = players[2].TruePosition;
        PatchManager.MovePlayer(players[0], pos2);
        PatchManager.MovePlayer(players[1], pos3);
        PatchManager.MovePlayer(players[2], pos1);
        OverlayAll(3f, true, 2);
    }

    void DoFog()
    {
        // 原实现显示 duration=-1 的永久白色遮罩且从不移除；改为限时浓雾 + 视野缩减
        OverlayAll(20f, true, 6);
        foreach (var p in AlivePlayers())
            p.GainAttribute(PlayerAttributes.Eyesight, 20f, 0.5f, false, 50, "HSG.RES.Fog");
    }

    void DoStone()
    {
        foreach (var p in AlivePlayers())
        {
            // GainSizeAttribute 自带 RPC 广播，不需要绕道 HostSendRpc
            p.GainSizeAttribute(new Virial.Compat.Vector2(1f, 0.5f), 120f, true, 50, "HSG.RES.StoneSize");
            p.GainSpeedAttribute(0.75f, 120f, true, 50, "HSG.RES.StoneSpeed");
        }
        OverlayAll(3f, true, 2);
    }

    void DoParty()
    {
        // 派对：所有人短暂加速
        foreach (var p in AlivePlayers())
            p.GainSpeedAttribute(1.5f, 30f, false, 50, "HSG.RES.Party");
        OverlayAll(3f, true, 2);
    }

    void DoGetKey()
    {
        // 随机一名存活玩家获得钥匙大师修饰符
        var players = AlivePlayers();
        if (players.Count == 0) return;
        var target = players[UnityEngine.Random.Range(0, players.Count)];
        if (!target.TryGetModifier<NebulaN.Roles.Modifier.KeyMaster.Instance>(out _))
            target.AddModifier(NebulaN.Roles.Modifier.KeyMaster.MyRole);
        OverlayAll(3f, true, 2);
    }
}
