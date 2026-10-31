namespace NebulaN.Roles.Impostor;

/// <summary>
/// 人造怨魂（内鬼）。
///
/// 「自由行动阶段可紫砂进入灵魂状态，可选择附身存活玩家并将刀人 cd 调整为 3。
/// 紫砂后灵魂离体一段时间后自动回体。」
///
/// 实现说明：Nebula 没有「假死后还能继续行动」的公开通道，直接调用 Suicide 会真的出局。
/// 所以这里的「紫砂」做成灵魂离体状态：本体原地留下、玩家进入隐身且免疫击杀，
/// 在此期间可以挑一个人附身；时间到自动回体。
/// </summary>
public class Wraith : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration SoulDurationOption = NebulaAPI.Configurations.Configuration(
        "options.role.wraith.soulDuration", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration PossessDurationOption = NebulaAPI.Configurations.Configuration(
        "options.role.wraith.possessDuration", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.wraith.cooldown", (15f, 120f, 5f), 40f, FloatConfigurationDecorator.Second);

    private Wraith() : base(
        "wraith", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [SoulDurationOption, PossessDurationOption, CooldownOption])
    { }

    public static readonly Wraith MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte wraithId, byte targetId, int duration)> RpcPossess =
        new("HSG.Wraith.Possess", (msg, _) =>
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (target?.AmOwner != true) return;
            target.AddModifierOnce<NebulaN.Roles.Modifier.Possessed.Instance>(
                NebulaN.Roles.Modifier.Possessed.MyRole, [msg.wraithId, msg.duration]);
        });

    static internal readonly RemoteProcess<(byte wraithId, float duration)> RpcSoul =
        new("HSG.Wraith.Soul", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.wraithId)?.Role is Instance inst) inst.EnterSoul(msg.duration);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>灵魂离体剩余时间。> 0 即处于灵魂状态。</summary>
        private float soulLeft = 0f;
        private bool possessedThisSoul = false;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var leave = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "wraith.leave", null,
                b => !b.IsInCooldown && soulLeft <= 0f,
                b => !MyPlayer.IsDead, false);
            leave.SetLabelType(ModAbilityButton.LabelType.Impostor);
            leave.OnClick = button =>
            {
                possessedThisSoul = false;
                // 灵魂状态要同步：免疫击杀的判定在房主执行，原来房主那份 soulLeft 永远是 0
                RpcSoul.Invoke((MyPlayer.PlayerId, (float)SoulDurationOption));
                button.StartCoolDown();
            };

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            tracker.SetColor(new Virial.Color(0.4f, 0.2f, 0.55f));

            var possess = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.SecondaryAbility, 0f, "wraith.possess", null,
                b => soulLeft > 0f && !possessedThisSoul && tracker.CurrentTarget != null,
                b => !MyPlayer.IsDead && soulLeft > 0f, false);
            possess.SetLabelType(ModAbilityButton.LabelType.Impostor);
            possess.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                possessedThisSoul = true;
                RpcPossess.Invoke((MyPlayer.PlayerId, target.PlayerId, (int)(float)PossessDurationOption));
                new StaticAchievementToken("wraith.newBody");
            };
        }

        /// <summary>灵魂状态：隐身 + 加速，且不会被杀。</summary>
        public void EnterSoul(float duration) => soulLeft = duration;

        // 所有客户端同步计时（房主据此判定免疫），隐身与加速只由本人施加
        void OnUpdate(GameUpdateEvent ev)
        {
            if (soulLeft <= 0f) return;
            soulLeft -= ev.DeltaTime;
            if (soulLeft <= 0f) { soulLeft = 0f; return; }
            if (!AmOwner) return;

            MyPlayer.GainAttribute(PlayerAttributes.Invisible, 0.25f, false, 0, "wraith:soul");
            MyPlayer.GainSpeedAttribute(1.2f, 0.25f, false, 0, "wraith:soulSpeed");
        }

        [OnlyMyPlayer, EventPriority(EventPriority.High)]
        void OnCheckKilled(PlayerCheckKilledEvent ev)
        {
            if (soulLeft > 0f && ev.Killer != MyPlayer) ev.Result = KillResult.Guard;
        }

        /// <summary>会议开始 → 强制回体。</summary>
        void OnMeetingStart(MeetingStartEvent ev) => soulLeft = 0f;
    }
}
