

namespace NebulaN.Roles.Crewmate;

public class Dilemma : DefinedRoleTemplate, DefinedRole, RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument, HasCitation
{
    static IntegerConfiguration MaxUses = NebulaAPI.Configurations.Configuration(
        "options.role.dilemma.maxUses", (1, 10, 1), 1);
    static FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.dilemma.cooldown", (2f, 60f, 2f), 15f, FloatConfigurationDecorator.Second);
    static BoolConfiguration AddMaxUsesWhenDoTask = NebulaAPI.Configurations.Configuration(
        "options.role.dilemma.AddMaxUsesWhenDoTask", true);
    static IntegerConfiguration NeedTasksCount = NebulaAPI.Configurations.Configuration(
        "options.role.dilemma.NeedTasksCount", (1,15,1),1,()=>AddMaxUsesWhenDoTask
        );
    Dilemma() : base(
        "dilemma",
        Virial.Color.CrewmateColor,
        RoleCategory.CrewmateRole,
        NebulaTeams.CrewmateTeam,
        new IConfiguration[] { MaxUses, Cooldown, AddMaxUsesWhenDoTask,NeedTasksCount }
        )
    {

    }
    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public static readonly Dilemma MyRole = new Dilemma();



    public Citation Citation => Citations.hvtXsvc_hsg;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("Choose.png")?.AsImage(115f),
            "role.dilemma.ability.doc"
        );
    }
    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        ModAbilityButton? button;
        Image? Image=NebulaAPI.AddonAsset.GetResource("Choose.png")?.AsImage(100f);
        int usesLeft;
        int MinusTaskCount = NeedTasksCount;
        int tmp = 0;
        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            usesLeft = MaxUses;
            int Action = -1;
            button = NebulaAPI.Modules.AbilityButton(
                this,
                MyPlayer,
                VirtualKeyInput.Ability,
                Cooldown,
                "dilimma.choose",
                Image,
                _ => usesLeft > 0 && MyPlayer.CanMove,
                _ => !MyPlayer.IsDead
            );
            button.ShowUsesIcon(4, usesLeft.ToString());

            button.OnClick = _ =>
            {
                if (usesLeft <= 0) return;
                usesLeft--;
                button.UpdateUsesIcon(usesLeft.ToString());
                Action = UnityEngine.Random.Range(0,6);
                DoAction(Action);
                button.StartCoolDown();
            };
        }

        [Local]
        void DoTask(PlayerTaskCompleteLocalEvent ev)
        {
            if (ev.Player != MyPlayer) return;
            if (!AddMaxUsesWhenDoTask) return;
            tmp++;
            if (tmp >= MinusTaskCount)
            {
                usesLeft++;
                tmp = 0;
                button?.UpdateUsesIcon(usesLeft.ToString());
            }
        }
        public void DoAction(int i)
        {
            var Title = NebulaAPI.CurrentGame?.GetModule<TitleShower>();
            switch (i)
            {
                case -1:
                    break;
                case 0:
                    MyPlayer.Suicide(PlayerState.Dead, null, KillParameter.NormalKill, null);
                    NebulaManager.Instance.StartDelayAction(2.3f, () =>
                    {
                        Title?.SetText(Language.Translate("role.dilemma.hudtextSuicide"), Cor.impRed, 5f, true);
                    });
                    break;
                case 1:
                    AmongUsUtil.PlayQuickFlash(Cor.Yellow);
                    MyPlayer.RequestEmergencyMeeting(true,false);
                    break;
                case 2:
                    var impostors = GamePlayer.AllPlayers.Where(p => p.IsImpostor && !p.IsDead).ToList();
                    if (impostors.Count > 0)
                    {
                        var randomImpostor = impostors[UnityEngine.Random.Range(0, impostors.Count)];
                        randomImpostor.Suicide(PlayerState.Dead, null, KillParameter.NormalKill, null);
                        AmongUsUtil.PlayQuickFlash(Cor.impRed);
                        Title?.SetText(Language.Translate("role.dilemma.hudtextKillImp"), Cor.LurkerCor, 1.5f,true);
                    }
                    else if (impostors.Count<=0)
                    {
                        AmongUsUtil.PlayQuickFlash(Cor.green);
                        Title?.SetText(Language.Translate("role.dilemma.hudtextNoImp"), Cor.LurkerCor, 1.5f);
                    }
                    break;
                case 3:
                    MyPlayer.SetRole(Nebula.Roles.Crewmate.Crewmate.MyRole);
                    AmongUsUtil.PlayQuickFlash(Cor.cyan);
                    Title?.SetText(Language.Translate("role.dilemma.hudtextBeCrew"), Cor.LurkerCor, 1.5f,true);
                    break;
                case 4:
                    var alivePlayers = GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected).ToList();
                    if (alivePlayers.Count > 0)
                    {
                        int index = UnityEngine.Random.Range(0, alivePlayers.Count);
                        var randomPlayer = alivePlayers[index];
                        AmongUsUtil.PlayQuickFlash(Cor.SpiritCor);
                        Title?.SetText(Language.Translate("role.dilemma.hudtextAddModi"), Cor.LurkerCor, 1.5f);
                        randomPlayer.AddModifier(Modifier.KeyMaster.MyRole);
                    }
                    break;
                case 5:
                    var MyOriginalRole = MyPlayer.Role.Role;
                    var MyOriginalArg= MyPlayer.Role.RoleArguments;
                    var alivePlayers2 = GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected).ToList();
                    if (alivePlayers2.Count>0)
                    {
                        int index = UnityEngine.Random.Range(0, alivePlayers2.Count);
                        var randomPlayer = alivePlayers2[index];
                        var targetRole = randomPlayer.Role.Role;
                        // 先缓存双方参数再互换：原实现在自己 SetRole 之后才读 MyPlayer.Role.RoleArguments，拿到的是新角色的参数
                        var targetArg = randomPlayer.Role.RoleArguments;
                        MyPlayer.SetRole(targetRole, targetArg);
                        randomPlayer.SetRole(MyOriginalRole, MyOriginalArg);
                        AmongUsUtil.PlayQuickFlash(Cor.MPCor);
                        Title?.SetText(Language.Translate("role.dilemma.hudtextChangeRole"), Cor.MPCor, 1.5f,true);
                    }
                    break;
                default:
                    HsgDebug.Log($"未定义的效果: {i}");
                    Title?.SetText(Language.Translate("role.dilemma.hudtextUn"),Cor.impRed,1.5f);
                    break;
            }
            HsgDebug.Log($"抉择者取效果：{i}");
        }
    }
}