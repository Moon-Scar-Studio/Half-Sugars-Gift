namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 群青（船员）。
///
/// 表格描述：「技能为描摹，使用技能可在会议上将对象的阵营进行二选一公开（字眼为黑白灰）」。
/// 描摹结果只保留一轮会议，可以对同一人多次描摹，一轮也可以描摹多次。
/// </summary>
public class Ultramarine : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration UsesPerMeetingOption = NebulaAPI.Configurations.Configuration(
        "options.role.ultramarine.usesPerMeeting", (1, 5), 1);

    private Ultramarine() : base(
        "ultramarine", new Virial.Color(0.25f, 0.3f, 0.75f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [UsesPerMeetingOption])
    { }

    public static readonly Ultramarine MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>描摹结果是公开的：全场都要看到这两个候选字眼。</summary>
    static internal readonly RemoteProcess<(byte targetId, string wordA, string wordB)> RpcDepict =
        new("HSG.Ultramarine.Depict", (msg, _) =>
        {
            if (GamePlayer.GetPlayer(msg.targetId) == null) return;
            CurrentResults[msg.targetId] = (msg.wordA, msg.wordB);
        });

    /// <summary>本轮会议的描摹结果。会议结束时清空。</summary>
    static internal readonly Dictionary<byte, (string a, string b)> CurrentResults = new();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int usesThisMeeting = 0;

        // CurrentResults 是静态的；若上一局在会议中结束，残留结果会带进下一局
        void RuntimeAssignable.OnActivated() => CurrentResults.Clear();

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            usesThisMeeting = 0;
            if (MyPlayer.IsDead) return;

            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(HalfSugarGift.Core.Patch.ExHelpers.MeetingIconOf(2),
                p =>
                {
                    usesThisMeeting++;
                    var target = p.MyPlayer;
                    // 传翻译键而不是译文：原来发送的是本机语言的文字，其他语言的玩家会看到别国文字
                    string truth = HalfSugarGift.Core.Patch.ExHelpers.CampKey(target);
                    string decoy = PickDecoy(truth);
                    // 二选一：真答案与一个干扰项随机排序后公开
                    bool truthFirst = UnityEngine.Random.value < 0.5f;
                    RpcDepict.Invoke((target.PlayerId,
                        truthFirst ? truth : decoy,
                        truthFirst ? decoy : truth));
                    new StaticAchievementToken("ultramarine.impression");
                },
                p => usesThisMeeting < UsesPerMeetingOption && !MyPlayer.IsDead && !p.MyPlayer.IsDead
                     && !NebulaN.Roles.Modifier.Smokescreen.IsProtected(p.MyPlayer),
                false));
        }

        static string PickDecoy(string truth)
        {
            var all = new[] { "hsg.camp.black", "hsg.camp.white", "hsg.camp.grey" };
            var others = all.Where(w => w != truth).ToList();
            return others[UnityEngine.Random.Range(0, others.Count)];
        }

        void OnMeetingEnd(MeetingEndEvent ev) => CurrentResults.Clear();

        /// <summary>把描摹结果贴在被描摹者名字后面，所有人可见。</summary>
        void OnDecorateName(PlayerDecorateNameEvent ev)
        {
            if (!CurrentResults.TryGetValue(ev.Player.PlayerId, out var words)) return;
            ev.Name += $" [{Language.Translate(words.a)}/{Language.Translate(words.b)}]".Color(MyRole.RoleColor.ToUnityColor());
        }
    }
}
