//namespace NebulaN.Roles.Impostor;

//public class Overwriter : DefinedRoleTemplate, HasCitation, DefinedRole, RuntimeAssignableGenerator<RuntimeRole>,IAssignableDocument
//{

//    static FloatConfiguration IMPselCD = NebulaAPI.Configurations.Configuration(
//        "options.overwriter.impselcd",(10f,60f,2.5f),20f,FloatConfigurationDecorator.Second
//        );
//    static FloatConfiguration NOIMPselCD = NebulaAPI.Configurations.Configuration(
//        "options.overwriter.NOimpselcd", (10f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second
//        );
//    static BoolConfiguration NoNoNo_PIGGOD_NoKillME_Dream_I_WILL_KILL_YOU = NebulaAPI.Configurations.Configuration(
//        "options.overwriter.NoKillTeamMate",false);
//    static BoolConfiguration EveryKnowOW = NebulaAPI.Configurations.Configuration(
//        "options.overwriter.EveryKnowOW",true
//        );

//    private Overwriter() : base(
//        "overwriter",
//        Cor.impRed,
//        RoleCategory.ImpostorRole,
//        NebulaTeams.ImpostorTeam,
//        new Virial.Configuration.IConfiguration[] {IMPselCD ,NOIMPselCD,NoNoNo_PIGGOD_NoKillME_Dream_I_WILL_KILL_YOU,EveryKnowOW})
//    {

//    }

//    public Citation Citation => Citations.hvtXsvc_hsg;

//    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments)
//    {
//        return new Instance(player);
//    }

//    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.KillersSide;

//    public static readonly Overwriter MyRole = new();
//    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
//    {
//        yield return new AssignableDocumentImage(
//            NebulaAPI.AddonAsset.GetResource("OW.png")?.AsImage(115f),
//            "role.override.ability.doc"
//        );
//        yield return new AssignableDocumentImage(
//            NebulaAPI.AddonAsset.GetResource("IMPSEL.png")?.AsImage(115f),
//            "role.override.ability.doc2"
//        );
//        yield return new AssignableDocumentImage(
//            NebulaAPI.AddonAsset.GetResource("NoIMPSEL.png")?.AsImage(115f),
//            "role.override.ability.doc3"
//        );
//    }
//    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
//    {
//        string WillKillTeamMate = NoNoNo_PIGGOD_NoKillME_Dream_I_WILL_KILL_YOU ? Language.Translate("role.override.willkilltm.true") : Language.Translate("role.override.willkilltm.false");
//        yield return new AssignableDocumentReplacement("%NPNDIWKY%", WillKillTeamMate);

//    }
//    bool IAssignableDocument.HasTips => false;
//    bool IAssignableDocument.HasAbility => true;
//    public class Instance : RuntimeAssignableTemplate, RuntimeRole
//    {
//        public Instance(GamePlayer player) : base(player) { }
//        public DefinedRole Role => MyRole;
//        Image? OverrideImage = NebulaAPI.AddonAsset.GetResource("MeetingButton/OW.png")?.AsImage(100f);
//        Image? ImpSelectImage = NebulaAPI.AddonAsset.GetResource("ImpSelect.png")?.AsImage(100f);
//        Image? NoImpSelectImage = NebulaAPI.AddonAsset.GetResource("NoImpSelect.png")?.AsImage(100f);
//        bool UseOveriride = false;
//        float IMPCD = IMPselCD.GetValue();
//        float NOIMPCD = NOIMPselCD.GetValue();
//        ModAbilityButton? ImpSelect;
//        ModAbilityButton? NoImpSelect;
//        GamePlayer IMPp;
//        GamePlayer NoIMPp;
//        bool useImpSel;
//        bool useNoImpSel;
//        public void OnActivated()
//        {
//            var IMPtracker = NebulaAPI.Modules.PlayerTracker(this,MyPlayer,p => !p.IsDead && p.IsImpostor);
//            IMPtracker.SetColor(Cor.impRed);
//            var NOIMPtracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer, p => !p.IsDead && !p.IsImpostor);
//            NOIMPtracker.SetColor(Virial.Color.CrewmateColor);
            
//            ImpSelect = NebulaAPI.Modules.InteractButton(
//                this, MyPlayer, IMPtracker, VirtualKeyInput.Ability, null, IMPCD, "impsel", ImpSelectImage, (target, btn) => 
//                {
//                    IMPp = target;
//                    useImpSel = true;
//                },_ => true,_=>!useImpSel
//                );
//            NoImpSelect = NebulaAPI.Modules.InteractButton(
//                this, MyPlayer, NOIMPtracker, VirtualKeyInput.SecondaryAbility,null,NOIMPCD, "Noimpsel",NoImpSelectImage, (target, btn) =>
//                {
//                    NoIMPp = target;
//                    useNoImpSel = true;
//                },_=> true,_=>!useNoImpSel
//                );
//        }
//        void OnMeetingStart(MeetingStartEvent ev)
//        {
//            var Override = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
//            Override?.RegisterMeetingAction(new(
//                OverrideImage,
//                state =>
//                {
//                    if (IMPp == null || IMPp.IsDead || NoIMPp == null || NoIMPp.IsDead)
//                        return;
//                    var target = state.MyPlayer;
//                    AmongUsUtil.PlayQuickFlash(Cor.impRed);
//                    PatchManager.SendLocalMessage(Language.Translate("role.override.select").Replace("%NAME%",state.MyPlayer.PlayerName.ToString()));
//                    var impRole = IMPp.Role.Role;
//                    NoIMPp.SetRole(impRole);
//                    if (!NoNoNo_PIGGOD_NoKillME_Dream_I_WILL_KILL_YOU)
//                    {
//                        IMPp.Suicide(PlayerState.Dead,null,KillParameter.MeetingKill);
//                    }
//                    else if (NoNoNo_PIGGOD_NoKillME_Dream_I_WILL_KILL_YOU)
//                    {
//                        IMPp.SetRole(Nebula.Roles.Impostor.Impostor.MyRole);
//                    }
//                    if (EveryKnowOW)
//                    {
//                        PatchManager.RpcFlashAll.Invoke("#FF0000");

//                    }
//                    UseOveriride = true;
//                }, p => !UseOveriride && useImpSel && useNoImpSel && p.MyPlayer == IMPp &&!p.MyPlayer.IsDead && GamePlayer.LocalPlayer == MyPlayer
//            ));
//        }
//        [Local]
//        void OnDecorateName(PlayerDecorateNameEvent ev)
//        {
//            if (UseOveriride) return;
//            if (!useImpSel || !useNoImpSel) return;

//            if (ev.Player == IMPp || ev.Player == NoIMPp)
//            {
//                ev.Name = "*".Color(Cor.impRed.ToUnityColor())+ev.Name+"*".Color(Cor.impRed.ToUnityColor());
//            }
//        }
//    }
//}