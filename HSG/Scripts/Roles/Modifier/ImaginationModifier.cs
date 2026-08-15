using NebulaN.Roles.Neutral;

namespace NebulaN.Roles.Modifier
{
    public class ImaginationModifier : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, IAssignableDocument, HasCitation
    {
        public static readonly ImaginationModifier MyRole = new();
        bool DefinedModifier.IsMadmate => false;
        
        ImaginationModifier() : base("imaginationM", "iM", Cor.ImaginationCor) { ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/ImaginationPic.png")?.AsImage(115f); }
        Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/MaskedDancerIcon.png")?.AsImage();
        public Citation Citation => Citations.hvtXsvc_hsg;

        RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
            => new Instance(player);
        bool ISpawnable.IsSpawnable => false;
        IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
        {
            yield return new AssignableDocumentImage(
                NebulaAPI.AddonAsset.GetResource("imagine.png")?.AsImage(115f),
                "role.imaginationM.ability.doc"
            );
        }
        bool IAssignableDocument.HasAbility => true;
        bool IAssignableDocument.HasTips => true;
        public class Instance : RuntimeAssignableTemplate, RuntimeModifier
        {
            DefinedModifier RuntimeModifier.Modifier => MyRole;
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
                    .Take(Imagination.CandidateCount)
                    .ToList();
                if (candidateRoles.Count == 0)
                {
                    MyPlayer.Suicide(State.Depression, null, KillParameter.NormalKill, null);
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
            string RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort, bool canSeeAllInfo)
            {
                if (canSeeAllInfo || AmOwner)
                {
                    var currentRole = MyPlayer.Role.Role;
                    if (currentRole is not Imagination)
                        return Language.Translate("role.imagination.prefix")
                            .Replace("%ROLE%", currentRole.DisplayColoredName);
                }
                return null;
            }
        }
    }
}