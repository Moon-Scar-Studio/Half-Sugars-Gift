namespace NebulaN.Roles.Impostor;

/// <summary>
/// 代号007（内鬼）。
/// 「向目标玩家埋下伏笔，被标记的玩家将在 n 轮后死亡，死亡前一轮受到提示」。
/// </summary>
public class Agent007 : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration DelayRoundsOption = NebulaAPI.Configurations.Configuration(
        "options.role.agent007.delayRounds", (1, 5), 2);
    static private readonly IntegerConfiguration MaxUsesOption = NebulaAPI.Configurations.Configuration(
        "options.role.agent007.maxUses", (1, 5), 2);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.agent007.cooldown", (10f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);

    private Agent007() : base(
        "agent007", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [DelayRoundsOption, MaxUsesOption, CooldownOption])
    { }

    public static readonly Agent007 MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>提示被埋伏笔的人「下一轮就要死了」。</summary>
    static internal readonly RemoteProcess<byte> RpcWarn = new("HSG.Agent007.Warn", (targetId, _) =>
    {
        if (GamePlayer.GetPlayer(targetId)?.AmOwner != true) return;
        AmongUsUtil.PlayQuickFlash(Cor.impRed);
        HalfSugarGift.Core.Patch.ExHelpers.ShowMeetingOverlay(
            "role.agent007.name", Language.Translate("role.agent007.warning"), Cor.impRed);
    });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>被埋了伏笔的人 → 还剩几轮。</summary>
        private readonly Dictionary<byte, int> foreshadows = new();
        private int usesLeft;

        void RuntimeAssignable.OnActivated()
        {
            usesLeft = MaxUsesOption;
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId && !foreshadows.ContainsKey(p.PlayerId));
            tracker.SetColor(Cor.impRed);

            var plant = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "agent007.plant", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && usesLeft > 0,
                b => !MyPlayer.IsDead && usesLeft > 0, false);
            plant.SetLabelType(ModAbilityButton.LabelType.Impostor);
            plant.ShowUsesIcon(3, usesLeft.ToString());
            plant.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                usesLeft--;
                plant.UpdateUsesIcon(usesLeft.ToString());
                // 伏笔要同步给所有客户端：倒计时与处决由房主执行，原来房主那份永远是空的
                RpcPlant.Invoke((MyPlayer.PlayerId, target.PlayerId));
                new StaticAchievementToken("agent007.plant");
                button.StartCoolDown();
            };
        }

        public void AddForeshadow(byte targetId) => foreshadows[targetId] = DelayRoundsOption;

        // 所有客户端同步递减（007 本人的名字标记也要随之消失），提示与处决只由房主发起
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            bool host = AmongUsClient.Instance.AmHost;
            foreach (var id in foreshadows.Keys.ToList())
            {
                var target = GamePlayer.GetPlayer(id);
                if (target == null || target.IsDead) { foreshadows.Remove(id); continue; }

                foreshadows[id]--;
                if (foreshadows[id] == 1) { if (host) RpcWarn.Invoke(id); }
                else if (foreshadows[id] <= 0)
                {
                    foreshadows.Remove(id);
                    if (!host) continue;
                    target.Suicide(HalfSugarGift.Core.Patch.ExState.Foreshadow, EventDetail.Kill, KillParameter.NormalKill);

                    // 「静谧无声」：队友全死了还靠伏笔拿人头
                    if (!GamePlayer.AllPlayers.Any(p => !p.IsDead && p.IsImpostor && p != MyPlayer))
                        RpcSilentAchievement.Invoke(MyPlayer.PlayerId);
                }
            }
        }

        [Local]
        void OnDecorateName(PlayerDecorateNameEvent ev)
        {
            if (!AmOwner) return;
            if (foreshadows.ContainsKey(ev.Player.PlayerId))
                ev.Name += " ⌛".Color(Cor.impRed.ToUnityColor());
        }
    }

    static internal readonly RemoteProcess<(byte agentId, byte targetId)> RpcPlant =
        new("HSG.Agent007.Plant", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.agentId)?.Role is Instance inst) inst.AddForeshadow(msg.targetId);
        });

    static internal readonly RemoteProcess<byte> RpcSilentAchievement =
        new("HSG.Agent007.Silent", (agentId, _) =>
        {
            if (GamePlayer.GetPlayer(agentId)?.AmOwner == true)
                new StaticAchievementToken("agent007.silent");
        });
}
