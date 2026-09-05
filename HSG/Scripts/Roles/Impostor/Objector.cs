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
            NebulaAPI.AddonAsset.GetResource("ObjectorButton.png")?.AsImage(115f),
            "role.objector.doc.image"
        );
    }

    public static readonly Objector MyRole = new();


    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator, IPlayerAbility
    {
        IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => Array.Empty<IPlayerAbility>();
        bool IPlayerAbility.HideKillButton => NeedHideKillBtn;
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
            objector.SetSprite(NebulaAPI.AddonAsset.GetResource("objector.png")?.AsImage().GetSprite());
            objector.Availability = (button) => MeetingHud.Instance.AsBoolFast(out var meeting) && meeting.CurrentState == MeetingHud.MeetingStates.NotVoted;
            objector.Visibility = (button) => !MyPlayer.IsDead && MeetingHud.Instance.AsBoolFast(out var meeting) && (meeting.CurrentState == MeetingHud.MeetingStates.NotVoted || meeting.CurrentState == MeetingHud.MeetingStates.Discussion) && !UsedObjector;
            objector.SetLabel("objector.obj");
            objector.OnClick = (button) =>
            {
                MeetingModRpc.RpcChangeVotingStyle.Invoke((0XFFFFFF,false,1f,false,true));
                UsedObjector = true;
            };
        }
        public static RemoteProcess<byte> RpcSetNeedShowRole = new("RpcObjectorSetNeedShow", (PlayerId, _) =>
        {
            var Player = GamePlayer.GetPlayer(PlayerId);
            if (Player?.Role is Objector.Instance obj)
                obj.NeedShowRole = true;
        });
        [Local]
        void SetConsequence(MeetingVoteEndEvent ev)
        {
            if (!UsedObjector) return;
            if (!NeedConsequence) return;
            NeedConsequence = false;
            switch (GetConsequenceIndex(ObjectorConsequence))
            {
                case 0: NeedShowRole = true;  RpcSetNeedShowRole.Invoke(MyPlayer.PlayerId); break;
                case 1:NeedSuicide = true; break;
                case 2:NeedHideKillBtn = true; break;
                case 3:break;
            }
            PatchManager.RpcFlashAll.Invoke("GunMu");
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (!NeedSuicide) return;
            MyPlayer.Suicide(PlayerState.Dead,null,KillParameter.NormalKill);
            PatchManager.RpcRemoveBody.Invoke(MyPlayer);
        }

        int GetConsequenceIndex(ValueConfiguration<int> indexConfig)
        {
            switch (indexConfig.GetValue())
            {
                case 0: return 0;
                case 1: return 1;
                case 2: return 2;
                case 3: return 3;
                default: return 0;
            }
            // 我都写完了发现好像用不着这方法。
            // 没事他很直观。
            
        }
        [Local]
        void ClearNeedShowRole(GameEndEvent ev) => NeedShowRole = false;
        
    }
}
    
