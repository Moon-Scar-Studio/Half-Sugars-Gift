namespace NebulaN.Roles.Neutral;

/// <summary>
/// 画师（中立）。
/// 「可以使用技能绘画赠予玩家一个画作，被赠予画作的玩家将会在剩余指定人数时变为画师的粉丝，
/// 与画师站在统一战线，绘画有次数限制。」
/// </summary>
public class Artist : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration MaxPaintingsOption = NebulaAPI.Configurations.Configuration(
        "options.role.artist.maxPaintings", (1, 6), 2);
    static private readonly IntegerConfiguration ConvertThresholdOption = NebulaAPI.Configurations.Configuration(
        "options.role.artist.convertThreshold", (2, 10), 5);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.artist.cooldown", (10f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);

    private Artist() : base(
        "artist", HalfSugarGift.Core.Patch.ExTeam.ArtistTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.ArtistTeam,
        [MaxPaintingsOption, ConvertThresholdOption, CooldownOption])
    { }

    public static readonly Artist MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte artistId, byte targetId)> RpcBecomeFan =
        new("HSG.Artist.BecomeFan", (msg, _) =>
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (target?.AmOwner != true) return;
            target.AddModifierOnce<NebulaN.Roles.Modifier.Admirer.Instance>(
                NebulaN.Roles.Modifier.Admirer.MyRole, [msg.artistId]);
            AmongUsUtil.PlayQuickFlash(HalfSugarGift.Core.Patch.ExTeam.ArtistTeam.Color);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>收到画作、但还没转化成粉丝的人。</summary>
        private readonly List<byte> painted = new();
        private int usesLeft;

        bool RuntimeRole.CanUseVent => true;

        void RuntimeAssignable.OnActivated()
        {
            usesLeft = MaxPaintingsOption;
            if (!AmOwner) return;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId && !painted.Contains(p.PlayerId));
            tracker.SetColor(MyRole.RoleColor);

            var paint = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "artist.paint", null,
                b => tracker.CurrentTarget != null && !b.IsInCooldown && usesLeft > 0,
                b => !MyPlayer.IsDead && usesLeft > 0, false);
            paint.ShowUsesIcon(3, usesLeft.ToString());
            paint.OnClick = button =>
            {
                var target = tracker.CurrentTarget?.RealPlayer;
                if (target == null) return;
                usesLeft--;
                paint.UpdateUsesIcon(usesLeft.ToString());
                painted.Add(target.PlayerId);
                RpcRegisterPainting.Invoke((MyPlayer.PlayerId, target.PlayerId));
                new StaticAchievementToken("artist.masterHand");
                button.StartCoolDown();
            };
        }

        public void RegisterPainting(byte targetId)
        {
            if (!painted.Contains(targetId)) painted.Add(targetId);
        }

        /// <summary>人数掉到阈值以下 → 所有持画者变成粉丝。</summary>
        [OnlyHost]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (painted.Count == 0) return;
            if (HalfSugarGift.Core.Patch.ExHelpers.AlivePlayers.Count() > ConvertThresholdOption) return;

            foreach (var id in painted.ToList())
            {
                var target = GamePlayer.GetPlayer(id);
                if (target == null || target.IsDead) continue;
                RpcBecomeFan.Invoke((MyPlayer.PlayerId, id));
            }
            painted.Clear();
        }

        /// <summary>画师自己看得到谁拿了画。</summary>
        [Local]
        void OnDecorateName(PlayerDecorateNameEvent ev)
        {
            if (!AmOwner) return;
            if (painted.Contains(ev.Player.PlayerId))
                ev.Name += " ✎".Color(MyRole.RoleColor.ToUnityColor());
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.ArtistWin;

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.ArtistWin && !MyPlayer.IsDead);

        /// <summary>画师与粉丝人数压过其他人 → 抢下胜利。</summary>
        [OnlyHost]
        void OnEndCriteriaMet(EndCriteriaMetEvent ev)
        {
            if (MyPlayer.IsDead) return;

            var camp = GamePlayer.AllPlayers
                .Where(p => !p.IsDead && (p == MyPlayer
                    || (p.TryGetModifier<NebulaN.Roles.Modifier.Admirer.Instance>(out var m)
                        && m.ArtistId == MyPlayer.PlayerId)))
                .ToList();
            if (camp.Count * 2 < HalfSugarGift.Core.Patch.ExHelpers.AlivePlayers.Count()) return;

            var winners = BitMasks.AsPlayer();
            foreach (var p in camp) winners.Add(p);
            if (ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.ExTeam.ArtistWin, 52,
                    GameEndReason.Special, (int)winners.AsRawPattern))
                RpcBestPainter.Invoke(MyPlayer.PlayerId);
        }
    }

    static internal readonly RemoteProcess<(byte artistId, byte targetId)> RpcRegisterPainting =
        new("HSG.Artist.RegisterPainting", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.artistId)?.Role is Instance inst) inst.RegisterPainting(msg.targetId);
        });

    static internal readonly RemoteProcess<byte> RpcBestPainter = new("HSG.Artist.BestPainter", (id, _) =>
    {
        if (GamePlayer.GetPlayer(id)?.AmOwner == true) new StaticAchievementToken("artist.bestPainter");
    });
}
