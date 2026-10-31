namespace NebulaN.Roles.Impostor;

/// <summary>
/// 代码员（内鬼）。
/// 「每轮会议阶段可标记一人（播下 bug 虫），之后被标记人第一次使用技能／出刀时，
/// 令其无效并让技能／刀进入 cd」。
/// </summary>
public class Coder : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly BoolConfiguration OncePerMeetingOption = NebulaAPI.Configurations.Configuration(
        "options.role.coder.oncePerMeeting", true);
    static private readonly FloatConfiguration PenaltyCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.coder.penaltyCooldown", (5f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);

    private Coder() : base(
        "coder", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [OncePerMeetingOption, PenaltyCooldownOption])
    { }

    public static readonly Coder MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>种下 bug：只有被标记者本人需要真正挂上修饰符。</summary>
    static internal readonly RemoteProcess<(byte coderId, byte targetId, int penalty)> RpcPlantBug =
        new("HSG.Coder.PlantBug", (msg, _) =>
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (target?.AmOwner != true) return;
            target.AddModifierOnce<NebulaN.Roles.Modifier.Bugged.Instance>(
                NebulaN.Roles.Modifier.Bugged.MyRole, [msg.coderId, msg.penalty]);
        });

    /// <summary>bug 生效了，回头通知代码员拿成就。</summary>
    static internal readonly RemoteProcess<byte> RpcBugTriggered =
        new("HSG.Coder.BugTriggered", (coderId, _) =>
        {
            if (GamePlayer.GetPlayer(coderId)?.AmOwner == true)
                new StaticAchievementToken("coder.seed");
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private bool usedThisMeeting = false;

        void RuntimeAssignable.OnActivated() { }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            usedThisMeeting = false;
            if (MyPlayer.IsDead) return;

            var bm = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            bm?.RegisterMeetingAction(new(HalfSugarGift.Core.Patch.ExHelpers.MeetingIconOf(7),
                p =>
                {
                    usedThisMeeting = true;
                    RpcPlantBug.Invoke((MyPlayer.PlayerId, p.MyPlayer.PlayerId, (int)(float)PenaltyCooldownOption));
                },
                p => !(OncePerMeetingOption && usedThisMeeting) && !MyPlayer.IsDead
                     && !p.MyPlayer.AmOwner && !p.MyPlayer.IsDead
                     && !NebulaN.Roles.Modifier.Smokescreen.IsProtected(p.MyPlayer),
                false));
        }

        // 实际的「让技能/刀失效」逻辑在 Modifier/Bugged.cs 里 —— 拦截必须发生在出刀者自己的客户端。
    }
}
