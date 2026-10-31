namespace NebulaN.Roles.Impostor;

/// <summary>
/// 惯犯（内鬼）。
/// 每次出刀按权重随机触发一种效果：
/// 毁尸灭迹 20% / 杀心四溢 30% / 永无休止 5% / 偶尔失手 5% / 我的刀钝 10% / 无事发生 30%。
/// </summary>
public class Recidivist : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration CleanChance = NebulaAPI.Configurations.Configuration(
        "options.role.recidivist.cleanChance", (0, 100, 5), 20, decorator: n => n + "%");
    static private readonly IntegerConfiguration HasteChance = NebulaAPI.Configurations.Configuration(
        "options.role.recidivist.hasteChance", (0, 100, 5), 30, decorator: n => n + "%");
    static private readonly IntegerConfiguration InstantChance = NebulaAPI.Configurations.Configuration(
        "options.role.recidivist.instantChance", (0, 100, 5), 5, decorator: n => n + "%");
    static private readonly IntegerConfiguration MissChance = NebulaAPI.Configurations.Configuration(
        "options.role.recidivist.missChance", (0, 100, 5), 5, decorator: n => n + "%");
    static private readonly IntegerConfiguration BluntChance = NebulaAPI.Configurations.Configuration(
        "options.role.recidivist.bluntChance", (0, 100, 5), 10, decorator: n => n + "%");
    static private readonly FloatConfiguration HasteAmount = NebulaAPI.Configurations.Configuration(
        "options.role.recidivist.hasteAmount", (1f, 15f, 0.5f), 5f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration BluntCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.recidivist.bluntCooldown", (20f, 60f, 2.5f), 40f, FloatConfigurationDecorator.Second);

    private Recidivist() : base(
        "recidivist", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [CleanChance, HasteChance, InstantChance, MissChance, BluntChance, HasteAmount, BluntCooldown])
    { }

    public static readonly Recidivist MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => false;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private enum Effect { Clean, Haste, Instant, Miss, Blunt, Nothing }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        // 时序说明：点击击杀键后，主程序集会「立刻」重置冷却（ResetKillCooldownLocalEvent），
        // 而 PlayerKillPlayerEvent 要等房主判定后才回来。所以效果必须在出刀那一刻就掷好，
        // 冷却类效果在紧接着的冷却重置里生效；需要尸体的「毁尸灭迹」等击杀真正发生后再执行。
        private float? pendingCooldown = null;
        private bool pendingClean = false;
        private int instantStreak = 0;
        private int bluntStreak = 0;

        void RuntimeAssignable.OnActivated() { }

        [Local, OnlyMyPlayer]
        void OnTryKill(PlayerTryVanillaKillLocalEventAbstractPlayerEvent ev)
        {
            pendingClean = false;
            pendingCooldown = null;

            switch (Roll())
            {
                case Effect.Miss:
                    // 偶尔失手：这一刀落空，刀照样进入冷却
                    ev.Cancel(true);
                    ResetStreaks();
                    break;
                case Effect.Clean:
                    pendingClean = true;
                    ResetStreaks();
                    break;
                case Effect.Haste:
                    pendingCooldown = Math.Max(1f, MyPlayer.TeamKillCooldown - HasteAmount);
                    ResetStreaks();
                    break;
                case Effect.Instant:
                    pendingCooldown = 0.1f;
                    instantStreak++;
                    bluntStreak = 0;
                    if (instantStreak >= 2) new StaticAchievementToken("recidivist.lucky");
                    break;
                case Effect.Blunt:
                    pendingCooldown = BluntCooldown;
                    bluntStreak++;
                    instantStreak = 0;
                    if (bluntStreak >= 3) new StaticAchievementToken("recidivist.sharpen");
                    break;
                default:
                    ResetStreaks();
                    break;
            }
        }

        [Local]
        void OnKill(PlayerKillPlayerEvent ev)
        {
            if (ev.Murderer != MyPlayer || ev.Dead == MyPlayer) return;
            if (!pendingClean) return;
            pendingClean = false;
            PatchManager.RpcRemoveBody.Invoke(ev.Dead);
        }

        private void ResetStreaks() { instantStreak = 0; bluntStreak = 0; }

        [Local, OnlyMyPlayer]
        void ResetKillCD(ResetKillCooldownLocalEvent ev)
        {
            if (pendingCooldown == null) return;
            ev.SetFixedCooldown(pendingCooldown.Value);
            pendingCooldown = null;
        }

        /// <summary>按权重掷效果；六个概率之和不足 100 的部分算「无事发生」。</summary>
        private static Effect Roll()
        {
            int roll = UnityEngine.Random.Range(0, 100);
            int acc = CleanChance;
            if (roll < acc) return Effect.Clean;
            acc += HasteChance; if (roll < acc) return Effect.Haste;
            acc += InstantChance; if (roll < acc) return Effect.Instant;
            acc += MissChance; if (roll < acc) return Effect.Miss;
            acc += BluntChance; if (roll < acc) return Effect.Blunt;
            return Effect.Nothing;
        }
    }
}
