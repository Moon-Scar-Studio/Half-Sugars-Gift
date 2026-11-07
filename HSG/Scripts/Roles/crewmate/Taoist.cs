
using NebulaN.Roles.Crewmate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalfSugarGift.Roles.Crewmate;
// 道士重置版，在1.99更新后也没成功出生。
// 让我们为他悼念。
public class Taoist : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    public Taoist():base(
        "Taoist", Cor.Golden, RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam, []
        )
    {
    }
    static readonly Image vadeSprite = NebulaAPI.AddonAsset.GetResource("vade.png")?.AsImage(115f);
    public Citation Citation => Citations.Hellos497;
    public static readonly Taoist MyRole = new();
    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => false;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield break;
    }
    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => Array.Empty<IPlayerAbility>();
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

        }
        void OnMeetingStart(MeetingStartEvent ev)
        {
            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(
                vadeSprite,
                state =>
                {
                    var target = state.MyPlayer;
                    if (target == null || target == MyPlayer) {  return; }
                    if (target.IsImpostor)
                    {

                    }
                },
                p => !p.MyPlayer.IsDead && !p.MyPlayer.AmOwner
            ));
        }
    }
}