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
    void OnUpdate(UpdateEvent ev)
    {
        if (!EnableRandomEventsSettings || !NeedCheck)
            return;

        timer -= ev.DeltaTime;

        if (timer > 0)
            return;

        timer = CheckTime();
        DoActionForRES();
    }
    [Local]
    void StartUpdate(GameStartEvent ev)
    {
        NeedCheck = true;
        timer = CheckTime();
    }


    [Local]
    void CloseUpdate(GameEndEvent ev)
    {
        NeedCheck = false;
    }


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
        if (totalWeight <= 0)
        {
            PatchManager.ShowScreenOverlay(UnityEngine.Color.white, -1f, true, 3);
            return;
        }
        int index = UnityEngine.Random.Range(0, totalWeight);
        if ((index -= MeetingWeight) < 0)
        {
            DoMeeting();
            return;
        }
        if ((index -= KillWeight) < 0)
        {
            DoKill();
            return;
        }
        if ((index -= SwapWeight) < 0)
        {
            DoSwap();
            return;
        }
        if ((index -= FogWeight) < 0)
        {
            DoFog();
            return;
        }
        if ((index -= StoneWeight) < 0)
        {
            DoStone();
            return;
        }
        if ((index -= PartyWeight) < 0)
        {
            return;
        }
        if ((index -= GetKeyWeight) < 0)
        {

            return;
        }
    }
    void DoMeeting()
    {
        var players = GamePlayer.AllPlayers
            .Where(p => !p.IsDead && !p.IsDisconnected)
            .ToList();

        if (players.Count == 0)
            return;

        var target = players[UnityEngine.Random.Range(0, players.Count)];

        target.RequestEmergencyMeeting(true, false);

        PatchManager.ShowScreenOverlay(UnityEngine.Color.white, 3f, true, 2);
    }
    void DoKill()
    {
        var players = GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected).ToList();

        if (players.Count < 2)return;
        var killer = players[UnityEngine.Random.Range(0, players.Count)];
        var victims = players.Where(p => p != killer).ToList();
        var victim = victims[UnityEngine.Random.Range(0, victims.Count)];
        killer.MurderPlayer(victim,PlayerStates.Dead,null,KillParameter.NormalKill);
        PatchManager.ShowScreenOverlay(UnityEngine.Color.white, 3f, true, 2);
    }
    void DoSwap()
    {
        var players = GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected).Take(3).ToList();
        if (players.Count < 3) return;
        var pos1 = players[0].TruePosition;
        var pos2 = players[1].TruePosition;
        var pos3 = players[2].TruePosition;
        PatchManager.MovePlayer(players[0], pos2);
        PatchManager.MovePlayer(players[1], pos3);
        PatchManager.MovePlayer(players[2], pos1);
        PatchManager.ShowScreenOverlay(UnityEngine.Color.white, 3f, true, 2);
    }
    void DoFog()
    {
        PatchManager.ShowScreenOverlay(UnityEngine.Color.white, -1f, true, 3);
    }
    void DoStone()
    {
        var players = GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected).ToList();
        HostSendRpc.SetSizeY(players, 0.5f);
        foreach (var p in players)
        {
            p.GainSpeedAttribute(0.75f,120f,true, 50,"RES_stone");
        }
        PatchManager.ShowScreenOverlay(UnityEngine.Color.white, 3f, true, 2);
    }
}