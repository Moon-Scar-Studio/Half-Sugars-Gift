
namespace NebulaN.Scripts.Roles.Crewmate;


public class MaskedDancer : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    static IntegerConfiguration PartyUses = NebulaAPI.Configurations.Configuration(
        "options.role.maskeddancer.partyuses", (1, 10), 2, null, null);

    static FloatConfiguration InviteCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.maskeddancer.inviteCooldownA", (0f, 60f, 2.5f), 25f,
        FloatConfigurationDecorator.Second, null, null);
    static FloatConfiguration StartCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.maskeddancer.startcooldown", (10f, 120f, 5f), 30f,
        FloatConfigurationDecorator.Second, null, null);

    MaskedDancer() : base(
        "maskedDancer", new Virial.Color(0.5f, 0.8f, 1f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        new Virial.Configuration.IConfiguration[] {
            PartyUses,  StartCooldown
        })
    {
       ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/MaskedDancer.png")?.AsImage(115f);
    }
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/MaskedDancerIcon.png")?.AsImage();

    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("MaskedDancerInvite.png")?.AsImage(100f),
            "role.maskeddancer.doc.invite");
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("MaskedDancerStart.png")?.AsImage(100f),
            "role.maskeddancer.doc.start");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new AssignableDocumentReplacement("%USES%", (PartyUses).ToString());
    }

    public static readonly MaskedDancer MyRole = new MaskedDancer();
    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        void IGameOperator.OnReleased() { }
        IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => Array.Empty<IPlayerAbility>();

        int UsesLeft;
        List<GamePlayer> InvitePlayers = new();
        bool PartyCanUse = false;

        List<PoolablePlayer> invitedIcons = new();
        Dictionary<byte, PoolablePlayer> iconDict = new();
        GameObject? iconHolder;

        public Instance(GamePlayer player) : base(player) { }
        public DefinedRole Role => MyRole;
        private void UpdateIconsLayout()
        {
            for (int i = 0; i < invitedIcons.Count; i++)
            {
                var icon = invitedIcons[i];
                if (icon){icon.transform.localPosition = new Vector3(-0.5f + i * 0.35f, 0f, 0f); }
            }
        }

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            UsesLeft = PartyUses;
            InvitePlayers.Clear();
            PartyCanUse = false;
            iconHolder = HudContent.InstantiateContent("MaskedDancerIcons", true, true).gameObject;
            invitedIcons.Clear();
            iconDict.Clear();

            Image inviteIcon = NebulaAPI.AddonAsset.GetResource("MaskedDancerInvite.png")?.AsImage(100f);
            Image startIcon = NebulaAPI.AddonAsset.GetResource("MaskedDancerStart.png")?.AsImage(100f);

            var playerTracker = NebulaAPI.Modules.PlayerTracker(this,MyPlayer);
            playerTracker.SetColor(MyRole.RoleColor);
            var invite = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, 0f,
                "maskedDancer.invite", inviteIcon,
                (ModAbilityButton _) => playerTracker.CurrentTarget != null
                    && playerTracker.CurrentTarget != MyPlayer
                    && !InvitePlayers.Contains(playerTracker.CurrentTarget)
                    && InvitePlayers.Count < 3,
                (button) => !MyPlayer.IsDead && UsesLeft > 0,
                false
            );

            invite.OnClick = (button) =>
            {
                var targetLike = playerTracker.CurrentTarget;
                if (targetLike == null) return;
                var invitedPlayer = targetLike.RealPlayer;
                if (InvitePlayers.Contains(invitedPlayer)) return;
                InvitePlayers.Add(invitedPlayer);
                PlayerControl pc = null;
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (p.PlayerId == invitedPlayer.PlayerId)
                    {
                        pc = p;
                        break;
                    }
                }
                if (pc != null && iconHolder != null)
                {
                    var icon = AmongUsUtil.GetPlayerIcon(
                        pc.Data.DefaultOutfit, 
                        iconHolder.transform,
                        Vector3.zero,
                        Vector3.one * 0.31f,
                        false, true
                    );
                    icon.ToggleName(false);
                    icon.SetAlpha(0.5f);
                    int i=InvitePlayers.Count - 1;
                    icon.transform.localPosition = new Vector3(i*0.29f-0.3f,-0.1f,-i*0.01f);
                    invitedIcons.Add(icon);
                    iconDict[invitedPlayer.PlayerId] = icon;
                }
                 button.StartCoolDown();
                button.CoolDownTimer = NebulaAPI.Modules.Timer(this,InviteCooldown).SetAsAbilityTimer().Start(InviteCooldown);
                UpdateIconsLayout();
            };
            var startBtn = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.SecondaryAbility,
                StartCooldown, "maskedDancer.start", startIcon,
                (ModAbilityButton _) => InvitePlayers.Count == 3 && PartyCanUse && !MyPlayer.IsDead,
                (button) => !MyPlayer.IsDead && UsesLeft > 0,
                false
            );

            startBtn.ShowUsesIcon(4, UsesLeft.ToString());

            startBtn.OnClick = (button) =>
            {
                if (InvitePlayers.Count != 3 || !PartyCanUse) return;

                int a = 0, b = 0, c = 0;
                foreach (var p in InvitePlayers)
                {
                    switch (p.Role.Role.Category)
                    {
                        case RoleCategory.CrewmateRole: a++; break;
                        case RoleCategory.ImpostorRole: b++; break;
                        case RoleCategory.NeutralRole: c++; break; // 吓哭了c++。
                    }
                }

                // 成就：CustomAchievement.dat 里声明了 maskedDancer.* 三个成就，但此前没有任何代码触发它们
                new StaticAchievementToken("maskedDancer.party");
                if (a == 1 && b == 1 && c == 1)
                {
                    var target = InvitePlayers[UnityEngine.Random.Range(0, InvitePlayers.Count)];
                    target.Suicide(State.PartyAccident, null, KillParameter.NormalKill, null);
                    new StaticAchievementToken("maskedDancer.accident");
                }
                else if (a == 3) {/*喵。*/}
                else if (a == 2)
                {
                    foreach (var p in InvitePlayers)
                    {
                        if (p.Role.Role.Category != RoleCategory.CrewmateRole)
                        {
                            p.Suicide(State.PartyAccident, null, KillParameter.NormalKill, null);
                            new StaticAchievementToken("maskedDancer.accident");
                            break;
                        }
                    }
                }
                else if (a == 0)
                {
                    MyPlayer.Suicide(State.PartyAccident, null, KillParameter.NormalKill, null);
                    new StaticAchievementToken("maskedDancer.suicide");
                }
                else
                {
                    var target = InvitePlayers[UnityEngine.Random.Range(0, InvitePlayers.Count)];
                    target.Suicide(State.PartyAccident, null, KillParameter.NormalKill, null);
                    new StaticAchievementToken("maskedDancer.accident");
                }
                foreach (var icon in invitedIcons)
                    if (icon) GameObject.Destroy(icon.gameObject);
                invitedIcons.Clear();
                iconDict.Clear();
                InvitePlayers.Clear();
                PartyCanUse = false;
                UsesLeft--;
                button.UpdateUsesIcon(UsesLeft.ToString());
            };

            GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev =>
            {
                if (AmOwner && !MyPlayer.IsDead && InvitePlayers.Count == 3)
                    PartyCanUse = true;

                var deadList = InvitePlayers.Where(p => p.IsDead).ToList();
                foreach (var dead in deadList)
                {
                    if (iconDict.TryGetValue(dead.PlayerId, out var icon))
                    {
                        if (icon) GameObject.Destroy(icon.gameObject);
                        iconDict.Remove(dead.PlayerId);
                        invitedIcons.Remove(icon);
                    }
                    InvitePlayers.Remove(dead);
                }
                UpdateIconsLayout();
            }, this);
        }


    }
}