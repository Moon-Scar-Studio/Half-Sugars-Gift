using HSGTeam = HalfSugarGift.Core.Patch.Team;
using static HalfSugarGift.Core.Patch.Cor;

namespace HalfSugarGift.Roles.Neutral;
public class WitchJudge : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    static readonly FloatConfiguration PunishCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.witchJudge.punishCooldown",
        (5f, 120f, 1f),
        20f,
        FloatConfigurationDecorator.Second
    );
    static readonly FloatConfiguration PunishDuration = NebulaAPI.Configurations.Configuration(
        "options.role.witchJudge.punishDuration",
        (5f, 120f, 1f),
        30f,
        FloatConfigurationDecorator.Second
    );
    static readonly IntegerConfiguration PunishMaxUses = NebulaAPI.Configurations.Configuration(
        "options.role.witchJudge.punishMaxUses",
        (1, 10, 1),
        3
    );
    static readonly BoolConfiguration ShowVoteRecords = NebulaAPI.Configurations.Configuration(
        "options.role.witchJudge.showVoteRecords",
        true
    );

    WitchJudge() : base(
        "witchJudge",
        PurpleWitchJudge,
        RoleCategory.NeutralRole,
        HSGTeam.WitchJudgeTeam,
        new Virial.Configuration.IConfiguration[] {
            PunishCooldown, PunishDuration, PunishMaxUses,
            ShowVoteRecords
        }
    ) { }

    public static readonly WitchJudge MyRole = new();
    public Citation Citation => Citations.hvtXsvc_hsg;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static readonly Virial.Media.Image? ExecuteDocIcon =
        NebulaAPI.AddonAsset.GetResource("ExecuteButton.png")?.AsImage(115f);

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(ExecuteDocIcon, "role.witchJudge.doc.execute");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield break;
    }

    internal static readonly Dictionary<byte, float> JailedUntil = new();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        DefinedRole RuntimeRole.Role => MyRole;

        static readonly TranslatableTag ExecutionState = State.ExecutedByJudge;

        static readonly Image? ExecuteIcon =
            NebulaAPI.AddonAsset.GetResource("ExecuteButton.png")?.AsImage(170f);

        static readonly Image? PunishIcon =
            NebulaAPI.AddonAsset.GetResource("Smallicon/WitchJudgeIcon.png")?.AsImage(115f);

        static readonly RemoteProcess<(byte judgeId, byte targetId, int usesLeft, float duration)> RpcUsePunish = new(
            "WitchJudge.UsePunish",
            (msg, _) =>
            {
                var judge = GamePlayer.GetPlayer(msg.judgeId);
                if (judge == null) return;
                if (judge.Role is not Instance inst) return;
                var target = GamePlayer.GetPlayer(msg.targetId);
                if (target == null) return;
                inst._usesLeft = msg.usesLeft;
                inst._jailed = target;
                inst._punishExpire = NebulaGameManager.Instance!.CurrentTime + msg.duration;
                inst._locked = true;

                JailedUntil[msg.targetId] = NebulaGameManager.Instance.CurrentTime + msg.duration;

                inst.EnsureKillBlockSubscribed();

                if (judge.AmOwner)
                {
                    string teamLabel = target.IsKiller
                        ? Language.Translate("role.witchJudge.eye.killer")
                        : Language.Translate("role.witchJudge.eye.notKiller");
                    string msg2 = Language.Translate("role.witchJudge.eye.message")
                        .Replace("%PLAYER%", target.Name)
                        .Replace("%TEAM%", teamLabel);
                    PlayerControl.LocalPlayer.RpcSendChat(msg2);
                }
            }
        );

        static readonly RemoteProcess<byte> RpcInsulate = new(
            "WitchJudge.Insulate",
            (playerId, _) =>
            {
                MeetingHudExtension.AddSealedMask(1 << playerId);
                var player = GamePlayer.GetPlayer(playerId);
                if (player != null && player.AmOwner)
                    MeetingHudExtension.CanUseAbility = false;
                if (MeetingHud.Instance != null)
                    MeetingHud.Instance.ResetPlayerState();
            }
        );
        GamePlayer? _jailed;
        float _punishExpire;
        bool _locked;
        int _executedImpostorCount;
        public int _usesLeft;
        private readonly Dictionary<byte, byte?> _voteRecords = new();
        bool _killBlockSubscribedLocally;
        bool _exposed;
        ModAbilityButton? _punishButton;
        ModAbilityButton? _executeButton;
        private static readonly RemoteProcess<(byte judgeId, bool isKiller)> RpcSyncExecute = new(
            "WitchJudge.SyncExecute",
            (msg, _) =>
            {
                var judge = GamePlayer.GetPlayer(msg.judgeId);
                if (judge == null) return;
                if (judge.Role is not Instance inst) return;
                inst._jailed = null;
                inst._locked = false;
                if (!msg.isKiller)
                {
                    inst._exposed = true;
                    inst._usesLeft = 0;
                }
            }
        );
        public Instance(GamePlayer player) : base(player) { }
        private void EnsureKillBlockSubscribed()
        {
            if (_killBlockSubscribedLocally || GameOperatorManager.Instance == null) return;
            _killBlockSubscribedLocally = true;
            GameOperatorManager.Instance.Subscribe<PlayerTryVanillaKillLocalEventAbstractPlayerEvent>(ev =>
            {
                if (NebulaGameManager.Instance == null) return;
                if (JailedUntil.TryGetValue(ev.Player.PlayerId, out var expiry))
                {
                    if (NebulaGameManager.Instance.CurrentTime < expiry)
                    {
                        ev.Cancel();
                    }
                    else
                    {
                        JailedUntil.Remove(ev.Player.PlayerId);
                    }
                }
            }, this);
        }
        [OnlyHost]
        void OnFixVote(PlayerFixVoteHostEvent ev)
        {
            if (ev.Player == MyPlayer && ev.DidVote && ev.VoteTo != null)
            {
                ev.Vote = 2;
                MeetingHudExtension.WeightMap[MyPlayer.PlayerId] = 2;
            }
        }
        [OnlyHost]
        void OnTieVote(MeetingTieVoteHostEvent ev)
        {
            MeetingHudExtension.ExileEvenIfTie = true;
        }
        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Dead.PlayerState != ExecutionState) return;
            if (ev.Murderer != MyPlayer) return;
            if (ev.Dead.Role?.Role?.IsKiller ?? false)
            {
                _executedImpostorCount++;
                if (_executedImpostorCount >= 2 && MyPlayer.IsAlive)
                {
                    int mask = 1 << MyPlayer.PlayerId;
                    NebulaGameEnd.RpcSendGameEnd(HSGTeam.WitchJudgeWin, mask, 0, GameEndReason.Situation, HSGTeam.WitchJudgeWin, GameEndReason.Situation);
                }
            }
        }
        [OnlyMyPlayer]
        void CheckExtraWins(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.OpportunistPhase) return;
            if (MyPlayer.IsAlive)
            {
                ev.ExtraWinMask.Add(HSGTeam.ExtraWitchJudgeWin);
                ev.IsExtraWin = true;
            }
        }
        [Local]
        void OnVoteCast(PlayerVoteCastEvent ev)
        {
            if (!ShowVoteRecords) return;
            _voteRecords[ev.Voter.PlayerId] = ev.VoteFor?.PlayerId;
        }
        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (ShowVoteRecords && _voteRecords.Count > 0 && MyPlayer.IsAlive)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(Language.Translate("role.witchJudge.vote.title"));
                foreach (var (voterId, targetId) in _voteRecords)
                {
                    var voter = GamePlayer.GetPlayer(voterId);
                    string voterName = voter?.Name ?? "?";
                    string targetName;
                    if (targetId.HasValue)
                    {
                        var target = GamePlayer.GetPlayer(targetId.Value);
                        targetName = target?.Name ?? "?";
                    }
                    else
                    {
                        targetName = Language.Translate("role.witchJudge.vote.skip");
                    }
                    sb.AppendLine($"{voterName} → {targetName}");
                }
                PlayerControl.LocalPlayer.RpcSendChat(sb.ToString().TrimEnd());
                _voteRecords.Clear();
            }
        }
        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (MyPlayer.IsAlive)
            {
                HudManager.Instance.Chat.SetVisible(true);
            }

            if (_jailed != null && _jailed.IsAlive)
            {
                RpcInsulate.Invoke(_jailed.PlayerId);
            }
        }
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                _usesLeft = PunishMaxUses;
                var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
                _punishButton = NebulaAPI.Modules.AbilityButton(
                    this, MyPlayer,
                    VirtualKeyInput.Ability,
                    PunishCooldown,
                    "witchJudge.punish",
                    PunishIcon,
                    (ModAbilityButton _) => tracker.CurrentTarget != null && tracker.CurrentTarget != MyPlayer && !tracker.CurrentTarget.IsDead,
                    (button) => !MyPlayer.IsDead && _usesLeft > 0,
                    false
                );
                _punishButton.OnClick = (button) =>
                {
                    var target = tracker.CurrentTarget;
                    if (target == null || target == MyPlayer || target.IsDead) return;
                    if (_usesLeft <= 0 || _locked) return;
                    int nextUses = _usesLeft - 1;
                    RpcUsePunish.Invoke((MyPlayer.PlayerId, target.PlayerId, nextUses, PunishDuration));
                    _usesLeft = nextUses;
                    _locked = true;
                };
                _executeButton = NebulaAPI.Modules.AbilityButton(this, false, false, 0, true)
                    .BindKey(VirtualKeyInput.SidekickAction)
                    .SetLabel("witchJudge.execute")
                    .SetLabelType(ModAbilityButton.LabelType.Impostor)
                    .SetColorLabel(MyRole.UnityColor);
                _executeButton.Visibility = _ => MyPlayer.IsAlive && AmongUsUtil.InMeeting && _jailed != null;
                _executeButton.Availability = _ => _jailed != null && _jailed.IsAlive;
                _executeButton.SetImage(ExecuteIcon!);
                _executeButton.OnClick = _ =>
                {
                    if (_jailed == null || _jailed.IsDead) return;
                    var target = _jailed;
                    bool isKiller = target.IsKiller;
                    MyPlayer.MurderPlayer(target, ExecutionState, EventDetail.Kill, KillParameter.MeetingKill, KillCondition.BothAlive);
                    RpcSyncExecute.Invoke((MyPlayer.PlayerId, isKiller));
                    _jailed = null;
                    _locked = false;
                    if (!isKiller)
                    {
                        _exposed = true;
                        _usesLeft = 0;
                    }
                    MeetingHud.Instance.RpcClose();
                };
            }
        }
        void IGameOperator.OnReleased() { }
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (_exposed)
                name += Language.Translate("role.witchJudge.exposedTag");
        }
    }
}
public static class WitchJudgeVoteIconPatch
{
    internal static readonly Dictionary<byte, int> RenderedVoteCounts = new();
    public static void ModBloopAVoteIconPrefix(ref NetworkedPlayerInfo? voterPlayer)
    {
        if (voterPlayer == null) return;
        byte id = voterPlayer.PlayerId;
        RenderedVoteCounts.TryGetValue(id, out int count);
        RenderedVoteCounts[id] = count + 1;
        if (count > 0 && IsWitchJudgeDoubleVote(id))
            voterPlayer = null;
    }
    public static void PopulateResultsPrefix()
    {
        RenderedVoteCounts.Clear();
    }
    private static bool IsWitchJudgeDoubleVote(byte playerId)
    {
        var player = GamePlayer.GetPlayer(playerId);
        if (player?.Role?.Role != WitchJudge.MyRole) return false;
        return MeetingHudExtension.WeightMap.GetValueOrDefault(playerId, 1) > 1;
    }
}
