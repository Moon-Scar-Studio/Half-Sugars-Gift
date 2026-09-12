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
        /// <summary>是否已获得击杀能力（有非船员阵营被潜行者拦下时获得）。由房主判定后通过 RPC 同步。</summary>
        bool CanKill = false;

        void RuntimeAssignable.OnActivated()
        {
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
                _ => !MyPlayer.IsDead && CanKill && playerTracker.CurrentTarget != null,
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
        }

        /// <summary>
        /// 拦截非船员阵营的胜利。只要潜行者还活着（用 MyPlayer.IsDead 判断——
        /// 原实现用 PlayerDieEvent 维护的 _isAlive，而放逐不会触发 PlayerDieEvent，
        /// 导致潜行者被投出后仍然永久拦截，游戏无法结束）。
        /// </summary>
        [OnlyHost]
        void OnEndCriteriaPreMet(EndCriteriaPreMetEvent ev)
        {
            if (MyPlayer.IsDead) return;
            var crewmateEnd = NebulaGameEnds.CrewmateGameEnd.Get();
            if (ev.GameEnd == crewmateEnd) return;
            ev.Reject();
            // 有阵营本该胜利却被拦下：潜行者获得击杀能力
            if (CanKillConfig && !CanKill) RpcSetCanKill.Invoke(MyPlayer.PlayerId);
        }

        static readonly RemoteProcess<byte> RpcSetCanKill = new("HSG.Lurker.SetCanKill", (playerId, _) =>
        {
            if (GamePlayer.GetPlayer(playerId)?.Role is Instance inst)
            {
                inst.CanKill = true;
                if (inst.AmOwner) AmongUsUtil.PlayQuickFlash(Cor.LurkerCor);
            }
        });

        [OnlyHost]
        void NeedWin(PlayerDieEvent ev)
        {
            var alivePlayers = GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected).ToList();
            int crewCount = alivePlayers.Count(p => p.Role.Role.Category == RoleCategory.CrewmateRole);
            if (alivePlayers.Count == crewCount) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnds.CrewmateGameEnd, GameEndReason.Situation);
        }
    }
}
