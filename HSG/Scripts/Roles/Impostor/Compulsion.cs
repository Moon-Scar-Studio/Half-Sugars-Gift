namespace NebulaN.Roles.Impostor;

/// <summary>
/// 强迫症（内鬼）。
/// 「每轮会议都有一定概率发作。发作的当轮会议如果不是全票打飞或全票弃票，
/// 则获得少数票数的一方会议结束后立刻暴毙。」
/// 另有小概率反噬：发动技能后自己也可能暴毙。
/// </summary>
public class Compulsion : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration TriggerChanceOption = NebulaAPI.Configurations.Configuration(
        "options.role.compulsion.triggerChance", (0, 100, 5), 20, decorator: n => n + "%");
    static private readonly IntegerConfiguration BackfireChanceOption = NebulaAPI.Configurations.Configuration(
        "options.role.compulsion.backfireChance", (0, 50, 5), 5, decorator: n => n + "%");

    private Compulsion() : base(
        "compulsion", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [TriggerChanceOption, BackfireChanceOption])
    { }

    public static readonly Compulsion MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => false;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>本轮是否发作。会议开始时掷骰。</summary>
        private bool triggered = false;
        /// <summary>本轮的票型：voterId → 投给谁（255 = 弃票）。</summary>
        private readonly Dictionary<byte, byte> votes = new();

        void RuntimeAssignable.OnActivated() { }

        [OnlyHost]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            votes.Clear();
            triggered = !MyPlayer.IsDead && UnityEngine.Random.Range(0, 100) < TriggerChanceOption;
            if (triggered) RpcTriggered.Invoke(MyPlayer.PlayerId);
        }

        [OnlyHost]
        void OnFixVote(MeetingFixVoteHostEvent ev)
        {
            if (!triggered) return;
            votes.Clear();
            foreach (var p in GamePlayer.AllPlayers)
            {
                if (p.IsDead) continue;
                if (!ev.GetDidVote(p)) { votes[p.PlayerId] = 255; continue; }
                votes[p.PlayerId] = ev.TryGetVotedFor(p, out var votedFor) && votedFor != null
                    ? votedFor.PlayerId
                    : (byte)255;
            }
        }

        [OnlyHost]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (!triggered) return;
            triggered = false;
            if (votes.Count == 0) return;

            // 按投票对象分组；全票一致（全打飞或全弃票）则不发作
            var groups = votes.GroupBy(kv => kv.Value).ToList();
            if (groups.Count <= 1) return;

            int minCount = groups.Min(g => g.Count());
            var losers = groups.Where(g => g.Count() == minCount).SelectMany(g => g.Select(kv => kv.Key)).ToList();

            int killed = 0;
            foreach (var id in losers)
            {
                var target = GamePlayer.GetPlayer(id);
                if (target == null || target.IsDead) continue;
                target.Suicide(HalfSugarGift.Core.Patch.ExState.Compulsion, EventDetail.Kill, KillParameter.NormalKill);
                killed++;
            }
            if (killed >= 3) RpcUnbearable.Invoke(MyPlayer.PlayerId);

            // 反噬
            if (!MyPlayer.IsDead && UnityEngine.Random.Range(0, 100) < BackfireChanceOption)
                MyPlayer.Suicide(HalfSugarGift.Core.Patch.ExState.Compulsion, EventDetail.Kill, KillParameter.NormalKill);
        }
    }

    static internal readonly RemoteProcess<byte> RpcTriggered = new("HSG.Compulsion.Triggered", (id, _) =>
    {
        if (GamePlayer.GetPlayer(id)?.AmOwner == true) new StaticAchievementToken("compulsion.onset");
    });

    static internal readonly RemoteProcess<byte> RpcUnbearable = new("HSG.Compulsion.Unbearable", (id, _) =>
    {
        if (GamePlayer.GetPlayer(id)?.AmOwner == true) new StaticAchievementToken("compulsion.unbearable");
    });
}
