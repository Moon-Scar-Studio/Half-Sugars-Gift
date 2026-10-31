namespace NebulaN.Roles.Neutral;

/// <summary>
/// 迷途者（中立）。
/// 「开局显示的职业是代理人，完成所有任务后得知自己是迷途者并解锁一把一次性的刀。
/// 出刀后在当轮会议被驱逐即获胜；如果在出刀的当轮会议活了下来，会议结束后立刻暴毙。」
/// </summary>
public class Strayer : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly BoolConfiguration SelfProveTaskOption = NebulaAPI.Configurations.Configuration(
        "options.role.strayer.selfProveTask", true);

    private Strayer() : base(
        "strayer", HalfSugarGift.Core.Patch.ExTeam.StrayerTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.StrayerTeam,
        [SelfProveTaskOption])
    { }

    public static readonly Strayer MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>还没做完任务之前，自己都以为是代理人。</summary>
        private bool awakened = false;
        private bool killedThisRound = false;

        RoleTaskType RuntimeRole.TaskType => RoleTaskType.CrewmateTask;

        /// <summary>觉醒之前对外（也对自己）显示为代理人。</summary>
        string? RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort, bool canSeeAllInfo)
            => awakened || canSeeAllInfo
                ? null
                : Language.Translate(isShort ? "role.agent.short" : "role.agent.name");

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var killTracker = NebulaAPI.Modules.KillTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            var kill = NebulaAPI.Modules.KillButton(
                this, MyPlayer, true, VirtualKeyInput.Kill, null, 0f,
                "kill", ModAbilityButton.LabelType.Impostor, null,
                (target, button) =>
                {
                    MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                    killedThisRound = true;
                    button.Break();
                },
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId,
                b => awakened && !killedThisRound,
                b => !MyPlayer.IsDead && awakened && !killedThisRound);
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(kill.GetKillButtonLike());
        }

        [Local]
        void SetTask(PlayerTasksTrySetLocalEvent ev)
        {
            if (ev.Player.Role.Role != MyRole) return;
            if (SelfProveTaskOption) ev.AddExtraQuota(1);
        }

        [Local]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent ev)
        {
            if (!AmOwner || awakened) return;
            if (!MyPlayer.Tasks.IsCompletedCurrentTasks) return;
            awakened = true;
            new StaticAchievementToken("strayer.awake");
            AmongUsUtil.PlayQuickFlash(MyRole.RoleColor);
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.StrayerWin;

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.StrayerWin);

        /// <summary>出刀的那轮被驱逐 → 获胜。</summary>
        // 「本轮杀过人」必须在所有客户端同步：放逐获胜与会议后暴毙都由房主判定，
        // 原来只在本人客户端置位，房主那边永远是 false。
        void OnAnyKill(PlayerKillPlayerEvent ev)
        {
            if (ev.Murderer == MyPlayer && ev.Dead != MyPlayer) killedThisRound = true;
        }

        [OnlyHost, OnlyMyPlayer]
        void OnExiled(PlayerExiledEvent ev)
        {
            if (!killedThisRound) return;
            NebulaAPI.CurrentGame?.TriggerGameEnd(
                HalfSugarGift.Core.Patch.ExTeam.StrayerWin,
                GameEndReason.Special, BitMasks.AsPlayer(1u << MyPlayer.PlayerId));
        }

        /// <summary>出刀那轮活下来了 → 会议结束立刻暴毙。</summary>
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (!killedThisRound || MyPlayer.IsDead) return;
            killedThisRound = false;
            if (!AmongUsClient.Instance.AmHost) return;
            MyPlayer.Suicide(HalfSugarGift.Core.Patch.ExState.Strayed, EventDetail.Kill, KillParameter.NormalKill);
        }

        /// <summary>一局结束还没觉醒 → 傻子成就。</summary>
        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (AmOwner && !awakened) new StaticAchievementToken("strayer.fool");
        }
    }
}
