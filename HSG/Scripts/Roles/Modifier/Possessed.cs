namespace NebulaN.Roles.Modifier;

/// <summary>
/// 被附身（附加职业，由人造怨魂附身）。
/// 附身期间获得一把冷却仅 3 秒的刀；灵魂回体后自动解除。
/// </summary>
public class Possessed : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Possessed() : base("possessed", new Virial.Color(0.4f, 0.2f, 0.55f), withConfigurationHolder: false) { }

    public static readonly Possessed MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255), arguments.Get(1, 20));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte wraithId;
        private float timeLeft;

        public Instance(GamePlayer player, int wraithId, int duration) : base(player)
        {
            this.wraithId = (byte)wraithId;
            timeLeft = duration;
        }

        int[] RuntimeAssignable.RoleArguments => [wraithId, (int)timeLeft];

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            new StaticAchievementToken("wraith.whatHappened");

            var tracker = NebulaAPI.Modules.KillTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            var kill = NebulaAPI.Modules.KillButton(
                this, MyPlayer, true, VirtualKeyInput.Kill, null, 3f,
                "kill", ModAbilityButton.LabelType.Impostor, null,
                (target, button) =>
                {
                    MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    button.StartCoolDown();
                },
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId,
                b => !MyPlayer.IsDead,
                b => !MyPlayer.IsDead);
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(kill.GetKillButtonLike());
        }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner) return;
            timeLeft -= ev.DeltaTime;
            if (timeLeft <= 0f) MyPlayer.RemoveModifier(MyRole);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            if (AmOwner || canSeeAllInfo || inEndScene || (local != null && local.PlayerId == wraithId))
                name += " ༄".Color(new Color(0.4f, 0.2f, 0.55f));
        }
    }
}
