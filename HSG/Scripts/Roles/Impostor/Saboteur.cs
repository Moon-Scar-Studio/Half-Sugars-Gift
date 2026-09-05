namespace NebulaN.Roles.Impostor;

public class Saboteur : DefinedSingleAbilityRoleTemplate<Saboteur.Ability>, HasCitation, DefinedRole,IAssignableDocument
{
    Saboteur() : base("saboteur", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam, [KillCoolDown, ReducePerSabotage, ReducePerKillDuringSabotage, MinCoolDown, MaxStack])
    {

        ConfigurationHolder.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/SaboImage.png")?.AsImage(115f);
    }
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/SaboteurIcon.png")?.AsImage();
    static IRelativeCooldownConfiguration KillCoolDown = NebulaAPI.Configurations.KillConfiguration(
        "options.role.saboteur.killcooldown",CoolDownType.Relative,
        (0f, 60f, 2.5f),
        30f,
        (-40f, 40f, 2.5f),
        +5f,
        (0.125f, 2f, 0.125f),
        1.25f
    );

    static FloatConfiguration ReducePerSabotage = NebulaAPI.Configurations.Configuration(
        "options.role.saboteur.reduceSabotage",
        (0f, 20f, 0.5f),
        5f,
        FloatConfigurationDecorator.Second
    );

    static FloatConfiguration ReducePerKillDuringSabotage = NebulaAPI.Configurations.Configuration(
        "options.role.saboteur.reduceKill",
        (0f, 20f, 0.5f),
        5f,
        FloatConfigurationDecorator.Second
    );

    static FloatConfiguration MinCoolDown = NebulaAPI.Configurations.Configuration(
        "options.role.saboteur.minCooldown",
        (0f, 60f, 2.5f),
        5f,
        FloatConfigurationDecorator.Second
    );

    static IntegerConfiguration MaxStack = NebulaAPI.Configurations.Configuration(
        "options.role.saboteur.maxStack",
        (0, 10),
        3
    );

    static int DestroyCount;

    public static readonly Saboteur MyRole = new Saboteur();

    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.Killers;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.AsUniqueKillAbility;
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        bool IPlayerAbility.HideKillButton => killButton != null && !killButton.IsBroken;
        public Ability(GamePlayer player) : base(player,false)
        {
            if (!AmOwner) return;
            
            var tracker = ObjectTrackers.ForPlayer(
                this,
                null,
                MyPlayer,
                ObjectTrackers.LocalKillablePredicate,
                Palette.ImpostorRed,
                Nebula.Roles.Impostor.Impostor.CanKillHidingPlayerOption,
                false
            );
            tracker.SetColor(Cor.impRed);

            killButton = NebulaAPI.Modules.AbilityButton(this,MyPlayer,true,false,
                VirtualKeyInput.Kill,null,KillCoolDown.Cooldown, "kill",null, _ => tracker.CurrentTarget != null,_ => !MyPlayer.IsDead,false
            ).SetLabelType(ModAbilityButton.LabelType.Impostor);

            killButton.OnClick = button =>
            {
                var target = tracker.CurrentTarget;
                if (target == null) return;

                var cancelable = GameOperatorManager.Instance?.Run(
                    new PlayerTryVanillaKillLocalEventAbstractPlayerEvent(MyPlayer, target)
                );
                if (cancelable?.IsCanceled ?? false) return;
                MyPlayer.MurderPlayer(target,PlayerState.Dead,EventDetail.Kill,KillParameter.NormalKill,
                    result =>
                    {
                        if (result == KillResult.Kill)
                        {
                            ResetCooldown(button);
                            NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                        }
                    }
                );
            };

            NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
        }

        ModAbilityButton? killButton;

        bool lastSab;
        bool sabotage;
        float temporaryReduce;

        float CurrentReduce => DestroyCount * ReducePerSabotage + temporaryReduce;

        float CurrentCooldown()
        {
            return Mathf.Max(MinCoolDown,KillCoolDown.Cooldown - CurrentReduce);
        }

        void ResetCooldown(ModAbilityButton button)
        {
            button.CoolDownTimer = NebulaAPI.Modules.Timer(this,CurrentCooldown()).SetAsKillCoolTimer().Start();
        }

        [Local]
        void OnUpdate(UpdateEvent ev)
        {
            bool current = AmongUsUtil.InAnySab;
            if (current && !lastSab)
            {
                sabotage = true;
                temporaryReduce = 0f;
            }
            if (!current && lastSab)
            {
                if (sabotage)
                {
                    DestroyCount = Mathf.Min(DestroyCount + 1,MaxStack);
                }
                sabotage = false;
                temporaryReduce = 0f;
            }
            lastSab = current;
        }

        [Local]
        void OnKill(PlayerKillPlayerEvent ev)
        {
            if (ev.Murderer.AmOwner && sabotage)
            {
                temporaryReduce += ReducePerKillDuringSabotage;
            }
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            DestroyCount = 0;
        }
        int[] IPlayerAbility.AbilityArguments => [];
    }
}