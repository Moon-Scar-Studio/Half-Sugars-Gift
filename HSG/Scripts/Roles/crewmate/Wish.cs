
namespace NebulaN.Roles.Crewmate;

public class Wish : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>,IAssignableDocument
{
    private static FloatConfiguration MarkCoolDown = NebulaAPI.Configurations.Configuration(
        "options.role.wish.MarkCoolDown",
        (5f, 50f, 2f),
        25f,
        FloatConfigurationDecorator.Second,
        null, null
    );

    private static IntegerConfiguration MarkMaxUses = NebulaAPI.Configurations.Configuration(
        "options.role.wish.MarkMaxUses",
        (1,5),
        2,
        null, null
    );
    static private BoolConfiguration suicideOnImpostor = NebulaAPI.Configurations.Configuration(
    "options.role.wish.suicideImpostor",
    true,
    null, null
    );

    static private BoolConfiguration suicideOnNeutral = NebulaAPI.Configurations.Configuration(
        "options.role.wish.suicideNeutral",
        false,
        null, null
    );

    static private BoolConfiguration suicideOnCrewmate = NebulaAPI.Configurations.Configuration(
        "options.role.wish.suicideCrewmate",
        false,
        null, null
    );
    private Wish() : base(
           "wish",
           Cor.lightYellow,
           RoleCategory.CrewmateRole,
           NebulaTeams.CrewmateTeam,
            new Virial.Configuration.IConfiguration[] 
            {
            suicideOnImpostor,
            suicideOnNeutral,
            suicideOnCrewmate,
            MarkCoolDown,
            MarkMaxUses,
           }
       )
    {
       ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/WishPic.png")?.AsImage(115f);
    }

    //Virial.Media.Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/RefereeIcon.png")?.AsImage();

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("hope.png")?.AsImage(115f),
            "role.wish.doc.wishSkill"
        );
    }
    static RemoteProcess<byte> RpcPlayReviveFlash = new(
    "HSG.Wish.ReviveFlash",
    (targetId, _) => {
        if (PlayerControl.LocalPlayer.PlayerId == targetId)
            AmongUsUtil.PlayQuickFlash(new Virial.Color(1f,0.9f,0.6f,0.5f));
    }
);
    public static readonly Wish MyRole = new Wish();
    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    Citation HasCitation.Citation => Citations.hvtXsvc_hsg;


    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/WishIcon.png")?.AsImage();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        int UsesLeft;
        public GamePlayer? MarkedPlayer;
        ModAbilityButton? MarkButton;
        public Instance(GamePlayer myPlayer) : base(myPlayer){ }

        public DefinedRole Role => MyRole;

        void IGameOperator.OnReleased() { }

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner)  return; 
            UsesLeft = MarkMaxUses;
            Virial.Media.Image WishMark = NebulaAPI.AddonAsset.GetResource("hope.png")?.AsImage(100f);
            var playerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
            playerTracker.SetColor(MyRole.RoleColor);

            MarkButton = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer,
                VirtualKeyInput.Ability,
                MarkCoolDown,
                "wish.mark",
                WishMark,
                (ModAbilityButton _) => playerTracker.CurrentTarget != null
                    && playerTracker.CurrentTarget != MyPlayer && MarkedPlayer == null ,
                (button) => !MyPlayer.IsDead && UsesLeft > 0,
                false
            );
            MarkButton.ShowUsesIcon(4, UsesLeft.ToString());

            MarkButton.OnClick = (button) =>
            {
                var target = playerTracker.CurrentTarget;
                if (target == null || target == MyPlayer || UsesLeft <= 0) return;
                var zhenying = target.Role.Role.Category;
                MarkedPlayer = target;
                UsesLeft--;
                button.UpdateUsesIcon(UsesLeft.ToString());
                bool suicide = false;
                if (zhenying == RoleCategory.ImpostorRole && suicideOnImpostor)
                {
                    suicide = true;
                }
                else if (zhenying == RoleCategory.CrewmateRole && suicideOnCrewmate)
                {
                    suicide = true;
                }
                else if (zhenying == RoleCategory.NeutralRole && suicideOnNeutral)
                {
                    suicide = true;
                }
                if (suicide) { MyPlayer.Suicide(State.BrokenWish,null,KillParameter.NormalKill,null); }
                button.StartCoolDown();
            };
        }
        [Local]
        private void PlayerMurderedEvent(PlayerMurderedEvent ev)
        {
            if (ev.Dead==MarkedPlayer) 
            { 
                //var 邪恶凶手 = ev.Murderer;// 用中文出问题算谁的。
                var murderer = ev.Murderer; // 那我不用了。
                if (murderer != null)
                {
                    string hex = PatchManager.GetPlayerHexColor(MarkedPlayer);
                    string msg = Language.Translate("role.wish.markdead").Replace("%KILLER%", murderer.PlayerName)
                        .Replace("%VICTIM%", MarkedPlayer.PlayerName).Replace("%COLOR%",ColorHelper.ColorToHexRGB(MarkedPlayer.Role.Role.Color.ToUnityColor()));
                    HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, msg);
                    HsgDebug.Log($"{msg}");
                }
                else
                {
                    string msg2 = Language.Translate("role.wish.murdererdead").Replace("%VICTIMS%", MarkedPlayer.PlayerName).Replace("%COLOR%", ColorHelper.ColorToHexRGB(MarkedPlayer.Role.Role.Color.ToUnityColor()));
                    HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, Language.Translate(msg2));
                }
            }
        }

        [Local]
        private void OnPlayerDie(PlayerDieEvent ev)
        {
            if (ev.Player == MarkedPlayer) MarkedPlayer = null;
        }

        [Local]
        private void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (MarkedPlayer != null && ev.Exiled?.Contains(MarkedPlayer) == true)
            {
                MarkedPlayer.Revive(MyPlayer, MarkedPlayer.Position, true, true);
                AmongUsUtil.PlayQuickFlash(new Virial.Color(1f, 0.9f, 0.6f, 0.5f));
                RpcPlayReviveFlash.Invoke(MarkedPlayer.PlayerId);
                MarkedPlayer = null;
            }
        }
        [Local]
        private void DecorateMarkedPlayerName(PlayerDecorateNameEvent ev)
        {
            if (!AmOwner) return;
            if (MyPlayer.IsDead) return;
            if (MarkedPlayer != null && ev.Player == MarkedPlayer)
            {
                ev.Name += " Wish".Color(new UnityEngine.Color(1f, 0.9f, 0.6f));
            }
        }


    }



}