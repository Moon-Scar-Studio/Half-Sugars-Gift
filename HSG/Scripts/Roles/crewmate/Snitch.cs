namespace NebulaN.Roles.Crewmate;

public class Snitch : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static BoolConfiguration CanBeGuess = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.canBeGuess",
         false
        );

    static ValueConfiguration<int> ZhixiangJiGeLang = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.zhixiangJiGeLang",
        new[] { "options.role.snitch.all", "options.role.snitch.one", "options.role.snitch.two", "options.role.snitch.three", "options.role.snitch.four", "options.role.snitch.five" },
        1
        );
    static BoolConfiguration UseSpeTask = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.useSpeTask",
        false
        );
    static IntegerConfiguration TaskShuLiang = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.TaskShuLiang",
        (1, 15),
        7,
        () => UseSpeTask
        );
    static ValueConfiguration<int> NeCo = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.NeCo",
        new[] {
            "options.role.snitch.neutral.any",
            "options.role.snitch.neutral.OnlyHUAIDE",
            "options.role.snitch.neutral.none"
        },
        2
        );

    private Snitch() : base(
        "snitch",
        Cor.green,
        RoleCategory.CrewmateRole,
        NebulaTeams.CrewmateTeam,
        new Virial.Configuration.IConfiguration[]
        {
            CanBeGuess, ZhixiangJiGeLang, UseSpeTask, TaskShuLiang, NeCo,
        }
    )
    {
    }

    public Citation Citation => Citations.TheOtherRoles;

    public static readonly Snitch MyRole = new Snitch();
    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => false;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield break;
    }
    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        string canguess = CanBeGuess ? Language.Translate("role.snitch.canBeGuess.true") : Language.Translate("role.snitch.canBeGuess.false");
        yield return new AssignableDocumentReplacement("%CanBeGuessed%", canguess);

        string imps;
        int impss = ZhixiangJiGeLang.GetValue();
        if (impss == 0)
            imps = Language.Translate("role.snitch.imposter.all");
        else
            imps = Language.Translate("role.snitch.imposter.count").Replace("%COUNT%", impss.ToString());

        string neutraltext;
        int neutrals = NeCo.GetValue();
        if (neutrals == 0)
            neutraltext = Language.Translate("role.snitch.neutral.any");
        else if (neutrals == 1)
            neutraltext = Language.Translate("role.snitch.neutral.badOnly");
        else
            neutraltext = Language.Translate("role.snitch.neutral.none");
        yield return new AssignableDocumentReplacement("%ImpInfo%", imps);
        yield return new AssignableDocumentReplacement("%NeuInfo%", neutraltext);
    }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static RemoteProcess<byte> RpcFlash = new("RpcFlash", (targetId, _) =>
    {
        if (GamePlayer.GetPlayer(targetId)?.AmOwner == true)
            AmongUsUtil.PlayQuickFlash(Cor.green);
    });

    static RemoteProcess<byte> RpcFlashRed = new("RpcFlashRed", (targetId, _) =>
    {
        if (GamePlayer.GetPlayer(targetId)?.AmOwner == true)
            AmongUsUtil.PlayQuickFlash(Cor.impRed);
    });

    static RemoteProcess<byte> RpcCreateArrowToSnitch = new("RpcCreateArrowToSnitch", (targetId, _) =>
    {
        var target = GamePlayer.GetPlayer(targetId);
        var snitch = GamePlayer.AllPlayers.FirstOrDefault(p => p.Role is Snitch.Instance);
        if (snitch != null && target != null && target.AmOwner)
        {
            var arrow = new TrackingArrowAbility(snitch, 0f, Virial.Color.Green, false);
            arrow.Register(snitch.Role as Instance);
            var inst = snitch.Role as Instance;
            inst?.AddArrowForTarget(target, arrow);
        }
    });

    static RemoteProcess<(byte snitchId, byte targetId)> RpcCreateRedArrow = new("RpcCreateRedArrow", (msg, _) =>
    {
        var snitch = GamePlayer.GetPlayer(msg.snitchId);
        var target = GamePlayer.GetPlayer(msg.targetId);
        if (snitch?.AmOwner == true && target != null)
        {
            var arrow = new TrackingArrowAbility(target, 0f, Virial.Color.Red, false);
            arrow.Register(snitch.Role as Instance);
            var inst = snitch.Role as Instance;
            inst?.AddArrowForTarget(target, arrow);
        }
    });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int assignedTaskTotal = -1;
        private int completedTasks = 0;
        private bool isWarning = false;
        private bool isTaskCompleted = false;

        internal List<TrackingArrowAbility> Actarr = new List<TrackingArrowAbility>();
        internal Dictionary<byte, List<TrackingArrowAbility>> arrmap = new Dictionary<byte, List<TrackingArrowAbility>>();
        internal Dictionary<byte, List<TrackingArrowAbility>> arrowsPointingToSnitch = new Dictionary<byte, List<TrackingArrowAbility>>();

        public void AddArrowForTarget(GamePlayer target, TrackingArrowAbility arrow)
        {
            if (!arrmap.ContainsKey(target.PlayerId))
                arrmap[target.PlayerId] = new List<TrackingArrowAbility>();
            arrmap[target.PlayerId].Add(arrow);
            Actarr.Add(arrow);
        }

        public void AddArrowPointingToSnitch(GamePlayer owner, TrackingArrowAbility arrow)
        {
            if (!arrowsPointingToSnitch.ContainsKey(owner.PlayerId))
                arrowsPointingToSnitch[owner.PlayerId] = new List<TrackingArrowAbility>();
            arrowsPointingToSnitch[owner.PlayerId].Add(arrow);
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner && !AmOwner)
            {
                throw new Exception("你这黑客。");
            }
        }

        [Local]
        void SetTask(PlayerTasksTrySetLocalEvent ev)
        {
            if (ev.Player.Role.Role != MyRole) return;
            int targetTotal;
            if (UseSpeTask)
            {
                targetTotal = TaskShuLiang;
                while (ev.Tasks.Count > targetTotal)
                {
                    int index = UnityEngine.Random.Range(0, ev.Tasks.Count);
                    ev.Tasks.RemoveAt(index);
                }
                if (ev.Tasks.Count < targetTotal)
                    ev.AddExtraQuota(targetTotal - ev.Tasks.Count);
            }
            else
            {
                targetTotal = ev.Tasks.Count + ev.ExtraQuota;
                if (targetTotal == 0)
                {
                    ev.AddExtraQuota(1);
                    targetTotal = 1;
                }
            }
            assignedTaskTotal = targetTotal;
            completedTasks = 0;
        }

        [Local]
        void TaskComplete(PlayerTaskCompleteLocalEvent ev)
        {
            if (!AmOwner) return;
            if (assignedTaskTotal <= 0) return;
            completedTasks++;
            int left = assignedTaskTotal - completedTasks;
            if (left == 1 && !isWarning)
            {
                isWarning = true;
                tskWarning();
            }
            if (completedTasks >= assignedTaskTotal && !isTaskCompleted)
            {
                isTaskCompleted = true;
                tskComplete();
            }
        }

        void tskWarning()
        {
            AmongUsUtil.PlayQuickFlash(Cor.green);

            var targets = GetNeuAndImp();
            foreach (var target in targets)
            {
                RpcFlash.Invoke(target.PlayerId);
                RpcCreateArrowToSnitch.Invoke(target.PlayerId);
            }
        }

        void tskComplete()
        {
            var impostors = GamePlayer.AllPlayers.Where(p => p.IsImpostor && !p.IsDead).ToList();
            foreach (var imp in impostors)
            {
                RpcFlashRed.Invoke(imp.PlayerId);
            }
            int arrCount = ZhixiangJiGeLang.GetValue();
            if (arrCount == 0) arrCount = impostors.Count;
            else arrCount = Math.Min(arrCount, impostors.Count);
            var selected = impostors.OrderBy(_ => Guid.NewGuid()).Take(arrCount).ToList();
            foreach (var target in selected)
            {
                RpcCreateRedArrow.Invoke((MyPlayer.PlayerId, target.PlayerId));
            }
        }

        List<GamePlayer> GetNeuAndImp()
        {
            var result = new List<GamePlayer>();
            int neutOption = NeCo.GetValue();
            foreach (var p in GamePlayer.AllPlayers)
            {
                if (p.IsDead) continue;
                if (p.IsImpostor)
                {
                    result.Add(p);
                    continue;
                }
                if (p.Role.Role.Category == RoleCategory.NeutralRole)
                {
                    bool isEvil = IsEvilNeu(p.Role.Role);
                    if (neutOption == 0)
                        result.Add(p);
                    else if (neutOption == 1 && isEvil)
                        result.Add(p);
                }
            }
            return result;
        }

        bool IsEvilNeu(DefinedRole role)
        {
            return role.IsKiller;
        }

        [Local]
        void Guessed(PlayerCanGuessPlayerLocalEvent ev)
        {
            if (ev.Target != MyPlayer) return;
            if(!CanBeGuess) ev.CanGuess = false;
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            foreach (var arrow in Actarr)
                arrow.Release();
            Actarr.Clear();
            arrmap.Clear();
            arrowsPointingToSnitch.Clear();
        }

        [Local]
        void OnPlayerDie(PlayerDieEvent ev)
        {
            if (arrmap.TryGetValue(ev.Player.PlayerId, out var arrows))
            {
                foreach (var arrow in arrows) arrow.Release();
                arrmap.Remove(ev.Player.PlayerId);
            }
            if (arrowsPointingToSnitch.TryGetValue(ev.Player.PlayerId, out var arrowsToSnitch))
            {
                foreach (var arrow in arrowsToSnitch) arrow.Release();
                arrowsPointingToSnitch.Remove(ev.Player.PlayerId);
            }
        }
    }
}