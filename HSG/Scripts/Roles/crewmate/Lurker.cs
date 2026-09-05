namespace NebulaN.Roles.Crewmate;

public class Lurker : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    static BoolConfiguration CanKillConfig = NebulaAPI.Configurations.Configuration(
    "options.role.lurker.ckc",
    true
    );
    static FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.lurker.cooldown",
        (5f, 60f, 3f),
        25,
        FloatConfigurationDecorator.Second,
        () => CanKillConfig
    );
    static BoolConfiguration NoTask = NebulaAPI.Configurations.Configuration(
        "options.role.lurker.nt",
        true
        );


    Lurker() : base(
        "lurker",
        Cor.LurkerCor,
        RoleCategory.CrewmateRole,
        NebulaTeams.CrewmateTeam,
        new Virial.Configuration.IConfiguration[] { Cooldown, NoTask, CanKillConfig }
    )
    {
        // ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/Lurker.png")?.AsImage(115f);
    }
    // Virial.Media.Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/LurkerIcon.png")?.AsImage();

    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    public static readonly Lurker MyRole = new Lurker();
    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new AssignableDocumentReplacement("%CD%", Cooldown.GetValue().ToString());
        yield return new AssignableDocumentReplacement("%CANKILL%", CanKillConfig ? Language.Translate("role.lurker.ck.true") : Language.Translate("role.lurker.ck.false"));
        yield return new AssignableDocumentReplacement("%NT%", NoTask ? Language.Translate("role.lurker.nt.true") : Language.Translate("role.lurker.nt.false"));
    }
    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        void IGameOperator.OnReleased() { }
        IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => Array.Empty<IPlayerAbility>();
        bool RuntimeAssignable.InvalidateCrewmateTask => NoTask;
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }
        ModAbilityButton? Btn;
        static public bool CanKill = false;
        private bool _isAlive = true;
        void RuntimeAssignable.OnActivated()
        {
            ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ; ;
            if (!AmOwner) return;
            var playerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
            playerTracker.SetColor(Cor.impRed);
            Btn = NebulaAPI.Modules.AbilityButton(
                this,
                MyPlayer,
                VirtualKeyInput.Kill,
                Cooldown,
                "lurker.kill",
                null,
                _ => !MyPlayer.IsDead && CanKill,
                _ => !MyPlayer.IsDead && CanKillConfig,
                false
            );
            Btn.OnClick = (button) =>
            {
                var target = playerTracker.CurrentTarget;
                if (target != null)
                {
                    MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill, KillCondition.NormalKill);
                }
                button.StartCoolDown();
            };
            GameOperatorManager.Instance?.Subscribe<EndCriteriaPreMetEvent>(OnEndCriteriaPreMet, this);
        }
        [OnlyHost]
        private void OnEndCriteriaPreMet(EndCriteriaPreMetEvent ev)
        {
            if (!_isAlive) return;
            var crewmateEnd = NebulaGameEnds.CrewmateGameEnd.Get();
            if (ev.GameEnd == crewmateEnd) return;
            ev.Reject();
        }
        static public RemoteProcess RpcSetBool = new("SetBool_H", _ =>
        {
            CanKill = true;
        });
        [OnlyHost]
        void NeedWin(PlayerDieEvent ev)
        {
            if (ev.Player == MyPlayer)
            {
                // 只是不想不使用参数。
                // 不用的话IDE骚扰我让我用。
                // 我好像可以PlayerDieEvent _
                // 这样就不会骚扰了。
                // 懒得整了。
                // 不改了。
            }
            var AlivePlayers = GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected).ToList();
            int CrewCount = AlivePlayers.Count(p => p.Role.Role.Category == RoleCategory.CrewmateRole);
            if (AlivePlayers.Count == CrewCount) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnds.CrewmateGameEnd, GameEndReason.Situation);
        }
        void EraseCanKill(GameStartEvent ev) => CanKill = false;
        [OnlyMyPlayer]
        void OnDie(PlayerDieEvent ev)
        {
            if (ev.Player == MyPlayer)
                _isAlive = false;
        }
        [OnlyMyPlayer]
        void OnGameStart(GameStartEvent ev)
        {
            _isAlive = true;
            CanKill = false;
        }
    }
}