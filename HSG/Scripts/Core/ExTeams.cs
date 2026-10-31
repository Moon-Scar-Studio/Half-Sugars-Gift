namespace HalfSugarGift.Core.Patch;

/// <summary>
/// 星云插件想法创意表里新增职业所需要的阵营 / 结算 / 死因标签。
///
/// 这些字段是 static readonly，依靠 C# 的惰性静态初始化：
/// 第一个引用它们的角色类在 Roles 阶段构造时会触发本类的静态构造函数，
/// 此时 <see cref="NebulaAPI.Preprocessor"/> 仍然可用。与 PatchManager.cs 里的 Team 类写法一致。
///
/// GameEnd 的 id 是全局 6bit 配额（主程序集已占 16~38 / 62 / 63），
/// 所以这里只给"必须要有独立结算画面"的角色建 End，
/// 跟随他人获胜的（狂信徒跟随、纯白加入阵营、加班狂跟随老板）一律复用宿主阵营的结算。
/// </summary>
public static class ExTeam
{
    static Virial.Color C(float r, float g, float b) => new(r, g, b);

    // ── 禁忌之恋（链子，被所有带刀角色知晓，需躲过追杀独立获胜） ──
    public static readonly RoleTeam ForbiddenLoveTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.forbiddenLove", C(1f, 0.45f, 0.72f), TeamRevealType.Teams);
    public static readonly GameEnd ForbiddenLoveWin =
        NebulaAPI.Preprocessor!.CreateEnd("forbiddenLoveWin", ForbiddenLoveTeam.Color, 61);

    // ── 猫猫虫 ──
    public static readonly RoleTeam CaterpillarTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.caterpillar", C(0.85f, 0.7f, 0.95f), TeamRevealType.OnlyMe);
    public static readonly GameEnd CaterpillarWin =
        NebulaAPI.Preprocessor!.CreateEnd("caterpillarWin", CaterpillarTeam.Color, 60);

    // ── 狂信徒（默认跟随被跟随者，房主可切换为独立获胜） ──
    public static readonly RoleTeam FanaticTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.fanatic", C(0.55f, 0.1f, 0.55f), TeamRevealType.OnlyMe);
    public static readonly GameEnd FanaticWin =
        NebulaAPI.Preprocessor!.CreateEnd("fanaticWin", FanaticTeam.Color, 59);

    // ── 寻宝者 ──
    public static readonly RoleTeam TreasureHunterTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.treasureHunter", C(0.95f, 0.8f, 0.25f), TeamRevealType.OnlyMe);
    public static readonly GameEnd TreasureHunterWin =
        NebulaAPI.Preprocessor!.CreateEnd("treasureHunterWin", TreasureHunterTeam.Color, 58);

    // ── 疯子（知晓全场身份，活到最后抢夺胜利） ──
    public static readonly RoleTeam MadmanTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.madman", C(0.6f, 0.9f, 0.35f), TeamRevealType.OnlyMe);
    public static readonly GameEnd MadmanWin =
        NebulaAPI.Preprocessor!.CreateEnd("madmanWin", MadmanTeam.Color, 57);

    // ── 欢愉者 ──
    public static readonly RoleTeam RevelerTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.reveler", C(1f, 0.35f, 0.55f), TeamRevealType.OnlyMe);
    public static readonly GameEnd RevelerWin =
        NebulaAPI.Preprocessor!.CreateEnd("revelerWin", RevelerTeam.Color, 56);

    // ── 水管工 ──
    public static readonly RoleTeam PlumberTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.plumber", C(0.2f, 0.55f, 1f), TeamRevealType.Everyone);
    public static readonly GameEnd PlumberWin =
        NebulaAPI.Preprocessor!.CreateEnd("plumberWin", PlumberTeam.Color, 55);
    /// <summary>水管工在只剩三人时被杀 → 船员 / 内鬼 / 中立共同获胜。</summary>
    public static readonly GameEnd PlumberEveryoneWin =
        NebulaAPI.Preprocessor!.CreateEnd("plumberEveryoneWin", C(1f, 1f, 1f), 54);

