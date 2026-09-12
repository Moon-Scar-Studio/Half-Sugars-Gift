
using NebulaN.Roles.Crewmate;

namespace NebulaN.Roles.Ghost;

public class Glimmer : DefinedGhostRoleTemplate, DefinedGhostRole, HasCitation
{
    Glimmer() : base(
        "glimmer",
        new Virial.Color(1f, 0f, 0f),
        RoleCategory.CrewmateRole,
        [Uses, CoolDown, FlashDuration,FlashColor]
    )
    {
    }
    static IntegerConfiguration Uses =
        NebulaAPI.Configurations.Configuration(
            "options.role.glimmer.uses",
            (1, 10),
            1
        );
    static FloatConfiguration CoolDown =
        NebulaAPI.Configurations.Configuration(
            "options.role.glimmer.cooldown",
            (0f, 60f, 2.5f),
            15f,
            FloatConfigurationDecorator.Second
        );
    static FloatConfiguration FlashDuration =
        NebulaAPI.Configurations.Configuration(
            "options.role.glimmer.flashDuration",
            (0.5f, 10f, 0.5f),
            3f,
            FloatConfigurationDecorator.Second
        );
    static ValueConfiguration<int> FlashColor = NebulaAPI.Configurations.Configuration(
        "options.role.glimmer.flashcolor",
        new[] { "options.role.glimmer.flashcolor.red", "options.role.glimmer.flashcolor.yellow", "options.role.glimmer.flashcolor.cyan", "options.role.glimmer.flashcolor.green", "options.role.glimmer.flashcolor.blue", "options.role.glimmer.flashcolor.purple", "options.role.glimmer.flashcolor.wishY","options.role.glimmer.flashcolor.followtarget" }
        , 0
        );
    Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/GlimmerIcon.png")?.AsImage();
    public static readonly Glimmer MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    public string CodeName => "GR";
    RuntimeGhostRole RuntimeAssignableGenerator<RuntimeGhostRole>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player);
    public class Instance : RuntimeAssignableTemplate, RuntimeGhostRole
    {
        DefinedGhostRole RuntimeGhostRole.Role => MyRole;
        ModAbilityButton? flashButton;
        public Instance(GamePlayer player) : base(player)
        {
        }
        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            int left = Uses;
            var playerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
            playerTracker.SetColor(MyRole.RoleColor);
            flashButton = NebulaAPI.Modules.AbilityButton(
                this,
                MyPlayer,
                VirtualKeyInput.Ability,
                CoolDown,
                "glimmer.flash",
                NebulaAPI.AddonAsset.GetResource("GlimmerLight.png")?.AsImage(),
                _ => playerTracker.CurrentTarget != null
                    && playerTracker.CurrentTarget != MyPlayer,
                _ => left > 0,
                true
            );
            flashButton.ShowUsesIcon(0, left.ToString());
            flashButton.OnClick = button =>
            {
                var target = playerTracker.CurrentTarget;
                if (target == null)
                    return;
                if (FlashColor.GetValue() != 7)
                {
                    PatchManager.RpcFlashCustom.Invoke((target.PlayerId, ColorHelper.ColorToHexRGB(GetConfigColor(FlashColor).ToUnityColor()), 0.2f, FlashDuration));
                }
                else if (FlashColor.GetValue() == 7)
                {
                    PatchManager.RpcFlashCustom.Invoke((target.PlayerId,ColorHelper.ColorToHexRGB(target.GetPlayerColor()),0.2f,FlashDuration));
                }
                left--;
                button.UpdateUsesIcon(left.ToString());
                button.StartCoolDown();
            };
        }

        Virial.Color wishY = new Virial.Color(1, 0.9f, 0.6f);
        public Virial.Color GetConfigColor(ValueConfiguration<int> Config)
        {
            switch (Config.GetValue())
            {
                case 0: return Cor.impRed;
                case 1: return Cor.Yellow;
                case 2: return Cor.cyan;
                case 3: return Cor.green;
                case 4: return Cor.blue;
                case 5: return new Virial.Color(0.5f, 0, 0.5f);
                case 6: return wishY;
                default: return Cor.White;
            }
        }
    }
}