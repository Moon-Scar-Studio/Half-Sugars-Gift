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
                if (MyPlayer.IsDead) return;
                OpenSelectGUI();
            }

            void OpenSelectGUI(bool showCloseButton = false)
            {
                var candidateRoles = Imagination.CollectCandidates();
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
                        Imagination.GetSelectedRoles().Add(selectedRole);
                        result?.CloseScreen();
                        MyPlayer.SetRole(selectedRole, selectedRole.DefaultAssignableArguments ?? []);
                    },
                    ref result,
                    showCloseButton
                );
            }

            [OnlyMyPlayer]
            void OnCheckWin(PlayerCheckWinEvent ev)
            {
                if (ev.GameEnd != HalfSugarGift.Core.Patch.Team.ImaginationWin) return;
                ev.SetWinIf(Imagination.CanWin(MyPlayer));
            }

            // 转职后想象力本体的角色实例已被释放，胜利覆盖逻辑必须在修饰符上继续存在
            [OnlyHost]
            void OnEndCriteriaMet(EndCriteriaMetEvent ev) => Imagination.TryOverwriteWin(ev, MyPlayer);

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
