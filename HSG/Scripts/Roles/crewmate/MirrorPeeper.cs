namespace NebulaN.Roles.Crewmate;
    public class MirrorPeeper : DefinedRoleTemplate, DefinedRole, HasCitation, IAssignableDocument
{
        static FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
            "options.role.mirrorpeeper.cooldown",
            (5f, 45f, 2.5f),
            15f,
            FloatConfigurationDecorator.Second
            );
        static IntegerConfiguration MaxUses = NebulaAPI.Configurations.Configuration(
            "options.role.mirrorpeeper.maxUses",
            (1, 10),
            3
            );
        static BoolConfiguration GueWillQ = NebulaAPI.Configurations.Configuration(
        "options.role.mirrorpeeper.guewillq",
        true
        );
        static BoolConfiguration DieWillQ = NebulaAPI.Configurations.Configuration(
        "options.role.mirrorpeeper.DieWillQ",
        true
        );
        static BoolConfiguration TaskOverWillQ = NebulaAPI.Configurations.Configuration(
        "options.role.mirrorpeeper.towq",
        true
        );
        static ValueConfiguration<int> GueCordie = NebulaAPI.Configurations.Configuration( // 误赌
        "options.role.mirrorpeeper.gcd",
        new[] { "options.role.mirrorpeeper.red", "options.role.mirrorpeeper.yellow", "options.role.mirrorpeeper.cyan", "options.role.mirrorpeeper.green", "options.role.mirrorpeeper.blue", "options.role.mirrorpeeper.purple", "options.role.mirrorpeeper.wishY" },
        3,
        () => GueWillQ
        );
        static ValueConfiguration<int> GueCorMurDie = NebulaAPI.Configurations.Configuration( // 赌杀
        "options.role.mirrorpeeper.gcmd",
        new[] { "options.role.mirrorpeeper.red", "options.role.mirrorpeeper.yellow", "options.role.mirrorpeeper.cyan", "options.role.mirrorpeeper.green", "options.role.mirrorpeeper.blue", "options.role.mirrorpeeper.purple", "options.role.mirrorpeeper.wishY" },
        2,
        () => GueWillQ
        );

        static ValueConfiguration<int> TaskCor = NebulaAPI.Configurations.Configuration(
        "options.role.mirrorpeeper.tc",
        new[] { "options.role.mirrorpeeper.red", "options.role.mirrorpeeper.yellow", "options.role.mirrorpeeper.cyan", "options.role.mirrorpeeper.green", "options.role.mirrorpeeper.blue", "options.role.mirrorpeeper.purple", "options.role.mirrorpeeper.wishY" },
        6,
        () => TaskOverWillQ
        );

        static ValueConfiguration<int> DieCor = NebulaAPI.Configurations.Configuration(
        "options.role.mirrorpeeper.dc",
        new[] { "options.role.mirrorpeeper.red", "options.role.mirrorpeeper.yellow", "options.role.mirrorpeeper.cyan", "options.role.mirrorpeeper.green", "options.role.mirrorpeeper.blue", "options.role.mirrorpeeper.purple", "options.role.mirrorpeeper.wishY" },
        0,
        () => DieWillQ
        );

        private MirrorPeeper() : base(
            "mirrorpeeper",
            Cor.MPCor,
            RoleCategory.CrewmateRole,
            NebulaTeams.CrewmateTeam,
            new IConfiguration[] { Cooldown, MaxUses, GueWillQ , DieWillQ, TaskOverWillQ, GueCordie, GueCorMurDie, TaskCor, DieCor })
        {

        }

        public static MirrorPeeper MyRole = new();

        public Citation Citation => Citations.hvtXsvc_hsg;
        bool IAssignableDocument.HasTips => true;
        bool IAssignableDocument.HasAbility => true;

        IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
        {
            yield return new AssignableDocumentImage(
                NebulaAPI.AddonAsset.GetResource("Monitoring.png")?.AsImage(115),
                "role.mirrorpeeper.doc.Monitoring"
                );
        }
    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        if (DieWillQ)
        {
            string colorName = GetColorName(DieCor.GetValue());
            yield return new AssignableDocumentReplacement("%DeathFlash_1%", Language.Translate("role.mirrorpeeper.desc.death.prefix"));
            yield return new AssignableDocumentReplacement("%DeathFlashColor%", colorName);
            yield return new AssignableDocumentReplacement("%DeathFlash_2%", Language.Translate("role.mirrorpeeper.desc.death.suffix"));
        }
        else
        {
            yield return new AssignableDocumentReplacement("%DeathFlash_1%", "");
            yield return new AssignableDocumentReplacement("%DeathFlashColor%", "");
            yield return new AssignableDocumentReplacement("%DeathFlash_2%", "");
        }
        if (TaskOverWillQ)
        {
            string colorName = GetColorName(TaskCor.GetValue());
            yield return new AssignableDocumentReplacement("%TaskFlash_1%", Language.Translate("role.mirrorpeeper.desc.task.prefix"));
            yield return new AssignableDocumentReplacement("%TaskFlashColor%", colorName);
            yield return new AssignableDocumentReplacement("%TaskFlash_2%", Language.Translate("role.mirrorpeeper.desc.task.suffix"));
        }
        else
        {
            yield return new AssignableDocumentReplacement("%TaskFlash_1%", "");
            yield return new AssignableDocumentReplacement("%TaskFlashColor%", "");
            yield return new AssignableDocumentReplacement("%TaskFlash_2%", "");
        }
        if (GueWillQ)
        {
            string goodColor = GetColorName(GueCorMurDie.GetValue());
            yield return new AssignableDocumentReplacement("%GuessCorrectFlash_1%", Language.Translate("role.mirrorpeeper.desc.guess.correct.prefix"));
            yield return new AssignableDocumentReplacement("%GuessCorrectFlashColor%", goodColor);
            yield return new AssignableDocumentReplacement("%GuessCorrectFlash_2%", Language.Translate("role.mirrorpeeper.desc.guess.correct.suffix"));

            string badColor = GetColorName(GueCordie.GetValue());
            yield return new AssignableDocumentReplacement("%GuessWrongFlash_1%", Language.Translate("role.mirrorpeeper.desc.guess.wrong.prefix"));
            yield return new AssignableDocumentReplacement("%GuessWrongFlashColor%", badColor);
            yield return new AssignableDocumentReplacement("%GuessWrongFlash_2%", Language.Translate("role.mirrorpeeper.desc.guess.wrong.suffix"));
        }
        else
        {
            yield return new AssignableDocumentReplacement("%GuessCorrectFlash_1%", "");
            yield return new AssignableDocumentReplacement("%GuessCorrectFlashColor%", "");
            yield return new AssignableDocumentReplacement("%GuessCorrectFlash_2%", "");
            yield return new AssignableDocumentReplacement("%GuessWrongFlash_1%", "");
            yield return new AssignableDocumentReplacement("%GuessWrongFlashColor%", "");
            yield return new AssignableDocumentReplacement("%GuessWrongFlash_2%", "");
        }
    }

    string GetColorName(int index)
        {
            switch (index)
            {
                case 0: return Language.Translate("options.role.mirrorpeeper.red");
                case 1: return Language.Translate("options.role.mirrorpeeper.yellow");
                case 2: return Language.Translate("options.role.mirrorpeeper.cyan");
                case 3: return Language.Translate("options.role.mirrorpeeper.green");
                case 4: return Language.Translate("options.role.mirrorpeeper.blue");
                case 5: return Language.Translate("options.role.mirrorpeeper.purple");
                case 6: return Language.Translate("options.role.mirrorpeeper.wishY");
                default: return Language.Translate("options.role.mirrorpeeper.UNKNOWN");
            }
        }

        public RuntimeRole CreateInstance(GamePlayer player, int[] args) => new Instance(player, args.Length > 0 ? args[0] : MaxUses);
     
        public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
        {
            int usesLeft;
            ModAbilityButton? Monitoring;
            bool UseCheck = false;
            bool isCheckTime = false;
            static Image MonitoringImage = NebulaAPI.AddonAsset.GetResource("Monitoring.png")?.AsImage(100);
            public Instance(GamePlayer player, int initUses) : base(player) => usesLeft = initUses;
            public DefinedRole Role => MyRole;

            void RuntimeAssignable.OnActivated()
            {
                if (!AmOwner) return;
                Monitoring = NebulaAPI.Modules.AbilityButton(
                    this,
                    MyPlayer,
                    VirtualKeyInput.Ability,
                    Cooldown,
                    "mirrorpeeper.Monitoring",
                    MonitoringImage,
                    _ => usesLeft > 0 && !isCheckTime && !UseCheck,
                    _ => !MyPlayer.IsDead
                );
                Monitoring.ShowUsesIcon(4, usesLeft.ToString());

                Monitoring.OnClick = _ =>
                {
                    if (usesLeft <= 0) return;
                    usesLeft--;
                    Monitoring.UpdateUsesIcon(usesLeft.ToString());
                    UseCheck = true;
                    Monitoring.StartCoolDown();

                };
            }
            Virial.Color wishY = new Virial.Color(1,0.9f,0.6f);
            Virial.Color GetColor(ValueConfiguration<int> vc)
            {
                switch (vc.GetValue())
                {
                    case 0: return Cor.impRed;
                    case 1: return Cor.Yellow;
                    case 2: return Cor.cyan;
                    case 3: return Cor.green;
                    case 4: return Cor.blue;
                    case 5: return new Virial.Color(0.5f, 0, 0.5f);
                    case 6: return wishY;
                    default: return Cor.White;
                }
            }
            
            [Local]
            void OnMettStart(MeetingStartEvent ev)
            {
                if (!AmOwner) return;
            }

            [Local]
            void OnMettEnd(MeetingEndEvent ev)
            {
                if (!AmOwner) return;
                if (UseCheck)
                {
                    UseCheck = false;
                    isCheckTime = true;
                }
                else if (isCheckTime) isCheckTime = false;
                
            }
            void TaskOver(PlayerTaskCompleteEvent ev)
            {
                if (!AmOwner || ev.Player == MyPlayer || !isCheckTime || !TaskOverWillQ) return;
                AmongUsUtil.PlayQuickFlash(GetColor(TaskCor));
            }

            void PlayerDie(PlayerDieEvent ev)
            {
                if (!AmOwner || ev.Player == MyPlayer || !isCheckTime || !DieWillQ || MeetingHud.Instance) return;
                AmongUsUtil.PlayQuickFlash(GetColor(DieCor));
            }

            void MayGuessed(PlayerMurderedEvent ev)
            {
                if (!AmOwner || !isCheckTime || ev.Dead == MyPlayer || !MeetingHud.Instance || !GueWillQ) return;
                // 赌错 = 赌怪自杀（Murderer == Dead）→ "误赌"色；赌对 = 赌怪杀死别人 → "赌杀"色
                if (ev.Murderer == ev.Dead) AmongUsUtil.PlayQuickFlash(GetColor(GueCordie));
                else AmongUsUtil.PlayQuickFlash(GetColor(GueCorMurDie));
            }
        }
    }