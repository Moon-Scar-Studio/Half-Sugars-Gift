using Nebula.Patches;
using Virial.Helpers;

namespace NebulaN.Roles.Impostor;

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
    Objector() : base(
        "objector",
        Cor.impRed,
        RoleCategory.ImpostorRole,
        NebulaTeams.ImpostorTeam,
        new Virial.Configuration.IConfiguration[] { ObjectorConsequence }
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
            "role.objector.doc.image"
        );
    }

    public static readonly Objector MyRole = new();


    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        // "禁止击杀"后果：原实现把 IPlayerAbility.HideKillButton 写在角色实例上，但 MyAbilities 返回空数组，
        // 框架只查 MyAbilities 里的 ability，永远读不到。正确做法是覆盖 RuntimeRole.HasVanillaKillButton（每帧动态读取）。
        bool RuntimeRole.HasVanillaKillButton => !NeedHideKillBtn;
        bool UsedObjector = false;
        bool NeedHideKillBtn = false;
        bool NeedConsequence = true;
        bool NeedShowRole = false;
        bool NeedSuicide = false;
        public bool _NeedShowRole => NeedShowRole;
        public Instance(GamePlayer player) : base(player) { }
        public DefinedRole Role => MyRole;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            var objector = new ModAbilityButtonImpl(alwaysShow: true).Register(this);
            // 实际资源名是 ObjectorObjection.png（原代码引用的 objector.png 不存在）
            objector.SetSprite(NebulaAPI.AddonAsset.GetResource("ObjectorObjection.png")?.AsImage()?.GetSprite());
            objector.Availability = (button) => MeetingHud.Instance.AsBoolFast(out var meeting) && meeting.CurrentState == MeetingHud.MeetingStates.NotVoted;
            objector.Visibility = (button) => !MyPlayer.IsDead && MeetingHud.Instance.AsBoolFast(out var meeting) && (meeting.CurrentState == MeetingHud.MeetingStates.NotVoted || meeting.CurrentState == MeetingHud.MeetingStates.Discussion) && !UsedObjector;
            objector.SetLabel("objector.obj");
            objector.OnClick = (button) =>
            {
                MeetingModRpc.RpcChangeVotingStyle.Invoke((0XFFFFFF, false, 1f, false, true));
                UsedObjector = true;
            };
        }

        // 后果在所有客户端同步：公开身份 / 禁止击杀都需要其他客户端知道
        public static RemoteProcess<(byte playerId, int consequence)> RpcSetConsequence = new("HSG.Objector.SetConsequence", (msg, _) =>
        {
            var player = GamePlayer.GetPlayer(msg.playerId);
            if (player?.Role is not Instance obj) return;
            switch (msg.consequence)
            {
                case 0: obj.NeedShowRole = true; break;
                case 1: obj.NeedSuicide = true; break;
                case 2: obj.NeedHideKillBtn = true; break;
            }
        });

        [Local]
        void SetConsequence(MeetingVoteEndEvent ev)
        {
            if (!UsedObjector) return;
            if (!NeedConsequence) return;
            NeedConsequence = false;
            RpcSetConsequence.Invoke((MyPlayer.PlayerId, ObjectorConsequence.GetValue()));
            PatchManager.RpcFlashAll.Invoke("#FF0000");
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (!NeedSuicide) return;
            NeedSuicide = false;
            MyPlayer.Suicide(PlayerState.Dead, null, KillParameter.NormalKill);
            PatchManager.RpcRemoveBody.Invoke(MyPlayer);
        }

        // "公开身份"后果：让所有客户端都能看到反对者的真实职业。
        // 原实现依赖一个从未被添加的 ObjectorPublicRole 修饰符，且其监听是 [Local]，只有反对者自己能看到。
        [OnlyMyPlayer]
        void OnCheckRoleVisibility(PlayerCheckRoleInfoVisibilityLocalEvent ev)
        {
            if (NeedShowRole) ev.CanSeeRole = true;
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (NeedShowRole && !inEndScene)
                name += " " + Language.Translate("role.objector.publicTag");
        }
    }
}
