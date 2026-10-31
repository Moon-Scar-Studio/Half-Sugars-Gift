namespace NebulaN.Roles.Neutral;

/// <summary>
/// 欢愉者（中立）。
/// 「你可以使用技能把任务给破坏，如果有玩家做了被你破坏的任务，那将会死亡。设计杀害 x 个玩家获胜。」
///
/// 实现说明：
/// - 破坏的对象是具体的<b>任务点（Console）</b>。欢愉者走到任务点旁边，按钮会锁定最近的一个（有描边提示）。
///   任务点用它在 ShipStatus.AllConsoles 里的下标标识，所有客户端地图相同，下标一致。
/// - 判定在<b>做任务的人自己的客户端</b>进行：每帧查看本地打开的 Minigame，
///   若它的 Console 是被破坏的任务点，就「记录」这次交互（记下当时的任务步数）；
///   按设置，在「打开时」或「这次交互推进了任务进度时」上报房主。
///   不需要 Harmony 补丁：Minigame.Instance / MyTask / Console 都是原版现成的字段。
/// - 房主仲裁：同一个陷阱只会生效一次（两个人同时触发时只有先到的那个死），
///   房主广播结果后，由受害者本机执行自杀，所有客户端同步计数。
/// </summary>
public class Reveler : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration KillsToWinOption = NebulaAPI.Configurations.Configuration(
        "options.role.reveler.killsToWin", (1, 8), 3);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.reveler.cooldown", (10f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly ValueConfiguration<int> TriggerTimingOption = NebulaAPI.Configurations.Configuration(
        "options.role.reveler.triggerTiming", new string[]
        {
            "options.role.reveler.triggerTiming.progress",
            "options.role.reveler.triggerTiming.open",
        }, 0);

    private Reveler() : base(
        "reveler", HalfSugarGift.Core.Patch.ExTeam.RevelerTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.RevelerTeam,
        [KillsToWinOption, CooldownOption, TriggerTimingOption])
    { }

    public static readonly Reveler MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    #region 任务点工具

    static Console[] AllConsoles()
    {
        var ship = ShipStatus.Instance;
        if (!ship) return [];
        return ship.AllConsoles.ToArray();
    }

    static int IndexOfConsole(Console console)
    {
        var ship = ShipStatus.Instance;
        if (!ship || !console) return -1;
        var all = ship.AllConsoles;
        int id = console.GetInstanceID();
        for (int i = 0; i < all.Length; i++)
            if (all[i] && all[i].GetInstanceID() == id) return i;
        return -1;
    }

    /// <summary>只把「真正的任务」算作被破坏的任务（排除修复破坏用的控制台等）。</summary>
    static bool IsTaskMinigame(Minigame minigame) =>
        minigame.MyTask && minigame.MyTask.TryCast<NormalPlayerTask>() != null;

    #endregion

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>被破坏的任务点下标（全体客户端同步）。</summary>
        private readonly HashSet<int> traps = new();
        private int designedKills = 0;

        bool RuntimeRole.CanUseVent => true;

        // ---- 所有者：锁定最近的任务点 ----
        private Console? currentTarget = null;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var sabotage = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "reveler.sabotage", null,
                b => !b.IsInCooldown && currentTarget != null,
                b => !MyPlayer.IsDead, false);
            sabotage.OnClick = button =>
            {
                if (currentTarget == null) return;
                int index = IndexOfConsole(currentTarget);
                if (index < 0 || traps.Contains(index)) return;
                RpcSetTrap.Invoke((MyPlayer.PlayerId, index));
                button.StartCoolDown();
            };

            Helpers.TextHudContent("RevelerText", this,
                tmPro => tmPro.text = Language.Translate("role.reveler.progress")
                    .Replace("%NUM%", designedKills.ToString())
                    .Replace("%TOTAL%", ((int)KillsToWinOption).ToString())
                    + "\n" + Language.Translate("role.reveler.trapCount").Replace("%NUM%", traps.Count.ToString()),
                true);
        }

        void UpdateTargetConsole()
        {
            Console? best = null;
            if (!MyPlayer.IsDead && !MeetingHud.Instance)
            {
                var pos = MyPlayer.TruePosition;
                float bestDis = float.MaxValue;
                var consoles = AllConsoles();
                for (int i = 0; i < consoles.Length; i++)
                {
                    var c = consoles[i];
                    if (!c || traps.Contains(i)) continue;
                    var cp = c.transform.position;
                    float dis = Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(cp.x, cp.y));
                    if (dis > c.UsableDistance || dis >= bestDis) continue;
                    best = c; bestDis = dis;
                }
            }

            if (currentTarget && currentTarget != best) currentTarget!.SetOutline(false, false);
            currentTarget = best;
            if (currentTarget) currentTarget!.SetOutline(true, true);
        }

        // ---- 本地玩家：检测是否在被破坏的任务点上做任务 ----
        private Minigame? trackingMinigame = null;
        private NormalPlayerTask? trackingTask = null;
        private int trackingIndex = -1;
        private int trackingStep = 0;
        private bool reported = false;

        void ResetTracking()
        {
            trackingMinigame = null;
            trackingTask = null;
            trackingIndex = -1;
            reported = false;
        }

        void CheckLocalTrap()
        {
            var local = GamePlayer.LocalPlayer;
            if (local == null || local.IsDead || local == MyPlayer) { ResetTracking(); return; }

            var minigame = Minigame.Instance;
            if (!minigame || !minigame.Console || !IsTaskMinigame(minigame)) { ResetTracking(); return; }

            // 新打开了一个小游戏：记录这次交互
            if (trackingMinigame == null || trackingMinigame.GetInstanceID() != minigame.GetInstanceID())
            {
                ResetTracking();
                int index = IndexOfConsole(minigame.Console);
                if (index < 0 || !traps.Contains(index)) return;

                trackingMinigame = minigame;
                trackingTask = minigame.MyTask.TryCast<NormalPlayerTask>();
                trackingIndex = index;
                trackingStep = trackingTask?.taskStep ?? 0;

                if (TriggerTimingOption.GetValue() == 1) Report();
                return;
            }

            if (reported || trackingTask == null) return;
            // 这次交互推进了任务（步数增加或任务完成）→ 上报
            if (trackingTask.IsComplete || trackingTask.taskStep > trackingStep) Report();
        }

        void Report()
        {
            if (reported || trackingIndex < 0) return;
            reported = true;
            RpcTriggerTrap.Invoke((MyPlayer.PlayerId, trackingIndex, GamePlayer.LocalPlayer!.PlayerId));
        }

        void OnUpdate(GameUpdateEvent ev)
        {
            if (AmOwner) UpdateTargetConsole();
            if (traps.Count > 0) CheckLocalTrap();
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (currentTarget) currentTarget!.SetOutline(false, false);
            currentTarget = null;
        }

        void RuntimeAssignable.OnInactivated()
        {
            if (currentTarget) currentTarget!.SetOutline(false, false);
        }

        // ---- 同步 ----
        internal void AddTrap(int index) => traps.Add(index);
        internal bool HasTrap(int index) => traps.Contains(index);

        internal void OnTrapFired(int index, GamePlayer victim)
        {
            traps.Remove(index);
            designedKills++;
            if (AmOwner)
            {
                new StaticAchievementToken("reveler.mission");
                if (designedKills >= KillsToWinOption) new StaticAchievementToken("reveler.iAmJoy");
            }
            if (victim.AmOwner && !victim.IsDead)
                victim.Suicide(HalfSugarGift.Core.Patch.ExState.Revel, EventDetail.Kill, KillParameter.NormalKill);
        }

        // ---- 胜利 ----
        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.RevelerWin;

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.RevelerWin
                           && designedKills >= KillsToWinOption);

        [OnlyHost]
        void OnEndCriteriaMet(EndCriteriaMetEvent ev)
        {
            if (designedKills < KillsToWinOption) return;
            var winners = BitMasks.AsPlayer();
            winners.Add(MyPlayer);
            ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.ExTeam.RevelerWin, 56,
                GameEndReason.Special, (int)winners.AsRawPattern);
        }
    }

    /// <summary>破坏任务点，全体客户端同步（判定在做任务的人本机进行，所以所有人都要知道）。</summary>
    static internal readonly RemoteProcess<(byte revelerId, int consoleIndex)> RpcSetTrap =
        new("HSG.Reveler.SetConsoleTrap", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.revelerId)?.Role is Instance inst) inst.AddTrap(msg.consoleIndex);
        });

    /// <summary>某人在被破坏的任务点上做了任务，请求房主仲裁。</summary>
    static internal readonly RemoteProcess<(byte revelerId, int consoleIndex, byte victimId)> RpcTriggerTrap =
        new("HSG.Reveler.TriggerTrap", (msg, _) =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (GamePlayer.GetPlayer(msg.revelerId)?.Role is not Instance inst) return;
            if (!inst.HasTrap(msg.consoleIndex)) return; // 已被别人先触发
            var victim = GamePlayer.GetPlayer(msg.victimId);
            if (victim == null || victim.IsDead) return;
            RpcTrapFired.Invoke((msg.revelerId, msg.consoleIndex, msg.victimId));
        });

    /// <summary>房主确认：陷阱生效。全体移除陷阱并计数，受害者本机自杀。</summary>
    static internal readonly RemoteProcess<(byte revelerId, int consoleIndex, byte victimId)> RpcTrapFired =
        new("HSG.Reveler.TrapFired", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.revelerId)?.Role is not Instance inst) return;
            var victim = GamePlayer.GetPlayer(msg.victimId);
            if (victim == null) return;
            inst.OnTrapFired(msg.consoleIndex, victim);
        });
}
