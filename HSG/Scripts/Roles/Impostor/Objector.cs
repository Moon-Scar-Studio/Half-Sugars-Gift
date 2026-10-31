using Nebula.Patches;
using Virial.Helpers;

namespace NebulaN.Roles.Impostor;

/// <summary>
/// 异议者：会议中提出异议，本轮所有已投出的票在计票时改为跳过票。
///
/// 实现要点（与旧版的区别）：
/// 1. 不再调用 MeetingModRpc.RpcChangeVotingStyle —— 那是「重开投票」的接口，会清空所有票、把时间砍到 1 秒、
///    隐藏跳过按钮、重排投票面板，并且会被「放弃投票惩罚」误伤。现在改为由房主在
///    MeetingFixVoteHostEvent / PlayerFixVoteHostEvent 中把票改成跳过票，投票流程本身不受影响。
/// 2. 异议状态由 RPC 同步到全体客户端（房主需要知道）；可在结果公布前取消，取消会返还次数。
/// 3. 后果由房主在计票时广播 ObjectionApplied 统一触发。该 RPC 由房主在 CompleteVoting 之前发出，
///    同一发送方的消息有序到达，因此不存在「最后一刻取消」导致各端结果不一致的问题。
/// 4. 「会议后死亡」改为额外放逐（ModMarkAsExtraVictim），与主程序集的写法一致。
/// </summary>
public class Objector : DefinedRoleTemplate, HasCitation, DefinedRole, RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    public static ValueConfiguration<int> ObjectorConsequence = NebulaAPI.Configurations.Configuration(
        "options.role.objector.ObjectorConsequence", new string[]
        {
            "options.role.objector.ObjectorConsequence.ShowSelfRole",
            "options.role.objector.ObjectorConsequence.Suicide",
            "options.role.objector.ObjectorConsequence.CantKill",
            "options.role.objector.ObjectorConsequence.None",
        }
        , 0
        );

    public static IntegerConfiguration MaxUsesOption = NebulaAPI.Configurations.Configuration(
        "options.role.objector.uses", (1, 10, 1), 1);

    const int ConsequenceShowRole = 0;
    const int ConsequenceSuicide = 1;
    const int ConsequenceCantKill = 2;

    Objector() : base(
        "objector",
        Cor.impRed,
        RoleCategory.ImpostorRole,
        NebulaTeams.ImpostorTeam,
        new Virial.Configuration.IConfiguration[] { MaxUsesOption, ObjectorConsequence }
    )
    {
        ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/ObjectorPic.png")?.AsImage(115f);
    }

    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/ObjectorIcon.png")?.AsImage();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("ObjectorObjection.png")?.AsImage(115f),
            "role.objector.doc.objection");
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("ObjectorCancel.png")?.AsImage(115f),
            "role.objector.doc.cancel");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new AssignableDocumentReplacement("%USES%", MaxUsesOption.GetValue().ToString());
    }

    public static readonly Objector MyRole = new();

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static bool IsObjectionPhase(MeetingHud meeting) =>
        meeting.CurrentState == MeetingHud.MeetingStates.Discussion ||
        meeting.CurrentState == MeetingHud.MeetingStates.NotVoted ||
        meeting.CurrentState == MeetingHud.MeetingStates.Voted;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        public Instance(GamePlayer player) : base(player) { }
        public DefinedRole Role => MyRole;

        // 「失去击杀能力」后果：每帧动态读取
        bool RuntimeRole.HasVanillaKillButton => !killDisabled;

        // ---- 全体客户端共享的状态（经 RPC 同步） ----
        bool objecting = false;      // 本场会议是否处于异议中
        bool appliedThisMeeting = false; // 房主已在本场会议执行过异议
        bool roleRevealed = false;
        bool killDisabled = false;

        // ---- 仅所有者 ----
        int usesLeft;
        ModAbilityButtonImpl? button;
        TMPro.TextMeshPro? usesText;

        public bool IsRoleRevealed => roleRevealed;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            usesLeft = MaxUsesOption.GetValue();

            var objectionSprite = NebulaAPI.AddonAsset.GetResource("ObjectorObjection.png")?.AsImage()?.GetSprite();
            var cancelSprite = NebulaAPI.AddonAsset.GetResource("ObjectorCancel.png")?.AsImage()?.GetSprite() ?? objectionSprite;

            button = new ModAbilityButtonImpl(alwaysShow: true).Register(this);
            button.SetSprite(objectionSprite);
            button.SetLabel("objector.obj");
            usesText = button.ShowUsesIcon(4);
            usesText.text = usesLeft.ToString();

            button.Visibility = _ =>
                !MyPlayer.IsDead &&
                MeetingHud.Instance.AsBoolFast(out var meeting) && IsObjectionPhase(meeting) &&
                !appliedThisMeeting &&
                (objecting || usesLeft > 0);
            button.Availability = _ =>
                MeetingHud.Instance.AsBoolFast(out var meeting) && IsObjectionPhase(meeting);

            button.OnClick = _ =>
            {
                if (appliedThisMeeting) return;
                if (!objecting)
                {
                    if (usesLeft <= 0) return;
                    usesLeft--;
                    RpcSetObjecting.Invoke((MyPlayer.PlayerId, true));
                    button.SetSprite(cancelSprite);
                    button.SetLabel("objector.cancel");
                }
                else
                {
                    usesLeft++; // 取消异议返还次数
                    RpcSetObjecting.Invoke((MyPlayer.PlayerId, false));
                    button.SetSprite(objectionSprite);
                    button.SetLabel("objector.obj");
                }
                if (usesText) usesText!.text = usesLeft.ToString();
            };
        }

        // 每场会议开始时重置（所有客户端各自执行，结果一致）
        void OnMeetingStart(MeetingStartEvent ev)
        {
            objecting = false;
            appliedThisMeeting = false;
            if (AmOwner && button != null)
            {
                button.SetSprite(NebulaAPI.AddonAsset.GetResource("ObjectorObjection.png")?.AsImage()?.GetSprite());
                button.SetLabel("objector.obj");
            }
        }

        static Instance? Find(byte playerId) => GamePlayer.GetPlayer(playerId)?.Role as Instance;

        public static RemoteProcess<(byte playerId, bool objecting)> RpcSetObjecting = new("HSG.Objector.SetObjecting", (msg, _) =>
        {
            var obj = Find(msg.playerId);
            if (obj == null) return;
            if (obj.appliedThisMeeting) return; // 已经计票，取消无效
            obj.objecting = msg.objecting;
        });

        /// <summary>房主计票时广播：本场异议已生效，各客户端执行后果。</summary>
        public static RemoteProcess<(byte playerId, int consequence)> RpcObjectionApplied = new("HSG.Objector.Applied", (msg, _) =>
        {
            var obj = Find(msg.playerId);
            if (obj == null) return;
            obj.appliedThisMeeting = true;
            obj.objecting = false;

            switch (msg.consequence)
            {
                case ConsequenceShowRole: obj.roleRevealed = true; break;
                case ConsequenceCantKill: obj.killDisabled = true; break;
            }

            var local = GamePlayer.LocalPlayer;
            if (local != null && !local.IsDead)
                // 注意：这里不能写 Color.red —— 嵌套类里 Color 会解析成外层 DefinedAssignableTemplate.Color 属性
                AmongUsUtil.PlayQuickFlash(new Virial.Color(1f, 0f, 0f));
        });

        bool ShouldVoid => objecting && !appliedThisMeeting && !MyPlayer.IsDead;

        // ---- 房主：把全场已投出的票改为跳过票 ----
        [OnlyHost]
        void OnFixVotes(MeetingFixVoteHostEvent ev)
        {
            if (!ShouldVoid) return;

            foreach (var p in GamePlayer.AllPlayers)
            {
                if (p.IsDead) continue;
                if (!ev.GetDidVote(p)) continue;
                ev.SetVotedFor(p, null);
            }

            int consequence = ObjectorConsequence.GetValue();
            // 必须在 CompleteVoting 之前发出，保证各端先收到本消息
            RpcObjectionApplied.Invoke((MyPlayer.PlayerId, consequence));

            if (consequence == ConsequenceSuicide &&
                !MeetingHudExtension.MarkedAsExtraVictims(MyPlayer.PlayerId))
            {
                MyPlayer.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
            }
        }

        // 兜底：防止其他职业在逐玩家阶段把票改回去。
        // RemoteProcess.Invoke 会在本地立刻执行，所以房主此时 appliedThisMeeting 已为 true。
        // 若同一会议内投票被重开（如正义使者），异议对重开后的计票依然有效。
        [OnlyHost]
        void OnFixPlayerVote(PlayerFixVoteHostEvent ev)
        {
            if (!appliedThisMeeting) return;
            if (!ev.DidVote) return;
            if (ev.VoteTo != null) ev.VoteTo = null;
        }

        // ---- 公开身份 ----
        [OnlyMyPlayer]
        void OnCheckRoleVisibility(PlayerCheckRoleInfoVisibilityLocalEvent ev)
        {
            if (roleRevealed) ev.CanSeeRole = true;
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (roleRevealed && !inEndScene)
                name += " " + Language.Translate("role.objector.publicTag");
        }
    }
}
