//using NebulaN.Roles.Modifier;

//namespace NebulaN.Roles.Crewmate;
//public class Miraclist: DefinedRoleTemplate, DefinedRole, HasCitation, IAssignableDocument,
//    RuntimeAssignableGenerator<RuntimeRole>
//{
//    static IntegerConfiguration FateWeaveCanSelectRoles = NebulaAPI.Configurations.Configuration(
//        "options.role.miraclist.FateWeaveCanSelectRoles",(1,10,1),3
//        );
//    /*static BoolConfiguration HasChatChannel = NebulaAPI.Configurations.Configuration(
//        "options.role.miraclist.HasChatChannel", true
//        );*/
//    static BoolConfiguration WillPublicRole = NebulaAPI.Configurations.Configuration(
//       "options.role.miraclist.WillPublicRole", true
//       );
//    public static BoolConfiguration CanBeGuessedAfterPublicRole = NebulaAPI.Configurations.Configuration(
//       "options.role.miraclist.CanBeGuessedAfterPublicRole", false,()=>WillPublicRole
//       );
//    static ValueConfiguration<int> HowToKnowMiracleDie = NebulaAPI.Configurations.Configuration(
//        "options.role.miraclist.HowToKnowMiracleDie", new[] { 
//            "options.role.miraclist.NoReport","options.role.miraclist.KnowKiller","options.role.miraclist.Flash"
//        }, 0
//        );
//    static ValueConfiguration<int> HowToKnowDSDie = NebulaAPI.Configurations.Configuration(
//        "options.role.miraclist.HowToKnowDSDie", new[] { "options.role.miraclist.NoReport", "options.role.miraclist.KnowKiller", "options.role.miraclist.Flash" }, 0
//        );
//    Miraclist() : base("miraclist", Cor.lightYellow, RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam, 
//        [FateWeaveCanSelectRoles,
//        //HasChatChannel,
//        WillPublicRole,
//        CanBeGuessedAfterPublicRole,
//        HowToKnowDSDie,
//        HowToKnowMiracleDie
//        ]
//    )
//    {

//    }
//    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
//    public Citation Citation => Citations.hvtXsvc_hsg;
//    public static readonly Miraclist MyRole = new();
//    bool IAssignableDocument.HasTips => true;
//    bool IAssignableDocument.HasAbility => true;

//    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
//    {
//        yield return new AssignableDocumentImage(
//            NebulaAPI.AddonAsset.GetResource("MeetingButton/DivineSight.png")?.AsImage(115),
//            "role.miraclist.doc.DivineSight"
//            );
//        yield return new AssignableDocumentImage(
//            NebulaAPI.AddonAsset.GetResource("FateWeave.png")?.AsImage(115),
//            "role.miraclist.doc.FateWeave"
//            );
//    }
//    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
//    {
//        bool UseSight = false;
//        public bool HaveMT = false;
//        static public bool HasMiracle = false;
//        static public GamePlayer? MiraclePlayer;
//        static bool CanChooseRole = false;
//        public Instance(GamePlayer player) : base(player) { }
//        static List<DefinedRole> MiracleRoles = new();
//        static bool FateWeaveUsed = false;
//        static bool NeedPulicRole = false;
//        public DefinedRole Role => MyRole;
//        Image? DivineSightSprite = NebulaAPI.AddonAsset.GetResource("MeetingButton/DivineSight.png").AsImage(100f);
//        Image? FateWeaveSprite = NebulaAPI.AddonAsset.GetResource("FateWeave.png").AsImage(100f);
//        public void OnActivated()
//        {

//        }
//        void RuntimeAssignable.OnActivated()
//        {
//            var FateWeave = NebulaAPI.Modules.AbilityButton(
//                this, MyPlayer, VirtualKeyInput.Ability, 0f,
//                "miraclist.fateweave", FateWeaveSprite,
//                (ModAbilityButton _) => HasMiracle && CanChooseRole,
//                (button) => AmOwner && !FateWeaveUsed,
//                false
//            );
//            FateWeave.OnClick = (button) =>
//            {
//                if (!AmOwner || FateWeaveUsed) return;
//                FateWeaveUsed = true;
//                MetaScreen window = null;
//                PatchManager.OpenRoleSelectWindowUsingTabs(MiracleRoles, [("game.role.crewmate",r=>r.Category.HasFlag(RoleCategory.CrewmateRole))],
//                    false,Language.Translate("role.miraclist.fatechoose"),role => 
//                    {
//                        RpcMiracleChooseRole.Invoke((MiraclePlayer.PlayerId,role.InternalName));
//                        NeedPulicRole = true;
                        
//                    },ref window,true
//                );
//            };

