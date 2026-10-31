namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 测谎师（船员）。
///
/// 表格描述：「可以在会议上使用测谎技能，将玩家与对应字眼匹配（某一职业、杀死某人），
/// 可以得到是或否的答案」。
///
/// 操作方式（都用会议里的玩家按钮完成，不需要额外 UI）：
/// 1. 第一次点击 = 选定被测谎的人；
/// 2. 再点一次同一个人 = 职业测谎，弹出职业列表让你挑一个断言；
/// 3. 点另一个人 = 击杀测谎，问「他是不是杀了这个人」。
/// </summary>
public class Polygraph : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration UsesPerMeetingOption = NebulaAPI.Configurations.Configuration(
        "options.role.polygraph.usesPerMeeting", (1, 3), 1);
    static private readonly IntegerConfiguration RoleCandidateOption = NebulaAPI.Configurations.Configuration(
        "options.role.polygraph.roleCandidates", (3, 12), 6);

    private Polygraph() : base(
        "polygraph", new Virial.Color(0.2f, 0.75f, 0.7f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [UsesPerMeetingOption, RoleCandidateOption])
    { }

    public static readonly Polygraph MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int usesThisMeeting;
        private GamePlayer? subject;
        /// <summary>本局的击杀记录，用来回答「他是不是杀了某人」。</summary>
        // 原为 static：跨局不清空，下一局会用上一局的击杀记录作答（玩家 id 会复用）
        private readonly Dictionary<byte, byte> KillLog = new();

        void RuntimeAssignable.OnActivated() { }

        /// <summary>记录全场击杀，供击杀测谎使用。</summary>
        void OnKill(PlayerKillPlayerEvent ev)
        {
            if (ev.Murderer == null || ev.Dead == null) return;
            KillLog[ev.Dead.PlayerId] = ev.Murderer.PlayerId;
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            usesThisMeeting = 0;
            subject = null;
            if (MyPlayer.IsDead) return;

            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(HalfSugarGift.Core.Patch.ExHelpers.MeetingIconOf(3),
                p =>
                {
                    if (subject == null)
                    {
                        subject = p.MyPlayer;
                        foreach (var s in buttonManager!.AllStates) s.SetSelect(s.MyPlayer == subject);
                        return;
                    }

                    if (p.MyPlayer == subject) OpenRoleQuestion(subject);
                    else AnswerKillQuestion(subject, p.MyPlayer);

                    subject = null;
                    foreach (var s in buttonManager!.AllStates) s.Reset();
                },
                p => usesThisMeeting < UsesPerMeetingOption && !MyPlayer.IsDead
                     && !NebulaN.Roles.Modifier.Smokescreen.IsProtected(p.MyPlayer),
                false));
        }

        /// <summary>职业测谎：挑一个职业断言，回答是/否。</summary>
        private void OpenRoleQuestion(GamePlayer target)
        {
            var candidates = (Nebula.Roles.Roles.AllRoles ?? new List<DefinedRole>())
                .Where(r => !r.IsSystemRole && r.IsSpawnable)
                .OrderBy(_ => Guid.NewGuid())
                .Take(RoleCandidateOption)
                .ToList();
            // 保证真答案在候选里，否则永远只能得到「否」
            if (!candidates.Contains(target.Role.Role)) candidates[0] = target.Role.Role;
            candidates = candidates.OrderBy(_ => Guid.NewGuid()).ToList();

            var tabs = new (string? tab, Predicate<DefinedRole>? predicate)[] { (null, _ => true) };
            MetaScreen? screen = null;
            PatchManager.OpenRoleSelectWindowUsingTabs(
                candidates, tabs, false,
                Language.Translate("role.polygraph.selectRole"),
                selected =>
                {
                    screen?.CloseScreen();
                    usesThisMeeting++;
                    bool yes = target.Role.Role == selected;
                    ShowAnswer(Language.Translate("role.polygraph.q.role")
                        .Replace("%NAME%", target.Name)
                        .Replace("%ROLE%", selected.DisplayName), yes);
                },
                ref screen, true);
        }

        /// <summary>击杀测谎：他是不是杀了那个人。</summary>
        private void AnswerKillQuestion(GamePlayer suspect, GamePlayer victim)
        {
            usesThisMeeting++;
            bool yes = KillLog.TryGetValue(victim.PlayerId, out var killerId) && killerId == suspect.PlayerId;
            ShowAnswer(Language.Translate("role.polygraph.q.kill")
                .Replace("%NAME%", suspect.Name)
                .Replace("%VICTIM%", victim.Name), yes);
        }

        private void ShowAnswer(string question, bool yes)
        {
            if (yes) new StaticAchievementToken("polygraph.truth");
            string answer = Language.Translate(yes ? "role.polygraph.yes" : "role.polygraph.no");
            HalfSugarGift.Core.Patch.ExHelpers.ShowMeetingOverlay(
                "role.polygraph.name",
                question + "<br><b>" + answer + "</b>",
                MyRole.RoleColor);
        }
    }
}
