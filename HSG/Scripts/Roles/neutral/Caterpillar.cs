namespace NebulaN.Roles.Neutral;

/// <summary>
/// 猫猫虫（中立）。
/// 「在使用技能时，全部人都会变成猫猫虫，可以趁机把别人吞下去」。
/// 变身期间全场同外观（伪装），猫猫虫可以吞人 —— 吞掉的人不留尸体。
/// 吞够指定人数即独立获胜。
/// </summary>
public class Caterpillar : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    static private readonly IntegerConfiguration EatToWinOption = NebulaAPI.Configurations.Configuration(
        "options.role.caterpillar.eatToWin", (1, 8), 3);
    static private readonly FloatConfiguration DurationOption = NebulaAPI.Configurations.Configuration(
        "options.role.caterpillar.duration", (5f, 40f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration CooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.caterpillar.cooldown", (15f, 90f, 2.5f), 35f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration EatCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.caterpillar.eatCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);

    private Caterpillar() : base(
        "caterpillar", HalfSugarGift.Core.Patch.ExTeam.CaterpillarTeam.Color, RoleCategory.NeutralRole,
        HalfSugarGift.Core.Patch.ExTeam.CaterpillarTeam,
        [EatToWinOption, DurationOption, CooldownOption, EatCooldownOption])
    { }

    public static readonly Caterpillar MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool DefinedRole.IsKiller => true;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages() { yield break; }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>全场变猫猫虫：所有人换成猫猫虫本人的外观。</summary>
    static internal readonly RemoteProcess<(byte caterpillarId, float duration)> RpcMorphAll =
        new("HSG.Caterpillar.MorphAll", (msg, _) =>
        {
            var source = GamePlayer.GetPlayer(msg.caterpillarId);
            var local = GamePlayer.LocalPlayer;
            if (source == null || local == null) return;
            local.AddOutfit(new OutfitCandidate(source.DefaultOutfit, "caterpillar", OutfitPriority.Camouflage, true));
            NebulaManager.Instance.StartDelayAction(msg.duration, () => local.RemoveOutfitByTag("caterpillar"));
        });

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int eaten = 0;
        private float morphLeft = 0f;

        bool RuntimeRole.HasVanillaKillButton => false;
        bool RuntimeRole.CanUseVent => true;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            var morph = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, CooldownOption, "caterpillar.morph", null,
                b => !b.IsInCooldown && morphLeft <= 0f,
                b => !MyPlayer.IsDead, false);
            morph.OnClick = button =>
            {
                morphLeft = DurationOption;
                RpcMorphAll.Invoke((MyPlayer.PlayerId, DurationOption));
                button.StartCoolDown();
            };

            var tracker = NebulaAPI.Modules.KillTracker(this, MyPlayer,
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId);
            var eat = NebulaAPI.Modules.KillButton(
                this, MyPlayer, true, VirtualKeyInput.Kill, null, EatCooldownOption,
                "caterpillar.eat", ModAbilityButton.LabelType.Impostor, null,
                (target, button) =>
                {
                    // 吞人不留尸体。原来是击杀后立刻删尸体，但击杀要等房主处理，那时尸体还没生成，删不到。
                    MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Eat, KillParameter.NormalKill & ~KillParameter.WithDeadBody);
                    new StaticAchievementToken("caterpillar.eat");
                    button.StartCoolDown();
                },
                p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId,
                b => morphLeft > 0f,
                b => !MyPlayer.IsDead);
            NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(eat.GetKillButtonLike());
        }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner || morphLeft <= 0f) return;
            morphLeft -= ev.DeltaTime;
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev) => morphLeft = 0f;

        [OnlyMyPlayer]
        void OnCheckWin(PlayerCheckWinEvent ev)
        {
            if (ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.CaterpillarWin) return;
            ev.SetWinIf(!MyPlayer.IsDead && eaten >= EatToWinOption);
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
            => ev.IsBlocked |= ev.GameEnd != HalfSugarGift.Core.Patch.ExTeam.CaterpillarWin;

        [OnlyHost]
        void OnEndCriteriaMet(EndCriteriaMetEvent ev)
        {
            if (MyPlayer.IsDead || eaten < EatToWinOption) return;
            var winners = BitMasks.AsPlayer();
            winners.Add(MyPlayer);
            ev.TryOverwriteEnd(HalfSugarGift.Core.Patch.ExTeam.CaterpillarWin, 60,
                GameEndReason.Special, (int)winners.AsRawPattern);
        }

        // 吞食计数在所有客户端同步（原来只在本人客户端累加，房主判胜时读到的永远是 0）
        void OnKill(PlayerKillPlayerEvent ev)
        {
            if (ev.Murderer != MyPlayer || ev.Dead == MyPlayer) return;
            eaten++;
            if (!AmongUsClient.Instance.AmHost) return;
            if (eaten >= EatToWinOption)
                NebulaAPI.CurrentGame?.TriggerGameEnd(
                    HalfSugarGift.Core.Patch.ExTeam.CaterpillarWin,
                    GameEndReason.Special, BitMasks.AsPlayer(1u << MyPlayer.PlayerId));
        }
    }
}
