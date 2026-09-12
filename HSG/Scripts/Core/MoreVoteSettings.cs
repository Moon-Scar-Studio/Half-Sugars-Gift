using System.Collections;
using AmongUs.GameOptions;
using HarmonyLib;
using Nebula.Modules;
using Nebula.Patches;
using Virial;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace HalfSugarGift.Core.Settings;

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
[NebulaRPCHolder]
public class MoreVoteSettings : AbstractModule<Game>, IGameOperator
{
    private bool _hasAdjusted = false;

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.DIManager.RegisterModule<Game>(() => new MoreVoteSettings());
    }

    // 必须是 override：用 new 只会隐藏基类的虚方法，Nebula 调用的是基类空实现，Register 永远不会执行。
    protected override void OnInjected(Game container) => this.Register(container);

    public static BoolConfiguration EnableVoteTimeChange = NebulaAPI.Configurations.Configuration(
        "options.hsg.mvs.enablevotetimechange", false);

    public static IntegerConfiguration TriggerCount = NebulaAPI.Configurations.Configuration(
        "options.hsg.mvs.triggercount", (1, 24), 1, () => EnableVoteTimeChange);

    public static FloatConfiguration VoteDuration = NebulaAPI.Configurations.Configuration(
        "options.hsg.mvs.voteduration", (5f, 120f, 5f), 10f,
        FloatConfigurationDecorator.Second, () => EnableVoteTimeChange);

    [OnlyHost]
    public void OnMeetingStart(MeetingStartEvent ev)
    {
        _hasAdjusted = false;
        if (!EnableVoteTimeChange) return;
        NebulaManager.Instance.StartCoroutine(CoWaitVotingPhase().WrapToIl2Cpp());
    }

    private IEnumerator CoWaitVotingPhase()
    {
        while (MeetingHud.Instance == null || MeetingHud.Instance.CurrentState == MeetingHud.MeetingStates.Discussion)
            yield return null;

        while (MeetingHud.Instance != null &&
               MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.NotVoted &&
               MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.Voted)
            yield return null;

        if (!AmongUsClient.Instance.AmHost) yield break;

        CheckAndAdjust();
    }

    [OnlyHost]
    void OnPlayerVote(PlayerVoteCastEvent ev)
    {
        if (_hasAdjusted || !EnableVoteTimeChange) return;
        if (MeetingHud.Instance == null || MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.Voted)
            return;
        CheckAndAdjust();
    }

    [OnlyHost]
    void OnPlayerDie(PlayerDieEvent ev)
    {
        if (_hasAdjusted || !EnableVoteTimeChange) return;
        if (MeetingHud.Instance == null || MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.Voted)
            return;
        CheckAndAdjust();
    }

    [OnlyHost]
    void OnPlayerDisconnect(PlayerDisconnectEvent ev)
    {
        if (_hasAdjusted || !EnableVoteTimeChange) return;
        if (MeetingHud.Instance == null || MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.Voted)
            return;
        CheckAndAdjust();
    }

    private void CheckAndAdjust()
    {
        if (_hasAdjusted || !EnableVoteTimeChange) return;

        int remaining = 0;
        var meeting = MeetingHud.Instance;
        if (meeting == null) return;

        foreach (var state in meeting.playerStates)
        {
            if (state == null) continue;
            var player = GamePlayer.GetPlayer(state.PlayerId);
            if (player == null || player.IsDead || player.IsDisconnected) continue;
            if (!state.DidVote && state.VotedForId.Value == 252)
                remaining++;
        }

        if (remaining > TriggerCount) return;

        MeetingModRpc.RpcChangeVotingStyle.Invoke((
            0xFFFFFF,
            false,
            VoteDuration,
            false,
            false
        ));

        _hasAdjusted = true;
    }

    [OnlyHost]
    public void OnMeetingEnd(MeetingEndEvent ev)
    {
        _hasAdjusted = false;
    }
}