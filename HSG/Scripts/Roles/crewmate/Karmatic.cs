using NebulaN.Core;
using NebulaN.Roles.Crewmate;
using NebulaN.Roles.Modifier;

namespace NebulaN.Roles.Crewmate
{
    public class Karmatic : DefinedRoleTemplate, DefinedRole, HasCitation, IAssignableDocument, RuntimeAssignableGenerator<RuntimeRole>
    {
        static FloatConfiguration CausalityChainCoolDown = NebulaAPI.Configurations.Configuration(
            "options.role.karmatic.CausalityChainCoolDown", (5f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second
            );
        static FloatConfiguration FollowTime = NebulaAPI.Configurations.Configuration(
            "options.role.karmatic.followtime", (0.5f, 10f, 0.5f), 3f
            );
        static IntegerConfiguration CausalityChainMaxUses = NebulaAPI.Configurations.Configuration(
            "options.role.karmatic.CausalityChainMaxUses", (1, 5, 1), 1);
        public static IntegerConfiguration EraseKarmaMeetingTimes = NebulaAPI.Configurations.Configuration(
            "options.role.karmatic.EraseKarmaMeetingTimes", (1, 10, 1), 1
            );
        public static BoolConfiguration DeadKillKarma = NebulaAPI.Configurations.Configuration("options.role.karmatic.deadkillkarma", false); // 为true时死者杀凶手，false时凶手自杀
        Karmatic() : base(
            "karmatic", Cor.Violet, RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
            [CausalityChainCoolDown, FollowTime, CausalityChainMaxUses, EraseKarmaMeetingTimes, DeadKillKarma]
            )
        {

        }

        public Citation Citation => Citations.hvtXsvc_hsg;
        static public readonly Karmatic MyRole = new();
        bool IAssignableDocument.HasTips => false;
        bool IAssignableDocument.HasAbility => true;
        public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
        public class Instance : RuntimeAssignableTemplate, RuntimeRole
        {
            public Instance(GamePlayer myPlayer) : base(myPlayer) { }
            int UsesLeft;
            public DefinedRole Role => MyRole;
            Image CausalityChainImage = NebulaAPI.AddonAsset.GetResource("CausalityChainImage.png")?.AsImage(100f);
            ModAbilityButton CausalityChain;
            GamePlayer TargetPlayer;
            bool HasTarget = false;
            void RuntimeAssignable.OnActivated()
            {
                if (!AmOwner) return;
                UsesLeft = CausalityChainMaxUses.GetValue();
                var PlayerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
                CausalityChain = NebulaAPI.Modules.EffectButton(
                    this, MyPlayer, VirtualKeyInput.Ability, CausalityChainCoolDown, FollowTime, "CausalityChain", CausalityChainImage, _ => PlayerTracker.CurrentTarget != null && UsesLeft != 0 && !HasTarget, _ => !MyPlayer.IsDead
                    );
                PlayerTracker.SetColor(MyRole.Color);
                CausalityChain.OnEffectStart = _ =>
                {
                    PlayerTracker.KeepAsLongAsPossible = true;
                };
                CausalityChain.OnEffectEnd = button =>
                {
                    PlayerTracker.KeepAsLongAsPossible = false;
                    var target = PlayerTracker.CurrentTarget;
                    if (target == null) return;
                    if (MeetingHud.Instance != null) return;
                    if (!button.EffectTimer!.IsProgressing)
                    {
                        TargetPlayer = target;
                        HasTarget = true;
                    }
                    button.StartCoolDown();
                    button.UpdateUsesIcon(UsesLeft.ToString());
                };
                CausalityChain.OnUpdate = button =>
                {
                    if (!button.IsInEffect) return;
                    if (PlayerTracker.CurrentTarget == null || PlayerTracker.CurrentTarget.IsDead)
                        button.InterruptEffect();
                };
            }
            [Local]
            void OnMeetingStart(MeetingStartEvent ev)
            {
                if (!HasTarget) return;

            }
            [Local]
            void AddKarma(MeetingEndEvent ev)
            {
                if (!HasTarget) return;
                if (TargetPlayer == null) return;
                TargetPlayer.AddModifier(Karma.MyRole);
                KarmaVisualManager.ShowKarmaVisual(TargetPlayer);
            }
            [Local]
            void OnDecorateName(PlayerDecorateNameEvent ev)
            {
                if (!HasTarget || TargetPlayer == null) return;

                if (ev.Player == TargetPlayer)
                {
                    ev.Name += " K".Color(Cor.Violet.ToUnityColor());
                }
            }
        }
    }
    



}