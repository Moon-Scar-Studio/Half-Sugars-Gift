using Nebula.Configuration;
using NebulaN.Roles.Modifier;

namespace NebulaN.Roles.Neutral
{
    public class Imagination : DefinedRoleTemplate, DefinedRole, DefinedAssignable, IAssignableDocument, HasCitation
    {
        public static readonly RoleTeam MyTeam = HalfSugarGift.Core.Patch.Team.ImaginationTeam;
        private static readonly HashSet<DefinedRole> SelectedRoles = new();

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
        public class Instance : RuntimeAssignableTemplate, RuntimeRole
        {
            bool _winTriggered = false;
            DefinedRole RuntimeRole.Role => MyRole;
            public Instance(GamePlayer player) : base(player) { }

            void RuntimeAssignable.OnActivated() { }

            [Local]
            void OnMeetingEnd(MeetingEndEvent ev)
            {
                OpenSelectGUI();
            }
            void OpenSelectGUI(bool showCloseButton = false)
            {
                var myPlayer = MyPlayer;
                var allRoles = Nebula.Roles.Roles.AllRoles ?? new List<DefinedRole>();
                var selectedRoles = Imagination.GetSelectedRoles();

                var candidateRoles = allRoles
                    .Where(r =>
                        !r.IsSystemRole &&
                        (r.Category == RoleCategory.CrewmateRole ||
                         r.Category == RoleCategory.ImpostorRole) &&
                        !selectedRoles.Contains(r)
                    )
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(CandidateCount)
                    .ToList();
                if (candidateRoles.Count == 0)
                {
                    MyPlayer.Suicide(State.Depression,null,KillParameter.NormalKill,null);
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
                        Imagination.GetSelectedRoles().Add(selectedRole);

                        result?.CloseScreen();

                        MyPlayer.SetRole(
                            selectedRole,
                            selectedRole.DefaultAssignableArguments ?? []
                        );

                        MyPlayer.AddModifier(
                            ImaginationModifier.MyRole,
                            null
                        );
                    },
                    ref result,
                    showCloseButton
                );
            }

            [OnlyMyPlayer]
            void OnCheckWin(PlayerCheckWinEvent ev)
            {
                if (ev.GameEnd != HalfSugarGift.Core.Patch.Team.ImaginationWin) return;
                ev.SetWinIf(
                    !MyPlayer.IsDead &&
                    !NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && p.Role.Role.IsKiller)
                );
            }
            [OnlyHost]
            void OnEndCriteriaMet(EndCriteriaMetEvent ev)
            {
                if (_winTriggered || MyPlayer.IsDead) return;
                var winners = BitMasks.AsPlayer();
                winners.Add(MyPlayer);
                ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.Team.ImaginationWin,80,GameEndReason.Special,(int)winners.AsRawPattern);
                _winTriggered = true;
            }
        }
    }
}