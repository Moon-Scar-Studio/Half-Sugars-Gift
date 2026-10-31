namespace NebulaN.Roles.Impostor;

/// <summary>
/// 袭卷者（内鬼）。
///
/// 「拥有一个释放气流装置的技能，放下装置后可以使用『开启』生成一个旋转的龙卷风，
/// 持续 5~30 秒，期间任何在范围内的人全都死亡。该技能只能使用一次，用完变成普通伪装者。」
/// </summary>
public class Sweeper : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration DurationOption = NebulaAPI.Configurations.Configuration(
        "options.role.sweeper.duration", (5f, 30f, 1f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration RadiusOption = NebulaAPI.Configurations.Configuration(
        "options.role.sweeper.radius", (1f, 6f, 0.5f), 2.5f, decorator: v => v + "m");
    static private readonly BoolConfiguration KillsSelfOption = NebulaAPI.Configurations.Configuration(
        "options.role.sweeper.killsSelf", false);

    private Sweeper() : base(
        "sweeper", Cor.impRed, RoleCategory.ImpostorRole, NebulaTeams.ImpostorTeam,
        [DurationOption, RadiusOption, KillsSelfOption])
    { }

    public static readonly Sweeper MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>龙卷风开启：全客户端一起显示特效，由房主负责判定击杀。</summary>
    static internal readonly RemoteProcess<(byte sweeperId, float x, float y, float radius, float duration)> RpcTornado =
        new("HSG.Sweeper.Tornado", (msg, _) =>
        {
            AmongUsUtil.PlayCustomFlash(new Virial.Color(0.7f, 0.7f, 0.8f), 0.5f, 0.5f, 0.4f, msg.duration);
            // 判定在房主执行，龙卷风的位置与时长必须同步过去（原来只存在施法者本机，房主那边永远不会刮）
            if (GamePlayer.GetPlayer(msg.sweeperId)?.Role is Instance inst)
                inst.StartTornado(new Vector2(msg.x, msg.y), msg.duration);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private Vector2? devicePos = null;
        private bool used = false;
        private float tornadoLeft = 0f;
        private Vector2 tornadoPos;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var place = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, 0f, "sweeper.place", null,
                b => devicePos == null && !used,
                b => !MyPlayer.IsDead && !used && devicePos == null, false);
            place.SetLabelType(ModAbilityButton.LabelType.Impostor);
            place.OnClick = button =>
            {
                devicePos = new Vector2(MyPlayer.TruePosition.x, MyPlayer.TruePosition.y);
            };

            var activate = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.SecondaryAbility, 0f, "sweeper.activate", null,
                b => devicePos != null && !used,
                b => !MyPlayer.IsDead && devicePos != null && !used, false);
            activate.SetLabelType(ModAbilityButton.LabelType.Impostor);
            activate.OnClick = button =>
            {
                if (devicePos == null) return;
                used = true;
                RpcTornado.Invoke((MyPlayer.PlayerId, tornadoPos.x, tornadoPos.y, RadiusOption, DurationOption));
            };
        }

        /// <summary>龙卷风持续期间，房主每帧判定范围内的人。</summary>
        /// <summary>已发起过击杀请求的玩家。击杀要等房主处理完才会反映到 IsDead，不记下来会每帧重复请求。</summary>
        private readonly HashSet<byte> blown = new();
        private bool hurricaneSent = false;

        public void StartTornado(Vector2 pos, float duration)
        {
            tornadoPos = pos;
            tornadoLeft = duration;
        }

        [OnlyHost]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (tornadoLeft <= 0f) return;
            tornadoLeft -= ev.DeltaTime;

            foreach (var p in GamePlayer.AllPlayers.ToList())
            {
                if (p.IsDead || blown.Contains(p.PlayerId)) continue;
                if (p == MyPlayer && !KillsSelfOption) continue;
                float dist = Vector2.Distance(tornadoPos, new Vector2(p.TruePosition.x, p.TruePosition.y));
                if (dist > RadiusOption) continue;
                blown.Add(p.PlayerId);
                p.Suicide(HalfSugarGift.Core.Patch.ExState.BlownAway, EventDetail.Kill, KillParameter.NormalKill);
            }

            if (!hurricaneSent && blown.Count >= 4) { hurricaneSent = true; RpcHurricane.Invoke(MyPlayer.PlayerId); }
        }
    }

    static internal readonly RemoteProcess<byte> RpcHurricane = new("HSG.Sweeper.Hurricane", (sweeperId, _) =>
    {
        if (GamePlayer.GetPlayer(sweeperId)?.AmOwner == true)
            new StaticAchievementToken("sweeper.hurricane");
    });
}
