namespace HalfSugarGift.Core;

public static class Citations
{
    public static Citation hvtXsvc_hsg { get; private set; } = new(
        "Half Sugars Gift",
        NebulaAPI.AddonAsset.GetResource("Citat/HalfSugarGift.png")?.AsImage(125f),
        new ColorTextComponent(
            Cor.impRed,
            new RawTextComponent("Half Sugars Gift")
        ),
        "https://hvtxsvc.top/download"
    );

    public static Citation AmongUs { get; private set; } = new(
        "Innersloth",
        NebulaAPI.AddonAsset.GetResource("Citat/AmongUs.png")?.AsImage(125f),
        new ColorTextComponent(
            Cor.White,
            new RawTextComponent("Innersloth")
        ),
        "https://www.innersloth.com/games/among-us/"
    );
    public static Citation Hellos497 { get; private set; } = new(
        "Hellos497",
        NebulaAPI.AddonAsset.GetResource("Citat/HalfSugarGift_Hellos497.png")?.AsImage(125f),
        new ColorTextComponent(
            Cor.impRed,
            new RawTextComponent("Hellos497")
        ),
        "https://hvtxsvc.top/download"
    );

    public static Citation TheOtherRoles { get; private set; } = new(
        "The Other Roles",
        NebulaAPI.AddonAsset.GetResource("Citat/TOR_logo.png")?.AsImage(125f),
        new ColorTextComponent(
            Cor.impRed,
            new RawTextComponent("The Other Roles")
        ),
        "https://github.com/TheOtherRolesAU/TheOtherRoles"
    );
    public static Citation LightInDark { get; private set; } = new(
        "Light in Dark",NebulaAPI.AddonAsset.GetResource("Citat/LightInDark.png")?.AsImage(125f),
        new ColorTextComponent(
            Cor.impRed,
            new RawTextComponent("Light in Dark")
        ),
        "https://github.com/LightInDark/LightInDark"
    );
}