namespace NebulaN.Roles.Modifier;

/// <summary>
/// 被催眠者（附加职业，由猫头鹰催眠）。
/// · 获得一把一次性的刀，人数上算内鬼。
/// · 出刀后恢复正常（解除本附加职业），且会议上所有人都能知道被刀者的死因。
/// · 规定时间内不出刀 → 自杀。
/// · 催眠期间猫头鹰可随时用「噬魂」让其暴毙。
/// </summary>
public class Hypnotized : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Hypnotized() : base("hypnotized", new Virial.Color(0.45f, 0.3f, 0.6f), withConfigurationHolder: false) { }

    public static readonly Hypnotized MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte owlId;
        private float timeLeft;
        private ModAbilityButton? killButton;

        public byte OwlId => owlId;

        public Instance(GamePlayer player, int owlId) : base(player) { this.owlId = (byte)owlId; }

        int[] RuntimeAssignable.RoleArguments => [owlId];

        void RuntimeAssignable.OnActivated()
        {
            timeLeft = NebulaN.Roles.Impostor.Owl.HypnosisDuration;
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.KillTracker(this, MyPlayer, p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            killButton = NebulaAPI.Modules.KillButton(
                this, MyPlayer, true, VirtualKeyInput.Kill, null, 0f,
                "hypnotized.kill", ModAbilityButton.LabelType.Impostor, null,
                (target, button) =>
                {
                    MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    // 出刀即恢复正常，并公开死因
                    Impostor.Owl.RpcHypnosisKilled.Invoke((MyPlayer.PlayerId, target.PlayerId));
                    MyPlayer.RemoveModifier(MyRole);
                },
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId,
                b => !MyPlayer.IsDead,
                b => !MyPlayer.IsDead);
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
        }

        /// <summary>人数统计上算内鬼。</summary>
        bool RuntimeAssignable.CanKill(GamePlayer player) => true;

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner || MyPlayer.IsDead) return;
            timeLeft -= ev.DeltaTime;
            if (timeLeft <= 0f)
            {
                timeLeft = float.MaxValue;
                MyPlayer.Suicide(HalfSugarGift.Core.Patch.ExState.SoulEaten, EventDetail.Kill, KillParameter.NormalKill);
            }
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            if (AmOwner || canSeeAllInfo || inEndScene || (local != null && local.PlayerId == owlId))
                name += " ☾".Color(new Color(0.45f, 0.3f, 0.6f));
        }
    }
}