    // ── 寻仇者（自由阶段被杀后可报仇） ──
    public static readonly RoleTeam VindicatorTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.vindicator", C(0.75f, 0.15f, 0.15f), TeamRevealType.OnlyMe);
    public static readonly GameEnd VindicatorWin =
        NebulaAPI.Preprocessor!.CreateEnd("vindicatorWin", VindicatorTeam.Color, 53);

    // ── 画师（与粉丝共同获胜） ──
    public static readonly RoleTeam ArtistTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.artist", C(0.35f, 0.8f, 0.8f), TeamRevealType.Teams);
    public static readonly GameEnd ArtistWin =
        NebulaAPI.Preprocessor!.CreateEnd("artistWin", ArtistTeam.Color, 52);

    // ── 救命恩人（与被救援者共同获胜） ──
    public static readonly RoleTeam SaviorTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.savior", C(0.4f, 1f, 0.75f), TeamRevealType.Teams);
    public static readonly GameEnd SaviorWin =
        NebulaAPI.Preprocessor!.CreateEnd("saviorWin", SaviorTeam.Color, 51);

    // ── 迷途者 ──
    public static readonly RoleTeam StrayerTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.strayer", C(0.55f, 0.55f, 0.75f), TeamRevealType.OnlyMe);
    public static readonly GameEnd StrayerWin =
        NebulaAPI.Preprocessor!.CreateEnd("strayerWin", StrayerTeam.Color, 50);

    // ── 老板 & 加班狂 ──
    public static readonly RoleTeam BossTeam =
        NebulaAPI.Preprocessor!.CreateTeam("teams.boss", C(0.95f, 0.75f, 0.1f), TeamRevealType.Teams);
    public static readonly GameEnd BossWin =
        NebulaAPI.Preprocessor!.CreateEnd("bossWin", BossTeam.Color, 49);
}

/// <summary>新增职业用到的死因 / 事件标签。翻译键为 "state.xxx"。</summary>
public static class ExState
{
    /// <summary>死因：伏笔（代号007）</summary>
    public static readonly TranslatableTag Foreshadow = new("state.hsgForeshadow");
    /// <summary>死因：欢愉（欢愉者破坏的任务）</summary>
    public static readonly TranslatableTag Revel = new("state.hsgRevel");
    /// <summary>死因：刺杀（国王加冕了非船员）</summary>
    public static readonly TranslatableTag Assassinated = new("state.hsgAssassinated");
    /// <summary>死因：替死（国王的被加冕者）</summary>
    public static readonly TranslatableTag Substitute = new("state.hsgSubstitute");
    /// <summary>死因：毒发（朱砂链子）</summary>
    public static readonly TranslatableTag Poisoned = new("state.hsgPoisoned");
    /// <summary>死因：噬魂 / 催眠超时自尽（猫头鹰）</summary>
    public static readonly TranslatableTag SoulEaten = new("state.hsgSoulEaten");
    /// <summary>死因：卷入龙卷风（袭卷者）</summary>
    public static readonly TranslatableTag BlownAway = new("state.hsgBlownAway");
    /// <summary>死因：麻醉（麻醉师）</summary>
    public static readonly TranslatableTag Anesthetized = new("state.hsgAnesthetized");
    /// <summary>死因：破产（加班狂工资归零）</summary>
    public static readonly TranslatableTag Bankrupt = new("state.hsgBankrupt");
    /// <summary>死因：任务超时（农场主）</summary>
    public static readonly TranslatableTag Overdue = new("state.hsgOverdue");
    /// <summary>死因：强迫症发作</summary>
    public static readonly TranslatableTag Compulsion = new("state.hsgCompulsion");
    /// <summary>死因：报仇超时（寻仇者）</summary>
    public static readonly TranslatableTag Unavenged = new("state.hsgUnavenged");
    /// <summary>死因：迷途（迷途者出刀后存活）</summary>
    public static readonly TranslatableTag Strayed = new("state.hsgStrayed");
    /// <summary>死因：牵连（救命恩人死亡时被救援者暴毙）</summary>
    public static readonly TranslatableTag Entangled = new("state.hsgEntangled");
}
