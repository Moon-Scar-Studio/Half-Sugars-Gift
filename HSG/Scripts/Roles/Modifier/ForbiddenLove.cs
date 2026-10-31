using Virial.Helpers;   // Shuffle / Shuffled 扩展方法所在的命名空间，不在全局 using 里
namespace NebulaN.Roles.Modifier;

/// <summary>
/// 禁忌之恋（附加·成对）。
///
/// 表格描述：「你们是一种很特殊的链子，你们会被所有带刀角色知晓，
/// 你们无法跟随获胜，你们必须要躲避带刀角色的追杀，最后独立获胜」。
///
/// 成对分配沿用主程序集 Twin 的写法（<see cref="HasAssignmentRoutine.TryAssign"/> + SpecialAssignment），
/// 不能用 DefinedAllocatableModifierTemplate 的按阵营配额分配 —— 那套一次只发一个人。
/// </summary>
public class ForbiddenLove : DefinedModifierTemplate, HasCitation, DefinedAllocatableModifier,
    RoleFilter, IAssignableDocument
{
    static internal readonly IntegerConfiguration NumOfPairsOption = NebulaAPI.Configurations.Configuration(
        "options.modifier.forbiddenLove.numOfPairs", (0, 3), 0);
    static internal readonly IntegerConfiguration ChanceOption = NebulaAPI.Configurations.Configuration(
        "options.modifier.forbiddenLove.chance", (10, 100, 10), 100, decorator: n => n + "%");
    static private readonly BoolConfiguration HasShieldOption = NebulaAPI.Configurations.Configuration(
        "options.modifier.forbiddenLove.hasShield", true);

    private ForbiddenLove() : base(
        "forbiddenLove",
        HalfSugarGift.Core.Patch.ExTeam.ForbiddenLoveTeam.Color,
        [NumOfPairsOption, ChanceOption, HasShieldOption])
    {
        ConfigurationHolder?.SetDisplayState(() =>
            NumOfPairsOption == 0 ? ConfigurationHolderState.Inactivated : ConfigurationHolderState.Activated);
    }

    public static readonly ForbiddenLove MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    string ICodeName.CodeName => "FBL";
    bool ISpawnable.IsSpawnable => NumOfPairsOption > 0;
    int HasAssignmentRoutine.AssignPriority => 1;

    bool AssignableFilter<DefinedRole>.Test(DefinedRole role) => role.ModifierFilter?.Test(this) ?? false;
    void AssignableFilter<DefinedRole>.ToggleAndShare(DefinedRole role) => role.ModifierFilter?.ToggleAndShare(this);
    void AssignableFilter<DefinedRole>.SetAndShare(DefinedRole role, bool val) => role.ModifierFilter?.SetAndShare(this, val);
    RoleFilter HasRoleFilter.RoleFilter => this;

    SpecialAssignment[] DefinedAllocatableModifier.SpecialAssignment =>
        NumOfPairsOption > 0 ? [new(this, NumOfPairsOption, ChanceOption.GetValue(), null, null)] : [];

    void IAssignToCategorizedRole.GetAssignProperties(RoleCategory category, out int assign100, out int assignRandom, out int assignChance)
    {
        assign100 = 0; assignRandom = 0; assignChance = 0;
    }

    /// <summary>成对分配：每一对从「允许携带本修饰符」的船员里抓两个，给同一个 pairId。</summary>
    void HasAssignmentRoutine.TryAssign(IRoleTable roleTable)
    {
        Queue<(byte playerId, DefinedRole role)> pool = new(
            roleTable.GetPlayers(RoleCategory.CrewmateRole).Where(p => p.role.CanLoad(this)).Shuffle());

        int maxPairs = NumOfPairsOption;
        float chance = ChanceOption / 100f;
        int assigned = 0;

        for (int i = 0; i < maxPairs; i++)
        {
            if (pool.Count < 2) break;
            if ((float)System.Random.Shared.NextDouble() >= chance) continue;

            roleTable.SetModifier(pool.Dequeue().playerId, this, [assigned]);
            roleTable.SetModifier(pool.Dequeue().playerId, this, [assigned]);
            assigned++;
        }
    }

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => false;
    bool IAssignableDocument.HasWinCondition => true;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 0));

    /// <summary>同一 pairId 的两个人互为恋人。</summary>
    static internal IEnumerable<GamePlayer> GetPair(int pairId)
        => GamePlayer.AllPlayers.Where(p => p.TryGetModifier<Instance>(out var m) && m.PairId == pairId);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        public int PairId { get; private init; }
        private bool shieldUsed = false;

        public Instance(GamePlayer player, int pairId) : base(player) { PairId = pairId; }

        int[] RuntimeAssignable.RoleArguments => [PairId];
        void RuntimeAssignable.OnActivated() { }

        private GamePlayer? Partner => GetPair(PairId).FirstOrDefault(p => p.PlayerId != MyPlayer.PlayerId);

        /// <summary>自己、搭档、以及所有带刀角色都能看到这条链子。</summary>
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            bool visible = AmOwner || canSeeAllInfo || inEndScene
                || (local != null && (local.Role.Role.IsKiller
                    || (local.TryGetModifier<Instance>(out var m) && m.PairId == PairId)));
            if (visible) name += " ♥".Color(HalfSugarGift.Core.Patch.ExTeam.ForbiddenLoveTeam.Color.ToUnityColor());
        }

        /// <summary>成就「惊魂未定」：自带一次护盾挡下带刀角色的一刀。</summary>
        [OnlyMyPlayer, EventPriority(EventPriority.High)]
        void OnCheckKilled(PlayerCheckKilledEvent ev)
        {
            if (!HasShieldOption || shieldUsed) return;
            if (ev.Killer == null || !ev.Killer.Role.Role.IsKiller) return;
            shieldUsed = true;
            ev.Result = KillResult.ObviousGuard;
            if (AmOwner) new StaticAchievementToken("forbiddenLove.shield");
        }

        /// <summary>无法跟随任何阵营获胜。</summary>
        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.ForbiddenLoveWin;

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.ForbiddenLoveWin && BothAlive());

        private bool BothAlive()
        {
            var pair = GetPair(PairId).ToList();
            return pair.Count == 2 && pair.All(p => !p.IsDead && !p.IsDisconnected);
        }

        /// <summary>两人都活到结算的瞬间 → 抢下独立胜利。较小 PlayerId 的一侧执行，避免重复覆盖。</summary>
        [OnlyHost]
        void OnEndCriteriaMet(EndCriteriaMetEvent ev)
        {
            if (!BothAlive()) return;
            var pair = GetPair(PairId).OrderBy(p => p.PlayerId).ToList();
            if (pair[0].PlayerId != MyPlayer.PlayerId) return;

            var winners = BitMasks.AsPlayer();
            foreach (var p in pair) winners.Add(p);
            ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.ExTeam.ForbiddenLoveWin, 61,
                GameEndReason.Special, (int)winners.AsRawPattern);
        }
    }
}
