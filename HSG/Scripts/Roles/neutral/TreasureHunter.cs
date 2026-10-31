namespace NebulaN.Roles.Neutral;

/// <summary>
/// 寻宝者（中立）。
/// 「请在飞船上找到你想要的宝物，找到之后即可获胜。你会看到地图里面有很多隆起的小土堆，
/// 挖开土堆即可寻找你想寻找的东西，找到即为获胜。」
///
/// 实现说明：原版地图没有可挖的土堆，这里把「土堆」落在各个通风口的位置上 ——
/// 那是地图上现成的、分布均匀且所有人都认得的坐标。只有寻宝者看得到哪些还没挖过。
/// </summary>
public class TreasureHunter : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly FloatConfiguration DigCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.treasureHunter.digCooldown", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration DigRadiusOption = NebulaAPI.Configurations.Configuration(
        "options.role.treasureHunter.digRadius", (0.5f, 3f, 0.25f), 1.25f, decorator: v => v + "m");

    private TreasureHunter() : base(
        "treasureHunter", HalfSugarGift.Core.Patch.ExTeam.TreasureHunterTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.TreasureHunterTeam,
        [DigCooldownOption, DigRadiusOption])
    { }

    public static readonly TreasureHunter MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>所有土堆的位置。</summary>
        private readonly List<Vector2> mounds = new();
        /// <summary>藏着宝物的那一个。</summary>
        private int treasureIndex = -1;
        private readonly HashSet<int> dug = new();
        private bool found = false;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var dig = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, DigCooldownOption, "treasureHunter.dig", null,
                b => !b.IsInCooldown && NearestMound() >= 0 && !found,
                b => !MyPlayer.IsDead && !found, false);
            dig.OnClick = button =>
            {
                int index = NearestMound();
                if (index < 0) return;
                dug.Add(index);

                if (index == treasureIndex)
                {
                    found = true;
                    new StaticAchievementToken("treasureHunter.found");
                    RpcFound.Invoke(MyPlayer.PlayerId);
                }
                else
                {
                    AmongUsUtil.PlayQuickFlash(new Virial.Color(0.5f, 0.4f, 0.3f));
                }
                button.StartCoolDown();
            };

            Helpers.TextHudContent("TreasureHunterText", this,
                tmPro => tmPro.text = found
                    ? Language.Translate("role.treasureHunter.found")
                    : Language.Translate("role.treasureHunter.progress")
                        .Replace("%DUG%", dug.Count.ToString())
                        .Replace("%TOTAL%", mounds.Count.ToString()),
                true);
        }

        void OnGameStart(GameStartEvent ev) => BuildMounds();

        private void BuildMounds()
        {
            mounds.Clear();
            dug.Clear();
            if (ShipStatus.Instance == null) return;
            foreach (var vent in ShipStatus.Instance.AllVents)
                mounds.Add(new Vector2(vent.transform.position.x, vent.transform.position.y));
            if (mounds.Count > 0) treasureIndex = UnityEngine.Random.Range(0, mounds.Count);
        }

        /// <summary>脚下最近的、还没挖过的土堆。</summary>
        private int NearestMound()
        {
            var pos = new Vector2(MyPlayer.TruePosition.x, MyPlayer.TruePosition.y);
            for (int i = 0; i < mounds.Count; i++)
            {
                if (dug.Contains(i)) continue;
                if (Vector2.Distance(pos, mounds[i]) <= DigRadiusOption) return i;
            }
            return -1;
        }

        public void MarkFound() => found = true;

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.TreasureHunterWin;

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
            => ev.SetWinIf(ev.GameEnd == HalfSugarGift.Core.Patch.ExTeam.TreasureHunterWin && found);

        // 成就要发给寻宝者本人，原来的 [OnlyHost] 会发到房主身上
        [Local, OnlyMyPlayer]
        void OnDie(PlayerDieEvent ev)
        {
            if (!found) new StaticAchievementToken("treasureHunter.lost");
        }
    }

    /// <summary>挖到宝物 → 立即结算。</summary>
    static internal readonly RemoteProcess<byte> RpcFound = new("HSG.TreasureHunter.Found", (id, _) =>
    {
        // 「已找到」同步到所有客户端，胜利判定才能在任何一端成立
        if (GamePlayer.GetPlayer(id)?.Role is Instance inst) inst.MarkFound();
        if (!AmongUsClient.Instance.AmHost) return;
        NebulaAPI.CurrentGame?.TriggerGameEnd(
            HalfSugarGift.Core.Patch.ExTeam.TreasureHunterWin,
            GameEndReason.Special, BitMasks.AsPlayer(1u << id));
    });
}
