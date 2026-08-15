using HalfSugarGift.Roles.Modifier;
using Nebula.Extensions;
using HSGTeam = HalfSugarGift.Core.Patch.Team;
using static HalfSugarGift.Core.Patch.Cor;

namespace HalfSugarGift.Roles.Neutral;
public class Taoist : DefinedRoleTemplate, DefinedRole, HasCitation,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    static FloatConfiguration AmuletCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.taoist.amuletCooldown", (5f, 60f, 1f), 20f, FloatConfigurationDecorator.Second
    );

    private Taoist() : base(
        "taoist",
        new Virial.Color(0.8f, 0.7f, 0.2f),
        RoleCategory.NeutralRole,
        HSGTeam.TaoistTeam,
        [AmuletCooldown]
    ) 
    {
        ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/TaoistPic.png")?.AsImage(115f);
    }

    public static readonly Taoist MyRole = new();
    public Citation Citation => Citations.Hellos497;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    Image? DefinedAssignable.IconImage =>
        NebulaAPI.AddonAsset.GetResource("Smallicon/TaoistIcon.png")?.AsImage();

    public static readonly TranslatableTag SacrificedState = new TranslatableTag("state.taoistSacrifice");
    public static readonly TranslatableTag AmuletTriggeredState = new TranslatableTag("state.amuletTriggered");

    internal static readonly Virial.Media.Image? AmuletDocIcon =
        NebulaAPI.AddonAsset.GetResource("ExecuteButton.png")?.AsImage(115f);

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(AmuletDocIcon, "role.taoist.doc.amulet");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield break;
    }

    internal static readonly RemoteProcess<(byte taoistId, byte killerId)> RpcTaoistSacrifice = new(
        "Taoist.Sacrifice",
        (data, _) =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            var taoist = GamePlayer.GetPlayer(data.taoistId);
            var killer = GamePlayer.GetPlayer(data.killerId);
            if (taoist == null || taoist.IsDead) return;
            if (killer == null || killer.IsDead) return;

            taoist.VanillaPlayer.NetTransform.RpcSnapTo(killer.Position);

            NebulaManager.Instance.StartDelayAction(0.3f, () =>
            {
                if (!taoist.IsDead)
                    taoist.Suicide(SacrificedState, EventDetail.Kill, KillParameter.NormalKill);
                if (!killer.IsDead)
                    killer.Suicide(AmuletTriggeredState, EventDetail.Kill, KillParameter.NormalKill);
            });
        }
    );

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable,
        IGameOperator
    {
        DefinedRole RuntimeRole.Role => MyRole;
        bool RuntimeRole.HasVanillaKillButton => false;

        private static readonly Virial.Media.Image? AmuletButtonIcon =
            NebulaAPI.AddonAsset.GetResource("ExecuteButton.png")?.AsImage(100f);

        private bool _used;
        private byte _amuletTargetId = byte.MaxValue; 

        private ModAbilityButton? _amuletButton;

        public Instance(GamePlayer player) : base(player) { }

        //AI太好用了你们知道吗
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
        //AI不好用你们知道吗
        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            _used = false;
            _amuletTargetId = byte.MaxValue;

            var tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);

            _amuletButton = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer,
                VirtualKeyInput.Ability,
                AmuletCooldown,
                "taoist.amulet",
                AmuletButtonIcon,
                _ => !_used && MyPlayer.CanMove
                    && tracker.CurrentTarget != null && tracker.CurrentTarget != MyPlayer
                    && !tracker.CurrentTarget.IsDead,
                _ => !MyPlayer.IsDead && !_used
            );
            _amuletButton.ShowUsesIcon(1, "1");
            _amuletButton.OnClick = _ =>
            {
                var target = tracker.CurrentTarget;
                if (_used || target == null || target == MyPlayer || target.IsDead) return;
                target.AddModifier(Amulet.MyRole);
                _amuletTargetId = target.PlayerId;
                _used = true;
                _amuletButton.UpdateUsesIcon("0");
            };
        }

        [Local]
        void OnMyDeath(PlayerDieEvent ev)
        {
            if (ev.Player != MyPlayer) return;
            if (_amuletTargetId == byte.MaxValue) return;
            var target = GamePlayer.GetPlayer(_amuletTargetId);
            if (target != null && !target.IsDead)
                target.RemoveModifier(Amulet.MyRole);
        }

        [OnlyMyPlayer]
        void CheckExtraWins(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.OpportunistPhase) return;
            if (_used && _amuletTargetId != byte.MaxValue)
            {
                var target = GamePlayer.GetPlayer(_amuletTargetId);
                if (target != null && target.IsAlive)
                {
                    ev.ExtraWinMask.Add(HSGTeam.ExtraTaoistWin);
                    ev.IsExtraWin = true;
                }
            }
        }

        void IGameOperator.OnReleased() { }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene) { }
    }
}
