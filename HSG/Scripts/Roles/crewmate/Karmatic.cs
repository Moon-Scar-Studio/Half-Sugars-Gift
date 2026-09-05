using NebulaN.Core;
using NebulaN.Roles.Crewmate;
using NebulaN.Roles.Modifier;
using UnityEngine.TextCore.Text;

namespace NebulaN.Roles.Crewmate
{
    public class Karmatic : DefinedRoleTemplate, DefinedRole, HasCitation, IAssignableDocument, RuntimeAssignableGenerator<RuntimeRole>
    {
        static FloatConfiguration CausalityChainCoolDown = NebulaAPI.Configurations.Configuration(
            "options.role.karmatic.CausalityChainCoolDown", (5f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second
            );
        static FloatConfiguration FollowTime = NebulaAPI.Configurations.Configuration(
            "options.role.karmatic.followtime", (0.5f, 10f, 0.5f), 3f,FloatConfigurationDecorator.Second
            );
        static IntegerConfiguration CausalityChainMaxUses = NebulaAPI.Configurations.Configuration(
            "options.role.karmatic.CausalityChainMaxUses", (1, 5, 1), 1);
        public static IntegerConfiguration EraseKarmaMeetingTimes = NebulaAPI.Configurations.Configuration(
            "options.role.karmatic.EraseKarmaMeetingTimes", (1, 10, 1), 1
            );
        public static BoolConfiguration DeadKillKarma = NebulaAPI.Configurations.Configuration("options.role.karmatic.deadkillkarma", false); // 为true时死者杀凶手，false时凶手自杀
        public static ValueConfiguration<int> NameDeco = NebulaAPI.Configurations.Configuration(
            "options.role.karmatic.NameDeco", ["options.role.karmatic.NameDeco.K", "options.role.karmatic.NameDeco.Color"],1
            );
        public static BoolConfiguration NeedImp = NebulaAPI.Configurations.Configuration("options.role.karmatic.NeedImp",true);
        Karmatic() : base(
            "karmatic", Cor.Violet, RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
            [CausalityChainCoolDown, FollowTime, CausalityChainMaxUses, EraseKarmaMeetingTimes, DeadKillKarma,NameDeco, NeedImp]
            )
        {

        }
        Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/KarmaticIcon.png").AsImage(); 
        public Citation Citation => Citations.hvtXsvc_hsg;
        static public readonly Karmatic MyRole = new();
        bool IAssignableDocument.HasTips => true;
        bool IAssignableDocument.HasAbility => true;
        public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
        IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
        {
            yield return new AssignableDocumentReplacement("%FT%", FollowTime.GetValue().ToString());
            yield return new AssignableDocumentReplacement("%MaxUses%", CausalityChainMaxUses.GetValue().ToString());
            yield return new AssignableDocumentReplacement("%CD%", CausalityChainCoolDown.GetValue().ToString());
            yield return new AssignableDocumentReplacement("%NameDeco%", NameDeco.GetValue()==0?Language.Translate("options.role.karmatic.NameDeco.K.Des") :Language.Translate("options.role.karmatic.NameDeco.Color.Des"));
            yield return new AssignableDocumentReplacement("%TryKillSomebody%", DeadKillKarma?Language.Translate("options.role.karmatic.deadkillkarma.true") :Language.Translate("options.role.karmatic.deadkillkarma.false"));
            //
            yield return new AssignableDocumentReplacement("%MeetingTimes%", EraseKarmaMeetingTimes.GetValue().ToString());
        }
        IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
        {
            yield return new AssignableDocumentImage(
                NebulaAPI.AddonAsset.GetResource("CausalityChainImage.png")?.AsImage(100f),
                "role.karmatic.doc.causalitychain");
        }
        public class Instance : RuntimeAssignableTemplate, RuntimeRole
        {
            public Instance(GamePlayer myPlayer) : base(myPlayer) { }
            int UsesLeft;
            public DefinedRole Role => MyRole;
            Image CausalityChainImage = NebulaAPI.AddonAsset.GetResource("CausalityChainImage.png")?.AsImage(120f);
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
                CausalityChain.ShowUsesIcon(4, UsesLeft.ToString());

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
                    int nd = NameDeco.GetValue();
                    switch (nd)
                    {
                        case 0:
                            ev.Name += " K".Color(Cor.Violet.ToUnityColor());
                            break;
                        case 1:
                        default:
                            string origName = ev.Name;
                            ev.Name = $"<color=#{ColorUtility.ToHtmlStringRGB(MyRole.Color.ToUnityColor())}>{origName}</color>";
                            break;
                        
                    }
                    
                }
            }
        }
    }
}