using HalfSugarGift.Roles.Modifier;
using Nebula.Extensions;
using HSGTeam = HalfSugarGift.Core.Patch.Team;
using static HalfSugarGift.Core.Patch.Cor;

namespace HalfSugarGift.Roles.Impostor;

/// <summary>
/// 魔法师，内鬼阵营。
/// 第一人格给目标施加脆弱，全死了切换第二人格， 还原范围攻击，回第一人格
/// 不具备普通击杀能力。
/// </summary>

public class Mage : DefinedRoleTemplate, DefinedRole, HasCitation,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    // 切换第二人格所需击杀人数
    static IntegerConfiguration RequiredKillCount = NebulaAPI.Configurations.Configuration(
        "options.role.mage.requiredKillCount", (1, 5, 1), 2
    );
    // 脆弱技能冷却
    static FloatConfiguration WeakCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.mage.weakCooldown", (5f, 60f, 1f), 20f, FloatConfigurationDecorator.Second
    );
    // 还原技能范围
    static FloatConfiguration RestoreRadius = NebulaAPI.Configurations.Configuration(
        "options.role.mage.restoreRadius", (0.5f, 3f, 0.1f), 1.5f,
        decorator: val => val + "x"
    );
    // 还原技能冷却
    static FloatConfiguration RestoreCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.mage.restoreCooldown", (5f, 60f, 1f), 20f, FloatConfigurationDecorator.Second
    );
    // 脆弱到期清除配置
    internal static BoolConfiguration WeaknessEnableRoundExpiry = NebulaAPI.Configurations.Configuration(
        "options.role.mage.weaknessEnableRoundExpiry", false
    );
    internal static IntegerConfiguration WeaknessExpiryRounds = NebulaAPI.Configurations.Configuration(
        "options.role.mage.weaknessExpiryRounds", (1, 10, 1), 2,
        predicate: () => WeaknessEnableRoundExpiry
    );

    private Mage() : base(
        "mage",
        Cor.impRed,
        RoleCategory.ImpostorRole,
        NebulaTeams.ImpostorTeam,
        [RequiredKillCount, WeakCooldown, RestoreRadius, RestoreCooldown,
         WeaknessEnableRoundExpiry, WeaknessExpiryRounds]
    )
    {
        ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset
            .GetResource("BigPic/RestorerPic.png")?.AsImage(115f);
    }

    public static readonly Mage MyRole = new();
    public Citation Citation => Citations.hvtXsvc_hsg;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    Virial.Media.Image? DefinedAssignable.IconImage =>
        NebulaAPI.AddonAsset.GetResource("Smallicon/MageIcon.png")?.AsImage();

    // 文档用技能图标
    internal static readonly Virial.Media.Image? WeakIcon =
        NebulaAPI.AddonAsset.GetResource("Weak.png")?.AsImage(115f);
    internal static readonly Virial.Media.Image? RestoreIcon =
        NebulaAPI.AddonAsset.GetResource("RestorerButton.png")?.AsImage(115f);

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
        {
            yield return new(WeakIcon, "role.mage.doc.weak");
            yield return new(RestoreIcon, "role.mage.doc.restore");
        }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield break;
    }

    // 死因标签、脆弱名字灰色
    public static readonly TranslatableTag AshedState = new TranslatableTag("state.ashed");
    public static readonly TranslatableTag WeaknessState = new TranslatableTag("state.weakness");
    private static readonly Virial.Color WeakGray = new(0.35f, 0.35f, 0.35f);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable,
        IGameOperator, IPlayerAbility
    {
        DefinedRole RuntimeRole.Role => MyRole;
        bool RuntimeRole.HasVanillaKillButton => false;
        bool IPlayerAbility.HideKillButton => true;
        // 技能按钮用图标（不同尺寸）
        private static readonly Virial.Media.Image? WeakButtonIcon =
            NebulaAPI.AddonAsset.GetResource("Weak.png")?.AsImage(100f);
        private static readonly Virial.Media.Image? RestoreButtonIcon =
                NebulaAPI.AddonAsset.GetResource("RestorerButton.png")?.AsImage(100f);

        //  状态 
        private bool _switched;                    // 是否处于第二人格
        private int _weakUsesLeft;                 // 当前轮剩余脆弱使用次数
        private readonly List<byte> _weakenedPlayerIds = new(); // 被施法的玩家ID

        private const string CamoTag = "MageCamo";
        private float _camoCheckTimer;

        private ModAbilityButton? _weakButton;
        private ModAbilityButton? _restoreButton;

        // === 通过反射获取 UnknownOutfit（隐蔽者同款伪装外观） ===
        private static OutfitDefinition? GetUnknownOutfit()
        {
            var game = NebulaAPI.CurrentGame;
            if (game == null) return null;
            var prop = game.GetType().GetProperty("UnknownOutfit",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return prop?.GetValue(game) as OutfitDefinition;

        }

        public Instance(GamePlayer player) : base(player) { }

        // 显示带淡入+保持+淡出效果的标题
        private static void SetTextWithFade(TitleShower? shower, string text, Virial.Color color, float holdDuration = 1f, bool shake = true)
        {
            if (shower == null) return;

            const float fadeInDuration = 0.3f;
            float fadeInTimer = fadeInDuration;
            float holdTimer = holdDuration;
            float fadeOutAlpha = 1f;
            float shakeTimer = 0f;

            shower.SetText(text, color, new TitleTrait(s =>
            {
                var dt = Time.deltaTime;

                // 抖动效果
                if (shake)
                {
                    shakeTimer -= dt;
                    if (shakeTimer < 0f)
                    {
                        shakeTimer = 0.08f;
                        s.Transform.localPosition = new(
                            ((float)System.Random.Shared.NextDouble() - 0.5f) * 0.06f,
                            ((float)System.Random.Shared.NextDouble() - 0.5f) * 0.06f);
                    }
                }

                // 透明度动画：淡入 → 保持 → 淡出
                if (fadeInTimer > 0f)
                {
                    fadeInTimer -= dt;
                    s.SetTextAlpha(Mathn.Clamp01(1f - fadeInTimer / fadeInDuration));
                }
                else if (holdTimer > 0f)
                {
                    holdTimer -= dt;
                    s.SetTextAlpha(1f);
                }
                else
                {
                    fadeOutAlpha -= dt * 0.5f;
                    s.SetTextAlpha(Mathn.Clamp01(fadeOutAlpha));
                }
            }));
        }

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            _switched = false;
            _weakUsesLeft = RequiredKillCount;
            _weakenedPlayerIds.Clear();

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
            tracker.SetColor(MyRole.RoleColor);

            // === 脆弱诅咒按钮（第一人格） ===
            _weakButton = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer,
                VirtualKeyInput.Ability,
                WeakCooldown,
                "mage.weak",
                WeakButtonIcon,
                _ => _weakUsesLeft > 0 && MyPlayer.CanMove
                    && tracker.CurrentTarget != null && tracker.CurrentTarget != MyPlayer
                    && !tracker.CurrentTarget.IsDead
                    && !_weakenedPlayerIds.Contains(tracker.CurrentTarget.PlayerId),
                _ => !MyPlayer.IsDead && !_switched
            );
            _weakButton.ShowUsesIcon(4, _weakUsesLeft.ToString());
            _weakButton.OnClick = _ =>
            {
                var target = tracker.CurrentTarget;
                if (target == null || target == MyPlayer || _switched || _weakUsesLeft <= 0) return;
                if (_weakenedPlayerIds.Contains(target.PlayerId)) return;
                target.AddModifier(Weakness.MyRole);
                _weakenedPlayerIds.Add(target.PlayerId);
                _weakUsesLeft--;
                _weakButton.UpdateUsesIcon(_weakUsesLeft.ToString());
                _weakButton.StartCoolDown();
            };

            // === 还原按钮（第二人格） ===
            _restoreButton = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer,
                VirtualKeyInput.SidekickAction,
                RestoreCooldown,
                "mage.restore",
                RestoreButtonIcon,
                _ => MyPlayer.CanMove,
                _ => !MyPlayer.IsDead && _switched
            );
            _restoreButton.OnClick = _ =>
            {
                if (!_switched || MyPlayer.IsDead) return;
                RpcRestoreKill.Invoke(MyPlayer.PlayerId);
                // 回归第一人格
                SwitchBackToFirst();
            };
        }

        // === 回归第一人格 ===
        private void SwitchBackToFirst()
        {
            _switched = false;
            // 移除所有玩家的伪装
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
                p.RemoveOutfitByTag(CamoTag);
            _weakUsesLeft = RequiredKillCount;
            _weakenedPlayerIds.Clear();
            if (_weakButton != null)
                _weakButton.UpdateUsesIcon(_weakUsesLeft.ToString());
            SetTextWithFade(NebulaAPI.CurrentGame?.GetModule<TitleShower>(),
                Language.Translate("role.mage.backToFirst"), Cor.impRed);
        }

        // === 还原 RPC（仅房主执行） ===
        private static readonly RemoteProcess<byte> RpcRestoreKill = new(
            "Mage.RestoreKill",
            (mageId, _) =>
            {
                if (!AmongUsClient.Instance.AmHost) return;
                var mage = GamePlayer.GetPlayer(mageId);
                if (mage == null || mage.IsDead) return;
                float radius = RestoreRadius;
                foreach (var p in GamePlayer.AllPlayerlikes)
                {
                    if (p.IsDead || p == mage) continue;
                    if (Vector2.Distance(p.Position, mage.Position) > radius) continue;
                    mage.MurderPlayer(p, AshedState, EventDetail.Kill,
                        KillParameter.WithOverlay | KillParameter.WithAssigningGhostRole,
                        KillCondition.BothAlive);
                }
            }
        );

        // === 监听玩家死亡 → 检查是否所有脆弱目标已死亡 ===
        [Local]
        void OnPlayerDie(PlayerDieEvent ev)
        {
            if (MyPlayer.IsDead) return;

            // 通知法师
            if (_weakenedPlayerIds.Contains(ev.Player.PlayerId))
            {
                // 临时使用 TitleShower 显示提示
                SetTextWithFade(NebulaAPI.CurrentGame?.GetModule<TitleShower>(),
                    Language.Translate("role.mage.weaknessTriggered")
                        .Replace("%PLAYER%", ev.Player.Name),
                    Cor.impRed);
            }

            // 第二人格不受影响
            if (_switched) return;

            // 移除已死亡的脆弱目标
            _weakenedPlayerIds.Remove(ev.Player.PlayerId);

            // 所有脆弱目标死亡且已无剩余次数 → 切换第二人格
            if (_weakenedPlayerIds.Count == 0 && _weakUsesLeft == 0)
            {
                _switched = true;
                // 对其他所有存活玩家应用伪装外观（灰色无装饰无名字）
                var unknown = GetUnknownOutfit();
                if (unknown != null)
                {
                    foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
                    {
                        if (!p.IsDead && p != MyPlayer)
                            p.AddOutfit(new OutfitCandidate(unknown, CamoTag, OutfitPriority.Camouflage, true));
                    }
                }
                // 临时使用 TitleShower 显示提示
                SetTextWithFade(NebulaAPI.CurrentGame?.GetModule<TitleShower>(),
                    Language.Translate("role.mage.switched"), Cor.impRed);
            }
        }

        // === 名字装饰 ===
        // 第一人格：灰色标记脆弱目标（仅法师可见）
        [Local]
        void DecorateWeaknessName(PlayerDecorateNameEvent ev)
        {
            if (MyPlayer.IsDead || _switched) return;
            if (_weakenedPlayerIds.Contains(ev.Player.PlayerId) && !ev.Player.IsDead)
                ev.Color = WeakGray;
        }

        // === 第二人格视觉特效 ===
        void OnUpdateCamera(CameraUpdateEvent ev)
        {
            if (!AmOwner) return;
            if (_switched && !MyPlayer.IsDead)
            {
                ev.UpdateSaturation(0f, true);   // 黑白去色
                ev.UpdateBrightness(1.15f, true); // 稍提亮（灰白感）
            }
        }

        // === 第二人格期间每 2 秒检测一次：确保所有玩家（除魔法使自己）都处于伪装状态 ===
        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!_switched || MyPlayer.IsDead) return;
            _camoCheckTimer += Time.deltaTime;
            if (_camoCheckTimer < 2f) return;
            _camoCheckTimer = 0f;

            var unknown = GetUnknownOutfit();
            if (unknown == null) return;
            foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
            {
                if (p.IsDead || p == MyPlayer) continue;
                p.AddOutfit(new OutfitCandidate(unknown, CamoTag, OutfitPriority.Camouflage, true));
            }
        }

        // === 第二人格期间有新玩家复活 → 立刻应用伪装 ===
        [Local]
        void OnPlayerRevive(PlayerReviveEvent ev)
        {
            if (!_switched || MyPlayer.IsDead || ev.Player == MyPlayer) return;
            var unknown = GetUnknownOutfit();
            if (unknown != null)
                ev.Player.AddOutfit(new OutfitCandidate(unknown, CamoTag, OutfitPriority.Camouflage, true));
        }

        void IGameOperator.OnReleased()
        {
            if (_switched)
            {
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
                    p.RemoveOutfitByTag(CamoTag);
            }
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene) { }
    }
}
