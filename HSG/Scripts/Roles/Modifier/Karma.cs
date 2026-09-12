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
                EraseSelfModiCounts = Karmatic.EraseKarmaMeetingTimes;
            }

            // PlayerCheckKilledEvent 只在房主客户端触发（见 API 注释）。
            // 原来标的是 [Local]（只在持有者本机执行），两者只有持有者恰好是房主时才同时成立。
            [OnlyHost]
            void KillSelf(PlayerCheckKilledEvent ev)
            {
                if (ev.Killer != MyPlayer) return;
                if (ev.Player == ev.Killer) return;
                if (ProcessingKarma) return;
                if (Karmatic.NeedImp && MyPlayer.Role.Role.Category != RoleCategory.ImpostorRole) return;

                ProcessingKarma = true;
                try
                {
                    ev.Result = KillResult.Guard;
                    RpcKarmaFlash.Invoke(MyPlayer.PlayerId);
                    if (Karmatic.DeadKillKarma)
                        ev.Player.MurderPlayer(MyPlayer, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    else
                        MyPlayer.Suicide(PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                }
                finally
                {
                    // 原实现在 NeedImp 提前 return 时不会复位，之后永久失效
                    ProcessingKarma = false;
                }
            }

            static readonly RemoteProcess<byte> RpcKarmaFlash = new("HSG.Karma.Flash", (playerId, _) =>
            {
                if (GamePlayer.GetPlayer(playerId)?.AmOwner == true)
                    AmongUsUtil.PlayQuickFlash(Cor.Violet);
            });

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