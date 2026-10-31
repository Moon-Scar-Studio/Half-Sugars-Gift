namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 绛红（船员）。
///
/// 表格描述：
/// 技能一 —— 在会议上拉起任意两个人的私密频道（可以包括自己），绛红能看到自己发起的频道内容；
/// 技能二 —— 悄悄话，选择玩家匿名发送一条信息，次数有限。
///
/// 两个技能都要接管聊天框，所以走 <see cref="CrimsonChatPatch"/> 里的 Harmony 补丁。
/// 补丁包在 try/catch 里：万一原版改了 SendChat 的签名，只有绛红这一个职业失效。
/// </summary>
public class Crimson : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration ChannelUsesOption = NebulaAPI.Configurations.Configuration(
        "options.role.crimson.channelUses", (1, 5), 2);
    static private readonly IntegerConfiguration WhisperUsesOption = NebulaAPI.Configurations.Configuration(
        "options.role.crimson.whisperUses", (1, 10), 3);

    private Crimson() : base(
        "crimson", new Virial.Color(0.78f, 0.1f, 0.25f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [ChannelUsesOption, WhisperUsesOption])
    { }

    public static readonly Crimson MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    // ───────────────────── 频道状态（所有客户端各存一份） ─────────────────────

    /// <summary>本轮会议里开着的私密频道：成员 + 旁观的绛红。</summary>
    static internal readonly List<(byte a, byte b, byte crimsonId)> Channels = new();

    /// <summary>本地玩家正处于「下一条消息走悄悄话」的状态时，这里是收信人。</summary>
    static internal byte WhisperTarget = 255;

    /// <summary>本地玩家是否属于某个私密频道（绛红作为旁观者也算）。</summary>
    static internal bool InChannel(byte playerId)
        => Channels.Any(c => c.a == playerId || c.b == playerId || c.crimsonId == playerId);

    static internal (byte a, byte b, byte crimsonId)? ChannelOf(byte playerId)
    {
        foreach (var c in Channels)
            if (c.a == playerId || c.b == playerId || c.crimsonId == playerId) return c;
        return null;
    }

    static internal readonly RemoteProcess<(byte crimsonId, byte a, byte b)> RpcOpenChannel =
        new("HSG.Crimson.OpenChannel", (msg, _) =>
        {
            Channels.Add((msg.a, msg.b, msg.crimsonId));
            var local = GamePlayer.LocalPlayer;
            if (local == null) return;
            if (local.PlayerId == msg.a || local.PlayerId == msg.b)
                HalfSugarGift.Core.Patch.ExHelpers.ShowMeetingOverlay(
                    "role.crimson.channel.title",
                    Language.Translate("role.crimson.channel.opened"),
                    MyRole.RoleColor);
        });

    /// <summary>频道内发言：只投递给频道成员与旁观的绛红。</summary>
    static internal readonly RemoteProcess<(byte senderId, string text)> RpcChannelChat =
        new("HSG.Crimson.ChannelChat", (msg, _) =>
        {
            var local = GamePlayer.LocalPlayer;
            if (local == null) return;
            var channel = ChannelOf(msg.senderId);
            if (channel == null) return;
            var c = channel.Value;
            if (local.PlayerId != c.a && local.PlayerId != c.b && local.PlayerId != c.crimsonId) return;

            var sender = GamePlayer.GetPlayer(msg.senderId)?.VanillaPlayer;
            if (sender == null || !HudManager.InstanceExists) return;
            HudManager.Instance.Chat.AddChat(sender, "[" + Language.Translate("role.crimson.channel.tag") + "] " + msg.text, false);
        });

    /// <summary>悄悄话：匿名投递给一个人。</summary>
    static internal readonly RemoteProcess<(byte targetId, string text)> RpcWhisper =
        new("HSG.Crimson.Whisper", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.targetId)?.AmOwner != true) return;
            HalfSugarGift.Core.Patch.ExHelpers.ShowMeetingOverlay(
                "role.crimson.whisper.title", msg.text, MyRole.RoleColor);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int channelUses;
        private int whisperUses;
        private GamePlayer? pendingFirst;

        void RuntimeAssignable.OnActivated()
        {
            channelUses = ChannelUsesOption;
            whisperUses = WhisperUsesOption;
        }

        // 私密频道表经 RPC 同步到所有客户端，清空也必须在所有客户端进行；
        // 原来写在 [Local] 里只有绛红本人清空，其他人会一直沿用上一场会议的频道。
        void OnMeetingStartAll(MeetingStartEvent ev) => Channels.Clear();

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            WhisperTarget = 255;
            pendingFirst = null;
            if (MyPlayer.IsDead) return;

            var bm = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();

            // 技能一：连点两个人拉起私密频道
            bm?.RegisterMeetingAction(new(HalfSugarGift.Core.Patch.ExHelpers.MeetingIconOf(4),
                p =>
                {
                    if (pendingFirst == null)
                    {
                        pendingFirst = p.MyPlayer;
                        foreach (var s in bm!.AllStates) s.SetSelect(s.MyPlayer == pendingFirst);
                        return;
                    }
                    if (p.MyPlayer == pendingFirst) { pendingFirst = null; foreach (var s in bm!.AllStates) s.Reset(); return; }

                    channelUses--;
                    RpcOpenChannel.Invoke((MyPlayer.PlayerId, pendingFirst.PlayerId, p.MyPlayer.PlayerId));
                    new StaticAchievementToken(
                        pendingFirst == MyPlayer || p.MyPlayer == MyPlayer ? "crimson.selfChat" : "crimson.lurk");
                    pendingFirst = null;
                    foreach (var s in bm!.AllStates) s.Reset();
                },
                p => channelUses > 0 && !MyPlayer.IsDead && !p.MyPlayer.IsDead,
                false));

            // 技能二：选一个人，下一条聊天内容会匿名发给他
            bm?.RegisterMeetingAction(new(HalfSugarGift.Core.Patch.ExHelpers.MeetingIconOf(5),
                p =>
                {
                    WhisperTarget = p.MyPlayer.PlayerId;
                    HalfSugarGift.Core.Patch.ExHelpers.ShowMeetingOverlay(
                        "role.crimson.whisper.title",
                        Language.Translate("role.crimson.whisper.armed").Replace("%NAME%", p.MyPlayer.Name),
                        MyRole.RoleColor);
                },
                p => whisperUses > 0 && !MyPlayer.IsDead && !p.MyPlayer.AmOwner && !p.MyPlayer.IsDead,
                false));
        }

        /// <summary>聊天补丁发完一条悄悄话后回调，用来扣次数。</summary>
        public void ConsumeWhisper() => whisperUses--;
    }
}

