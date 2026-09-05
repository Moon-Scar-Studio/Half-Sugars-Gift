using NebulaN.Core;
using NebulaN.Roles.Crewmate;

namespace NebulaN.Roles.Modifier
{
    public class Karma : DefinedAllocatableModifierTemplate, HasCitation, DefinedAllocatableModifier
    {

        Karma() : base(
            "karma",
            "kar",
            Cor.Violet,
            new Virial.Configuration.IConfiguration[] { },
            false,
            false,
            false
        )
        { }

        public static readonly Karma MyRole = new();
        public Citation Citation => Citations.hvtXsvc_hsg;
        bool ISpawnable.IsSpawnable => false;
        RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
        Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/KarmaticIcon.png").AsImage();
        public class Instance : RuntimeAssignableTemplate, RuntimeModifier
        {
            bool ProcessingKarma = false;
            int EraseSelfModiCounts;
            DefinedModifier RuntimeModifier.Modifier => MyRole;
            bool RuntimeAssignable.CanBeAwareAssignment => false;
            public Instance(GamePlayer player) : base(player) { }
            void RuntimeAssignable.OnActivated()
            {
                if (AmOwner)
                    EraseSelfModiCounts = Karmatic.EraseKarmaMeetingTimes;
            }
            [Local]
            void KillSelf(PlayerCheckKilledEvent ev)
            {
                if (ev.Killer != MyPlayer) return;
                if (ev.Player == ev.Killer) return;
                
                if (ProcessingKarma) return;

                ProcessingKarma = true;
                if (Karmatic.NeedImp)
                    if (MyPlayer.Role.Role.Category != RoleCategory.ImpostorRole)
                        return;

                ev.Result = KillResult.Guard;
                AmongUsUtil.PlayQuickFlash(Cor.Violet);
                if (Karmatic.DeadKillKarma)
                {
                    ev.Player.MurderPlayer(MyPlayer, null, null, KillParameter.NormalKill);
                }
                else if (!Karmatic.DeadKillKarma)
                {
                    MyPlayer.Suicide(PlayerState.Dead, null, KillParameter.NormalKill);
                }

                ProcessingKarma = false;
            }
            [Local]
            void EraseKarma(MeetingPreEndEvent ev)
            {
                if (EraseSelfModiCounts <= 0) return;
                EraseSelfModiCounts--;
                if (EraseSelfModiCounts <= 0)
                {
                    MyPlayer.RemoveModifier(MyRole);
                    KarmaVisualManager.RemoveKarmaVisual(MyPlayer.PlayerId);
                }
            }
        }
    }
}