namespace NebulaN.Roles.Crewmate;

public class Tester : DefinedRoleTemplate, DefinedRole, HasCitation, IAssignableDocument
{

    private Tester() : base(
        "Tester",
        Cor.MPCor,
        RoleCategory.CrewmateRole,
        NebulaTeams.CrewmateTeam,
        new IConfiguration[] {  })
    {

    }

    public static Tester MyRole = new();

    public Citation Citation => Citations.hvtXsvc_hsg;
    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public Instance(GamePlayer player) : base(player) { }
        public DefinedRole Role => MyRole;
        ModAbilityButton? TestButton;
        void RuntimeAssignable.OnActivated()
        {
            TestButton = NebulaAPI.Modules.AbilityButton(
                this,MyPlayer,VirtualKeyInput.Ability,10,"TEST",null,_=>true,_=>true
                );
            TestButton.OnClick = _ => {
                PatchManager.MutePlayer(MyPlayer,1145141919);
            };
        }

    }
}