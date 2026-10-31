using Virial.Helpers;   // AsBoolFast 扩展方法所在的命名空间
namespace NebulaN.Roles.Crewmate;

/// <summary>
/// 豆腐狂热者（船员）。
///
/// 表格描述：「豆腐狂热者可以食用狐妖的豆腐，食用后可在一定时间内获得狐妖的技能，
/// 豆腐狂热者也拥有狐妖的饥饿值，豆腐狂热者的饥饿值是为了显示他可以使用狐妖技能的时间，
/// 饥饿值清零并不会死亡。」
///
/// 这里的「狐妖」就是主程序集的妖狐 <see cref="Spectre"/>：
/// · 豆腐 = <see cref="Spectre.Dish"/>（地图上的薯条盘，由 <see cref="Spectre.FriesDishManager"/> 生成）；
/// · 饥饿值 = 妖狐的 Satiety，一盘豆腐 = 1 点饥饿值 = Spectre.SatietyRateOption 秒；
/// · 狐妖的技能 = 消失（Vanish），消耗饥饿值换一段隐身。
///
/// 与妖狐的两点关键区别：
/// 1. 饥饿值归零<b>不会饿死</b>，只是用不了消失；
/// 2. 豆腐是抢来的 —— 狂热者吃掉一盘，妖狐就少一盘。
///
/// ⚠️ 依赖：房间里<b>必须开启妖狐</b>，否则地图上根本不会生成豆腐
/// （见 FriesDishManager.OnGameStarted 里的 IsAvailable 判定）。
/// 所以没开妖狐时本职业的设置页会显示为灰色。
/// </summary>
public class TofuLover : DefinedRoleTemplate, HasCitation, DefinedRole, IAssignableDocument
{
    /// <summary>开局自带的饥饿值（单位：盘豆腐）。</summary>
    static private readonly FloatConfiguration InitialSatietyOption = NebulaAPI.Configurations.Configuration(
        "options.role.tofuLover.initialSatiety", (0f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration MaxSatietyOption = NebulaAPI.Configurations.Configuration(
        "options.role.tofuLover.maxSatiety", (1f, 8f, 0.25f), 4f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration VanishCostOption = NebulaAPI.Configurations.Configuration(
        "options.role.tofuLover.vanishCost", (0f, 2f, 0.25f), 0.5f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration VanishCooldownOption = NebulaAPI.Configurations.Configuration(
        "options.role.tofuLover.vanishCooldown", (0f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration VanishDurationOption = NebulaAPI.Configurations.Configuration(
        "options.role.tofuLover.vanishDuration", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);

    private TofuLover() : base(
        "tofuLover", new Virial.Color(0.95f, 0.95f, 0.85f), RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        [InitialSatietyOption, MaxSatietyOption, VanishCostOption, VanishCooldownOption, VanishDurationOption])
    {
        // 没开妖狐就没有豆腐可吃，把设置页灰掉，省得房主纳闷这职业为什么没反应
        ConfigurationHolder?.SetDisplayState(() =>
            (Spectre.MyRole as ISpawnable).IsSpawnable
                ? ConfigurationHolderState.Activated
                : ConfigurationHolderState.Inactivated);
        ConfigurationHolder?.ScheduleAddRelated(() => [Spectre.MyRole.ConfigurationHolder!]);
    }

    public static readonly TofuLover MyRole = new();
    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(Spectre.buttonSprite, "role.tofuLover.ability.vanish");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        // 文档里直接把「一盘豆腐值多少秒」算出来，免得玩家自己换算
        yield return new AssignableDocumentReplacement("%SEC%", SatietyRate.ToString("F1"));
    }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    /// <summary>一盘豆腐折合多少秒 —— 与妖狐共用同一个换算率，保证血条刻度对得上。</summary>
    static internal float SatietyRate => Spectre.SatietyRateOption;

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        /// <summary>剩余可用时间（秒）。除以 <see cref="SatietyRate"/> 就是饥饿值。</summary>
        private float leftTime;
        public float Satiety => leftTime / SatietyRate;

        /// <summary>已经放行过的豆腐盘，避免重复包装 CanUse。</summary>
        private readonly HashSet<int> patchedDishes = new();

        int[] RuntimeAssignable.RoleArguments => [(int)leftTime];

        void RuntimeAssignable.OnActivated()
        {
            leftTime = InitialSatietyOption * SatietyRate;
            if (!AmOwner) return;

            // 借用妖狐那条薯条血条，刻度与妖狐完全一致
            new Spectre.FriesGuageViewer(MyPlayer, new FunctionalGetter<float>(() => leftTime)).Register(this);

            var vanishButton = NebulaAPI.Modules.EffectButton(
                this, MyPlayer, VirtualKeyInput.Ability,
                VanishCooldownOption, VanishDurationOption, "vanish", Spectre.buttonSprite,
                _ => Satiety > VanishCostOption);
            vanishButton.SetLabelType(ModAbilityButton.LabelType.Crewmate);
            vanishButton.OnEffectStart = button =>
            {
                AddSatiety(-VanishCostOption);
                MyPlayer.GainAttribute(PlayerAttributes.Invisible, VanishDurationOption, false, 100, "hsg::tofuLoverVanish");
            };
            vanishButton.OnEffectEnd = button => button.StartCoolDown();
        }

        public void AddSatiety(float satiety)
            => leftTime = Mathf.Clamp(leftTime + satiety * SatietyRate, 0f, MaxSatietyOption * SatietyRate);

        /// <summary>
        /// 吃完一盘豆腐。妖狐的小游戏在结算时只认妖狐与背德者两个职业，
        /// 但它无论谁吃都会广播 <see cref="Spectre.EatFriesEvent"/>，所以从这里补记饥饿值。
        /// </summary>
        [OnlyMyPlayer]
        void OnEatFries(Spectre.EatFriesEvent ev)
        {
            AddSatiety(1f);
            // 事件在所有客户端触发，成就只能发给狂热者本人
            if (AmOwner) new StaticAchievementToken("tofuLover.eat");
        }

        /// <summary>
        /// 原版 Dish 的 CanUse 写死了只有妖狐与背德者能开，这里在<b>本地</b>把判定包一层 ——
        /// 只影响自己客户端的可交互判定，不改别人的。
        /// 豆腐是开局由房主生成后同步过来的，所以要持续检查有没有新盘子冒出来。
        /// </summary>
        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!AmOwner) return;

            OpenDishesForMe();

            // 饥饿值持续消耗，但会议与放逐动画期间不走表（与妖狐一致）
            if (!MeetingHud.Instance.AsBoolFast() && !ExileController.Instance.AsBoolFast())
                leftTime = Mathf.Max(0f, leftTime - ev.DeltaTime);
            // 归零只是用不了消失，不会饿死 —— 这正是与妖狐的核心区别
        }

        private void OpenDishesForMe()
        {
            var manager = ModSingleton<Spectre.FriesDishManager>.Instance;
            if (manager == null) return;

            foreach (var dish in manager.AllDishes)
            {
                if (dish.DishId < 0 || !patchedDishes.Add(dish.DishId)) continue;

                var property = dish.Console?.Property;
                if (property == null) continue;

                var original = property.CanUse;
                property.CanUse = pos =>
                {
                    if (dish.Ate) return false;
                    var me = GamePlayer.LocalPlayer;
                    if (me != null && !me.IsDead && me.Role is Instance) return true;
                    return original?.Invoke(pos) ?? false;
                };
            }
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (!AmOwner) return;
            if (ev.EndState.Winners.Test(MyPlayer))
                new StaticAchievementToken("tofuLover.win");
        }
    }
}
