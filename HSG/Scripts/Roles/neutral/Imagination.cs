using Nebula.Configuration;
using NebulaN.Roles.Modifier;

namespace NebulaN.Roles.Neutral
{
    public class Imagination : DefinedRoleTemplate, DefinedRole, DefinedAssignable, IAssignableDocument, HasCitation
    {
        public static readonly RoleTeam MyTeam = HalfSugarGift.Core.Patch.Team.ImaginationTeam;
        private static readonly HashSet<DefinedRole> SelectedRoles = new();

        /// <summary>胜利抢夺是否已经触发过（Role 与 Modifier 共享，防止双抢）。</summary>
        internal static bool WinTriggered = false;

        public static HashSet<DefinedRole> GetSelectedRoles()
        {
            return SelectedRoles;
        }

        static IntegerConfiguration ChooseCount = NebulaAPI.Configurations.Configuration(
            "options.role.imagination.candidateCount", (1, 10), 4);

        public static readonly SimpleRoleFilterConfiguration RoleFilter =
            new SimpleRoleFilterConfiguration("options.role.imagination.filter")
            {
                RolePredicate = r => !r.IsSystemRole &&
                    (r.Category == RoleCategory.CrewmateRole || r.Category == RoleCategory.ImpostorRole),
                ScrollerTag = "imagineFilter",
                InvertOption = true,
                PreviewOnlySpawnableRoles = false
            };

        Imagination() : base(
            "imagination",
            Cor.ImaginationCor,
            RoleCategory.NeutralRole,
            MyTeam,
            [RoleFilter,ChooseCount]
        )
        {
            ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/ImaginationPic.png")?.AsImage(115f);
        }
        Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/MaskedDancerIcon.png")?.AsImage();
        public static readonly Imagination MyRole = new();
        public Citation Citation => Citations.hvtXsvc_hsg;
        public static int CandidateCount => ChooseCount;
        RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments)
            => new Instance(player);
        IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
        {
            yield return new AssignableDocumentImage(
                NebulaAPI.AddonAsset.GetResource("imagine.png")?.AsImage(115f),
                "role.imagination.ability.doc"
            );
        }
        bool IAssignableDocument.HasAbility => true;
        bool IAssignableDocument.HasTips => true;
        /// <summary>
        /// 候选角色池（供想象力本体与转职后的修饰符共用）。
        /// 使用 RoleFilter 配置过滤，并排除本局已选过的角色。
        /// </summary>
        internal static List<DefinedRole> CollectCandidates()
        {
            var allRoles = Nebula.Roles.Roles.AllRoles ?? new List<DefinedRole>();
            return allRoles
                .Where(r =>
                    !r.IsSystemRole &&
                    (r.Category == RoleCategory.CrewmateRole || r.Category == RoleCategory.ImpostorRole) &&
                    RoleFilter.Contains(r) &&
                    !SelectedRoles.Contains(r))
                .OrderBy(_ => Guid.NewGuid())
                .Take(CandidateCount)
                .ToList();
        }

        /// <summary>想象力胜利条件：本人存活且场上没有任何杀手（内鬼 / 中立杀手）存活。</summary>
        internal static bool CanWin(GamePlayer player)
            => !player.IsDead && !GamePlayer.AllPlayers.Any(p => !p.IsDead && !p.IsDisconnected && p.Role.Role.IsKiller);

        /// <summary>房主侧：在结算发生时，若满足想象力胜利条件则覆盖为想象力胜利。</summary>
        internal static void TryOverwriteWin(EndCriteriaMetEvent ev, GamePlayer player)
        {
            if (WinTriggered || !CanWin(player)) return;
            // 场上可能同时有多名想象力（本体或已转职），一起算作胜者
            var winners = BitMasks.AsPlayer();
            foreach (var p in GamePlayer.AllPlayers)
            {
                if (p.IsDead) continue;
                if (p.Role.Role == MyRole || p.TryGetModifier<ImaginationModifier.Instance>(out _)) winners.Add(p);
            }
            ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.Team.ImaginationWin, 80, GameEndReason.Special, (int)winners.AsRawPattern);
            WinTriggered = true;
        }

        public class Instance : RuntimeAssignableTemplate, RuntimeRole
        {
            DefinedRole RuntimeRole.Role => MyRole;
            public Instance(GamePlayer player) : base(player) { }

            void RuntimeAssignable.OnActivated() { }

            // 候选池与抢夺标志都是静态的，必须每局重置，否则多局之后候选耗尽直接"抑郁"
            void OnGameStart(GameStartEvent ev)
            {
                SelectedRoles.Clear();
                WinTriggered = false;
            }

            [Local]
            void OnMeetingEnd(MeetingEndEvent ev)
            {
                if (MyPlayer.IsDead) return;
                OpenSelectGUI();
            }

            void OpenSelectGUI(bool showCloseButton = false)
            {
                var candidateRoles = CollectCandidates();
                if (candidateRoles.Count == 0)
                {
                    MyPlayer.Suicide(State.Depression, null, KillParameter.NormalKill, null);
                    return;
                }
                var tabs = new (string? tab, Predicate<DefinedRole>? predicate)[]
                {
                    (null, _ => true)
                };

                MetaScreen result = null;
                PatchManager.OpenRoleSelectWindowUsingTabs(
                    candidateRoles,
                    tabs,
                    true,
                    Language.Translate("role.imagination.select"),
                    (DefinedRole selectedRole) =>
                    {
                        SelectedRoles.Add(selectedRole);
                        result?.CloseScreen();
                        MyPlayer.SetRole(selectedRole, selectedRole.DefaultAssignableArguments ?? []);
                        // 想象力身份延续标记：换角色后 Modifier 不随 SetRole 卸载，只加一次防堆叠
                        if (!MyPlayer.TryGetModifier<ImaginationModifier.Instance>(out _))
                            MyPlayer.AddModifier(ImaginationModifier.MyRole, null);
                    },
                    ref result,
                    showCloseButton
                );
            }

            [OnlyMyPlayer]
            void OnCheckWin(PlayerCheckWinEvent ev)
            {
                if (ev.GameEnd != HalfSugarGift.Core.Patch.Team.ImaginationWin) return;
                ev.SetWinIf(CanWin(MyPlayer));
            }

            // 原实现只要想象者存活就无条件覆盖为想象力胜利（内鬼胜利也会被抢走）；现在只在无杀手存活时覆盖
            [OnlyHost]
            void OnEndCriteriaMet(EndCriteriaMetEvent ev) => TryOverwriteWin(ev, MyPlayer);
        }
    }
}
