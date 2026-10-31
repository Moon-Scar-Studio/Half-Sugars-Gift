namespace NebulaN.Roles.Neutral;

/// <summary>
/// 疯子（中立）。
/// 「你知道所有人的身份，请存活至最后，然后抢夺那些愚蠢的人的胜利。
/// 疯子无法拥有除迷你船员以外的所有副职业，疯子投出的票无效。」
/// </summary>
public class Madman : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration SurvivorsOption = NebulaAPI.Configurations.Configuration(
        "options.role.madman.survivors", (1, 5), 3);

    private Madman() : base(
        "madman", HalfSugarGift.Core.Patch.ExTeam.MadmanTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.MadmanTeam,
        [SurvivorsOption])
    {
        // 「不能携带副职业」的限制放在运行期做：
        // AssignableFilterHolder.ModifierFilter 是模板里的显式接口实现，构造函数里
        // 只能解析到同名的「类型」而不是成员（CS0119）；而且 SetAndShare 会发 RPC，
        // 在注册阶段调用本来就不合适。
    }

    public static readonly Madman MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => false;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            // 开局若已经被分到了副职业，一并剥掉
            foreach (var m in MyPlayer.Modifiers.ToList())
                if (m.Modifier is DefinedAllocatableModifier) MyPlayer.RemoveModifier(m.Modifier);
        }

        /// <summary>疯子无法拥有任何副职业，拿到就立刻卸掉。</summary>
        [OnlyMyPlayer]
        void OnModifierSet(PlayerModifierSetEvent ev)
        {
            if (!AmOwner) return;
            // 只剥离「可随机分配」的副职业。bug 虫、中毒、宝石之盾等由其它职业在对局中施加的效果必须保留，
            // 否则疯子对这些技能完全免疫。
            if (ev.Modifier.Modifier is not DefinedAllocatableModifier) return;
            MyPlayer.RemoveModifier(ev.Modifier.Modifier);
        }

        /// <summary>知晓全场身份。</summary>
        [Local]
        void OnDecorateName(PlayerDecorateNameEvent ev)
        {
            if (!AmOwner || ev.Player == MyPlayer) return;
            ev.Name += " " + ev.Player.Role.Role.DisplayColoredShort;
        }

        /// <summary>疯子的票作废。</summary>
        [OnlyHost]
        void OnFixVote(MeetingFixVoteHostEvent ev)
        {
            if (MyPlayer.IsDead) return;
            ev.SetVote(MyPlayer, 0);
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.MadmanWin;

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.MadmanWin && !MyPlayer.IsDead);

        /// <summary>活到最后 → 把别人的胜利抢过来。</summary>
        [OnlyHost]
        void OnEndCriteriaMet(EndCriteriaMetEvent ev)
        {
            if (MyPlayer.IsDead) return;
            if (HalfSugarGift.Core.Patch.ExHelpers.AlivePlayers.Count() > SurvivorsOption) return;

            var winners = BitMasks.AsPlayer();
            winners.Add(MyPlayer);
            ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.ExTeam.MadmanWin, 57,
                GameEndReason.Special, (int)winners.AsRawPattern);
            RpcWin.Invoke(MyPlayer.PlayerId);
        }
    }

    static internal readonly RemoteProcess<byte> RpcWin = new("HSG.Madman.Win", (id, _) =>
    {
        if (GamePlayer.GetPlayer(id)?.AmOwner == true) new StaticAchievementToken("madman.whoIsMad");
    });
}
