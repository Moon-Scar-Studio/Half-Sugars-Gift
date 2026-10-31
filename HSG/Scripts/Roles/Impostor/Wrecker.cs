namespace NebulaN.Roles.Impostor;

/// <summary>
/// 破坏王（内鬼）。
/// 「破坏时自身击杀 cd 减少 5 秒，且在破坏期间每次出刀自身 cd 再次减少 5 秒（最低 10 秒），
/// 效果跨会议保留，但破坏结束后效果解除并重新开始计算减少的 cd 时间」。
/// </summary>
public class Wrecker : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration StepOption = NebulaAPI.Configurations.Configuration(
        "options.role.wrecker.step", (1f, 15f, 0.5f), 5f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration FloorOption = NebulaAPI.Configurations.Configuration(
        "options.role.wrecker.floor", (5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);

    private Wrecker() : base(
        "wrecker", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [StepOption, FloorOption])
    { }

    public static readonly Wrecker MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => false;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>破坏期间累计减掉的秒数。破坏结束清零。</summary>
        private float reduction = 0f;
        private bool inSabotage = false;

        void RuntimeAssignable.OnActivated() { }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner) return;
            bool nowSabotage = AmongUsUtil.InAnySab;
            if (nowSabotage == inSabotage) return;

            inSabotage = nowSabotage;
            if (inSabotage)
            {
                // 破坏开始：先白送一档
                reduction = StepOption;
                new StaticAchievementToken("wrecker.executioner");
            }
            else
            {
                // 破坏结束：效果解除，重新计算
                reduction = 0f;
            }
        }

        /// <summary>破坏期间每次出刀再减一档。</summary>
        [Local]
        void OnKill(PlayerKillPlayerEvent ev)
        {
            if (!AmOwner || ev.Murderer != MyPlayer || !inSabotage) return;
            reduction += StepOption;
        }

        [Local]
        void ResetKillCD(ResetKillCooldownLocalEvent ev)
        {
            if (!AmOwner || reduction <= 0f) return;
            float baseCd = MyPlayer.TeamKillCooldown;
            float cd = Math.Max(FloorOption, baseCd - reduction);
            ev.SetFixedCooldown(cd);
            if (cd <= FloorOption + 0.01f) new StaticAchievementToken("wrecker.kingOfKings");
        }
    }
}
