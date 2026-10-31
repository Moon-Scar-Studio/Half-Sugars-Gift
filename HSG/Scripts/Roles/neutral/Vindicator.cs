namespace NebulaN.Roles.Neutral;

/// <summary>
/// 寻仇者（中立，表格里写作「F寻仇者」）。
/// 「自由阶段死亡时尸体消失，并在下一轮自由阶段开始时获得易怒状态 30s，期间禁止拉铃，
/// 获得凶手位置信息并试图报仇（击杀）。若刀错，凶手阵营获胜。超时死亡。
/// 报仇成功则跟随胜利阵营获胜；超时的话，若凶手在当轮会议中被票出，寻仇者单独获胜。」
/// </summary>
public class Vindicator : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration RageDurationOption = NebulaAPI.Configurations.Configuration(
        "options.role.vindicator.rageDuration", (10f, 90f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration RageSpeedOption = NebulaAPI.Configurations.Configuration(
        "options.role.vindicator.rageSpeed", (1f, 2f, 0.05f), 1.2f, decorator: v => v + "x");

    private Vindicator() : base(
        "vindicator", HalfSugarGift.Core.Patch.ExTeam.VindicatorTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.VindicatorTeam,
        [RageDurationOption, RageSpeedOption])
    { }

    public static readonly Vindicator MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static internal readonly RemoteProcess<(byte vindicatorId, byte killerId, float x, float y)> RpcBeginRevenge =
        new("HSG.Vindicator.BeginRevenge", (msg, _) =>
        {
            var v = GamePlayer.GetPlayer(msg.vindicatorId);
            if (v == null) return;
            if (AmongUsClient.Instance.AmHost)
                v.Revive(null, new Virial.Compat.Vector2(msg.x, msg.y), true, false);
            if (v.Role is Instance inst) inst.BeginRevenge(msg.killerId);
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private byte killerId = 255;
        /// <summary>等待下一个自由阶段开始复仇。</summary>
        private bool pendingRevenge = false;
        private float rageLeft = 0f;
        private bool succeeded = false;
        private bool failed = false;
        private TrackingArrowAbility? arrow;

        int[] RuntimeAssignable.RoleArguments => [killerId];

        /// <summary>易怒状态期间禁止拉铃。</summary>
        bool RuntimeRole.CanUseVent => false;

        void RuntimeAssignable.OnActivated() { }

        /// <summary>自由阶段被杀 → 尸体消失，记下凶手。</summary>
        [OnlyMyPlayer, OnlyHost]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (AmongUsUtil.InMeeting) return;
            killerId = ev.Murderer?.PlayerId ?? (byte)255;
            if (killerId == 255) return;
            pendingRevenge = true;
            PatchManager.RpcRemoveBody.Invoke(MyPlayer);
        }

        /// <summary>下一轮自由阶段开始 → 起身复仇。</summary>
        [OnlyHost]
        void OnTaskPhaseStart(TaskPhaseStartEvent ev)
        {
            if (!pendingRevenge) return;
            pendingRevenge = false;

            var killer = GamePlayer.GetPlayer(killerId);
            if (killer == null || killer.IsDead)
            {
                // 凶手已经不在场了（比如当轮被票出）→ 寻仇者单独获胜
                NebulaAPI.CurrentGame?.TriggerGameEnd(
                    HalfSugarGift.Core.Patch.ExTeam.VindicatorWin,
                    GameEndReason.Special, BitMasks.AsPlayer(1u << MyPlayer.PlayerId));
                return;
            }
            RpcBeginRevenge.Invoke((MyPlayer.PlayerId, killerId,
                MyPlayer.TruePosition.x, MyPlayer.TruePosition.y));
        }

        public void BeginRevenge(byte killer)
        {
            killerId = killer;
            rageLeft = RageDurationOption;
            if (!AmOwner) return;

            // 凶手位置信息
            var target = GamePlayer.GetPlayer(killer);
            if (target != null)
            {
                arrow = new TrackingArrowAbility(target, 0f, MyRole.RoleColor, false);
                arrow.Register(this);
            }

            var killTracker = NebulaAPI.Modules.KillTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            var revenge = NebulaAPI.Modules.KillButton(
                this, MyPlayer, true, VirtualKeyInput.Kill, null, 0f,
                "vindicator.revenge", ModAbilityButton.LabelType.Impostor, null,
                (t, button) =>
                {
                    MyPlayer.MurderPlayer(t, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    if (t.PlayerId == killerId)
                    {
                        succeeded = true;
                        new StaticAchievementToken("vindicator.revenge");
                    }
                    else
                    {
                        failed = true;
                        new StaticAchievementToken("vindicator.innocent");
                    }
                    rageLeft = 0f;
                    RpcResolve.Invoke((MyPlayer.PlayerId, succeeded));
                },
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId,
                b => rageLeft > 0f,
                b => rageLeft > 0f);
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(revenge.GetKillButtonLike());
        }

        public void Resolve(bool success)
        {
            succeeded = success;
            failed = !success;
            rageLeft = 0f;
        }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner || rageLeft <= 0f) return;
            rageLeft -= ev.DeltaTime;
            MyPlayer.GainSpeedAttribute(RageSpeedOption, 0.25f, false, 0, "vindicator:rage");
            if (rageLeft <= 0f)
            {
                rageLeft = 0f;
                // 超时死亡
                MyPlayer.Suicide(HalfSugarGift.Core.Patch.ExState.Unavenged, EventDetail.Kill, KillParameter.NormalKill);
            }
        }

        /// <summary>易怒状态期间不允许召集会议。</summary>
        [Local]
        void OnCheckEmergency(CheckCanPushEmergencyButtonEvent ev)
        {
            if (rageLeft > 0f) ev.DenyButton("role.vindicator.noButton");
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
        {
            // 刀错人 → 凶手阵营获胜，自己什么都拿不到
            if (failed) ev.IsBlocked = true;
        }

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            if (failed) return;
            if (ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.VindicatorWin) { ev.SetWinIf(true); return; }
            // 报仇成功 → 跟随最终胜利阵营
            if (succeeded) ev.SetWinIf(true);
        }
    }

    /// <summary>报仇结果同步（原来处理器是空的，胜负判定在其它客户端上读不到成败）。</summary>
    static internal readonly RemoteProcess<(byte vindicatorId, bool success)> RpcResolve =
        new("HSG.Vindicator.Resolve", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.vindicatorId)?.Role is Instance inst) inst.Resolve(msg.success);
        });
}