//        }
//        public static RemoteProcess<(byte playerId, string roleName)> RpcMiracleChooseRole =
//            new("RpcMiracleChooseRole", (msg, _) =>
//            {
//                var player = GamePlayer.GetPlayer(msg.playerId);
//                if (player == null) return;
//                var role = Nebula.Roles.Roles.AllRoles.FirstOrDefault(r => r.InternalName == msg.roleName);
//                if (role == null) return;
//                player.SetRole(role);
//                player.AddModifier(DivineSeed.MyRole);
//                if (player.AmOwner)
//                {
//                    AmongUsUtil.PlayQuickFlash(Cor.Golden);
//                }
//            });
//        [Local]
//        void OnGameEnd(GameEndEvent ev)
//        {
//            HasMiracle = false;
//            MiraclePlayer = null;
//            CanChooseRole = false;
//            FateWeaveUsed = false;
//            NeedPulicRole = false;
//        }
//        void PublicRole(PlayerVoteCastEvent ev)
//        {
//            if (!NeedPulicRole) return;
//            if (!WillPublicRole) return;
//            MyPlayer.AddModifier(PublicMiraclist.MyRole);
//            NeedPulicRole = false;
//        }
//        void OnMeetingStart(MeetingStartEvent ev)
//        {
//            if (UseSight) return;
//            var DivineSight = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
//            DivineSight?.RegisterMeetingAction(new(
//                DivineSightSprite,
//                state =>
//                {
//                    var target = state.MyPlayer;
//                    if (target == null || target == MyPlayer) return;
//                    target.AddModifier(Modifier.MiracleTouched.MyRole);
//                    HaveMT = true;
//                    UseSight = true;
//                },
//                p => !UseSight && !p.MyPlayer.IsDead && !p.MyPlayer.AmOwner
//            ));
//        }
//        /*void PlayerDie(PlayerMurderedEvent ev)
//        {
//            if(ev.Murderer.TryGetModifier<MiracleTouched.Instance>(out _))
//            {
//                if (HaveMT)
//                {
//                    if(ev.Murderer.Role.Role.Category != RoleCategory.CrewmateRole)
//                    {
//                        if (ev.Murderer.TryGetModifier<MiracleTouched.Instance>(out _))
//                        {
//                            AmongUsUtil.PlayQuickFlash(Cor.cyan);
//                            HaveMT = false;
//                            HasMiracle = false;
//                            MyPlayer.SetRole(Nebula.Roles.Crewmate.Crewmate.MyRole);
//                        }
//                    }
//                }
//            }
//        }*/
//        void PlayerDie(PlayerMurderedEvent ev)
//        {
//            if (!ev.Murderer.TryGetModifier<MiracleTouched.Instance>(out _)) return;
//            if (!HaveMT) return;
//            if (ev.Murderer.Role.Role.Category == RoleCategory.CrewmateRole) return;
//            AmongUsUtil.PlayQuickFlash(Cor.cyan);
//            HaveMT = false;
//            HasMiracle = false;
//            MyPlayer.SetRole(Nebula.Roles.Crewmate.Crewmate.MyRole);
//        }
//        // 感谢DeepSeek的屎山修复。（?
//        public static RemoteProcess<byte> RpcMiracleSet = new("RpcMiracleSet", (playerID, _) =>
//        {
//            MiraclePlayer = GamePlayer.GetPlayer(playerID);
//            HasMiracle = true;
//            CanChooseRole = true;
//        });
//        void OnGameStart(GameStartEvent ev)
//        {
//            if (AmongUsClient.Instance.AmHost)
//            {
//                MiracleRoles = FateWeaveRoleSelect(FateWeaveCanSelectRoles.GetValue());
//            }
//        }
//        [Local]
//        void OnDSDie(PlayerMurderedEvent ev)
//        {
//            if (!ev.Dead.TryGetModifier<DivineSeed.Instance>(out _)) return;
//            int htk = HowToKnowDSDie.GetValue();
//            if (htk == 0) return;
//            if(htk == 1)
//            {
//                PatchManager.SendLocalMessage(Language.Translate("role.miraclist.DSdie").Replace("%KILLER%",ev.Murderer.ToString()));
//            }
//            if(htk == 2)
//            {
//                AmongUsUtil.PlayQuickFlash(Cor.impRed);
//            }
//        }
//        [Local]
//        void OnMiracleDie(PlayerMurderedEvent ev)
//        {
//            if (!ev.Dead.TryGetModifier<Miracle.Instance>(out _)) return;
//            int htk = HowToKnowMiracleDie.GetValue();
//            switch (htk)
//            {
//                case 0:
//                    return;
//                case 1:
//                    PatchManager.SendLocalMessage(Language.Translate("role.miraclist.Mdie").Replace("%KILLER%", ev.Murderer.ToString()));
//                    return;
//                case 2:
//                    AmongUsUtil.PlayQuickFlash(Cor.Golden);
//                    AmongUsUtil.PlayQuickFlash(Cor.impRed);
//                    return;
//                default:
//                    return;
//            }
//        }
//        void DecorateMiracle(PlayerDecorateNameEvent ev)
//        {
//            if (!AmOwner) return;
//            if (!HasMiracle) return;
//            if (ev.Player != MiraclePlayer)  return;
//            ev.Name += " M".Color(Cor.lightYellow.ToUnityColor());
//        }
//        void DecorateDivineSeed(PlayerDecorateNameEvent ev)
//        {
//            if (!AmOwner) return;
//            if (!ev.Player.TryGetModifier<DivineSeed.Instance>(out _)) return;
//            ev.Name += " DS".Color(Cor.Golden.ToUnityColor());
//        }
//    }
//    public static List<DefinedRole> FateWeaveRoleSelect(int count)
//    {
//        var candidates = new List<DefinedRole>();

//        foreach (var role in Nebula.Roles.Roles.AllRoles)
//        {
//            if (role == MyRole) continue;
//            if (!role.Category.HasFlag(RoleCategory.CrewmateRole)) continue;
//            bool canCreate = false;
//            foreach (var type in AssignmentType.AllTypes)
//            {
//                if (!type.CanGuessAsAbility) continue;
//                if (type.Predicate.Invoke(role.AssignmentStatus, role) &&
//                    role.GetCustomAllocationParameters(type)?.RoleCountSum > 0)
//                {
//                    canCreate = true;
//                    break;
//                }
//            }
//            if (canCreate) candidates.Add(role);
//        }
//        candidates = candidates.OrderBy(_ => UnityEngine.Random.value).Take(count).ToList();
//        return candidates;
//    }
    
//}