/// <summary>
/// 接管聊天框：把绛红的悄悄话与私密频道的发言从公屏上摘下来。
/// 按照资料库的建议，不去 patch ModAbilityButtonImpl 之类的核心路径，
/// 只挂原版 ChatController.SendChat，并且失败时静默降级。
/// </summary>
[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
internal static class CrimsonChatPatch
{
    static Harmony? harmony;

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        try
        {
            harmony = new Harmony("HSG.Crimson");
            var sendChat = typeof(ChatController).GetMethod(nameof(ChatController.SendChat));
            if (sendChat != null)
                harmony.Patch(sendChat,
                    prefix: new HarmonyMethod(typeof(CrimsonChatPatch).GetMethod(nameof(BeforeSendChat))));
        }
        catch (Exception e)
        {
            HsgDebug.LogException("[Crimson] 聊天补丁应用失败，私密频道与悄悄话将不可用", e);
        }
    }

    /// <returns>false = 吃掉这次公屏发言。</returns>
    public static bool BeforeSendChat(ChatController __instance)
    {
        try
        {
            var local = GamePlayer.LocalPlayer;
            if (local == null) return true;

            string text = __instance.freeChatField?.textArea?.text ?? "";
            if (string.IsNullOrWhiteSpace(text)) return true;

            // 1) 绛红的悄悄话优先
            if (Crimson.WhisperTarget != 255 && local.Role is Crimson.Instance crimson)
            {
                Crimson.RpcWhisper.Invoke((Crimson.WhisperTarget, text));
                Crimson.WhisperTarget = 255;
                crimson.ConsumeWhisper();
                __instance.freeChatField.Clear();
                return false;
            }

            // 2) 身处私密频道 → 只发给频道成员
            if (Crimson.InChannel(local.PlayerId))
            {
                Crimson.RpcChannelChat.Invoke((local.PlayerId, text));
                __instance.freeChatField.Clear();
                return false;
            }
        }
        catch (Exception e)
        {
            HsgDebug.LogException("[Crimson] SendChat 前置处理异常", e);
        }
        return true;
    }
}
