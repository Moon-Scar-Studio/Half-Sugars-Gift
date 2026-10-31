namespace HalfSugarGift.Core.Patch;

/// <summary>新增职业共用的小工具：会议覆盖层、会议按钮图标、随机挑人等。</summary>
public static class ExHelpers
{
    /// <summary>
    /// 会议内按钮的默认图标。主程序集的 MeetingButtons.png 是切片图集，
    /// 这里借用第 0 号切片，省得每个角色都做一张图。
    /// </summary>
    public static Image MeetingIcon => MeetingPlayerButtonManager.Icons.AsLoader(0);

    public static Image MeetingIconOf(int index) => MeetingPlayerButtonManager.Icons.AsLoader(index);

    /// <summary>在会议里给本地玩家弹一个信息面板。</summary>
    public static void ShowMeetingOverlay(string titleKey, string content, Virial.Color color, int iconIndex = 6)
    {
        // 全程走公开的 NebulaAPI.GUI：
        // 主程序集那个 GUI.API 属于 Nebula.Modules.GUIWidget.GUI，引入它会与 UnityEngine.GUI 撞名，
        // 而 NebulaAPI.GUI 返回的就是同一套 Virial.Media.GUI，还省掉了 NoSGUIText 这个内部类型。
        var gui = NebulaAPI.GUI;
        NebulaAPI.CurrentGame?.GetModule<MeetingOverlayHolder>()?.RegisterOverlay(
            gui.VerticalHolder(GUIAlignment.Left,
                gui.Text(GUIAlignment.Left,
                    gui.GetAttribute(AttributeAsset.OverlayTitle),
                    new TranslateTextComponent(titleKey)),
                gui.Text(GUIAlignment.Left,
                    gui.GetAttribute(AttributeAsset.OverlayContent),
                    new RawTextComponent(content))),
            MeetingOverlayHolder.IconsSprite[iconIndex],
            color);
    }

    /// <summary>把一段文字作为「会议信息」推给指定玩家（只有那个人看得到）。</summary>
    public static readonly RemoteProcess<(byte targetId, string titleKey, string content, string colorHex)>
        RpcShowMeetingInfo = new("HSG.Ex.ShowMeetingInfo", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.targetId)?.AmOwner != true) return;
            if (!ColorUtility.TryParseHtmlString(msg.colorHex, out var color)) color = Color.white;
            ShowMeetingOverlay(msg.titleKey, msg.content, color.ToVirialColor());
        });

    /// <summary>从候选里随机取一个（空集合返回 null）。</summary>
    public static T? PickRandom<T>(this IEnumerable<T> source) where T : class
    {
        var list = source as IList<T> ?? source.ToList();
        return list.Count == 0 ? null : list[UnityEngine.Random.Range(0, list.Count)];
    }

    /// <summary>当前存活且没断线的玩家。</summary>
    public static IEnumerable<GamePlayer> AlivePlayers
        => GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.IsDisconnected);

    /// <summary>阵营字眼：内鬼 = 黑，船员 = 白，中立 = 灰。群青 / 测谎师等会用到。</summary>
    /// <summary>阵营字眼的翻译键（跨客户端同步时传键而不是译文，各端按自己的语言显示）。</summary>
    public static string CampKey(GamePlayer player)
    {
        if (player.IsImpostor) return "hsg.camp.black";
        if (player.Role.Role.Category == RoleCategory.NeutralRole) return "hsg.camp.grey";
        return "hsg.camp.white";
    }

    public static string CampWord(GamePlayer player)
    {
        if (player.IsImpostor) return Language.Translate("hsg.camp.black");
        if (player.Role.Role.Category == RoleCategory.NeutralRole) return Language.Translate("hsg.camp.grey");
        return Language.Translate("hsg.camp.white");
    }

    public static Virial.Color CampColor(GamePlayer player)
    {
        if (player.IsImpostor) return new Virial.Color(0.2f, 0.2f, 0.2f);
        if (player.Role.Role.Category == RoleCategory.NeutralRole) return new Virial.Color(0.6f, 0.6f, 0.6f);
        return new Virial.Color(1f, 1f, 1f);
    }

    /// <summary>安全地给某人挂修饰符（已有同类则跳过）。</summary>
    public static void AddModifierOnce<M>(this GamePlayer player, DefinedModifier modifier, int[]? args)
        where M : class, RuntimeModifier
    {
        if (player.TryGetModifier<M>(out _)) return;
        player.AddModifier(modifier, args);
    }
}
