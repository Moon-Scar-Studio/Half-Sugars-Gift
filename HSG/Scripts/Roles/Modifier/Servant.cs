namespace NebulaN.Roles.Modifier;

/// <summary>
/// 仆从（附加职业，由农场主招募）。
///
/// 表格里这个附加职业的原名带有明显的冒犯性表述，这里在保留全部机制的前提下改称「仆从」。
/// 机制：被农场主招募后替农场主完成任务；农场主死亡则解除本附加职业，恢复原职业与原胜利条件。
/// </summary>
public class Servant : DefinedModifierTemplate, HasCitation, DefinedModifier,
    RuntimeAssignableGenerator<RuntimeModifier>
{
    private Servant() : base("servant", new Virial.Color(0.65f, 0.5f, 0.3f), withConfigurationHolder: false) { }

    public static readonly Servant MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player, arguments.Get(0, 255));

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly byte masterId;
        public GamePlayer? Master => GamePlayer.GetPlayer(masterId);

        public Instance(GamePlayer player, int masterId) : base(player) { this.masterId = (byte)masterId; }

        int[] RuntimeAssignable.RoleArguments => [masterId];
        void RuntimeAssignable.OnActivated() { }

        [Local]
        void OnTaskComplete(PlayerTaskCompleteLocalEvent ev)
        {
            if (!AmOwner) return;
            RpcServantTaskDone.Invoke((masterId, MyPlayer.PlayerId));
        }

        /// <summary>农场主死了 → 恢复自由。</summary>
        void OnPlayerDie(PlayerDieEvent ev)
        {
            if (ev.Player.PlayerId != masterId) return;
            if (!AmOwner) return;
            new StaticAchievementToken("servant.free");
            MyPlayer.RemoveModifier(MyRole);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            var local = GamePlayer.LocalPlayer;
            if (AmOwner || canSeeAllInfo || inEndScene || (local != null && local.PlayerId == masterId))
                name += " ⚒".Color(new Color(0.65f, 0.5f, 0.3f));
        }
    }

    /// <summary>通知农场主：你的仆从完成了一个任务。</summary>
    static internal readonly RemoteProcess<(byte masterId, byte servantId)> RpcServantTaskDone =
        new("HSG.Servant.TaskDone", (msg, _) =>
        {
            var master = GamePlayer.GetPlayer(msg.masterId);
            if (master?.Role is NebulaN.Roles.Crewmate.Plantation.Instance farm) farm.OnServantTaskDone();
        });
}
