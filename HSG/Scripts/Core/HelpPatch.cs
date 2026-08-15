/*
 * 帮助界面星标标签页 Patch
 * 在帮助界面中添加"星标"标签页，显示已星标的角色列表
 */

using Nebula.Modules.GUIWidget;
using Nebula.Modules.MetaWidget;
using static Nebula.Modules.HelpScreen;

namespace HalfSugarGift.Core.Patch;

/// <summary>
/// 帮助界面星标标签页扩展
/// </summary>
[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public static class HelpPatch
{
    // 星标标签页的翻译键
    private const string StarsTabTranslateKey = "help.tabs.stars";

    // 标签按钮文本属性
    private static readonly TextAttributeOld TabButtonAttr = new(TextAttributeOld.BoldAttr)
    {
        Size = new Vector2(0.82f, 0.21f),
        FontSize = 1.6f,
        FontMaxSize = 1.6f
    };

    // 角色标题文本属性
    private static readonly TextAttributeOld RoleTitleAttr = new(TextAttributeOld.BoldAttr)
    {
        Size = new Vector2(1.2f, 0.29f),
        FontMaterial = VanillaAsset.StandardMaskedFontMaterial
    };

    // 缓存的反射方法
    private static MethodInfo? _cachedOpenAssignableHelpMethod;

    /// <summary>
    /// 预处理方法，注册 Harmony Patch
    /// </summary>
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        // 注册翻译
        Language.Register(StarsTabTranslateKey, () => "星标");
        Language.Register("help.stars.empty", () => "暂无星标，点击职业详情页的星标按钮添加");

        // 缓存反射方法
        _cachedOpenAssignableHelpMethod = typeof(HelpScreen).GetMethod(
            "OpenAssignableHelp",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        // 手动注册 Harmony Patch
        var harmony = new Harmony("HSG.HelpPatch");

        // Patch GetTabsWidget 方法
        var getTabsWidgetMethod = typeof(HelpScreen).GetMethod(
            "GetTabsWidget",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        if (getTabsWidgetMethod != null)
        {
            var postfix = new HarmonyMethod(typeof(HelpPatch).GetMethod(nameof(GetTabsWidgetPostfix)));
            harmony.Patch(getTabsWidgetMethod, postfix: postfix);
            HsgDebug.Log("[HelpPatch] 已 Patch GetTabsWidget 方法");
        }

        HsgDebug.Log("[HelpPatch] 星标标签页 Patch 加载完成");
    }

    /// <summary>
    /// GetTabsWidget Postfix：在标签列表末尾添加星标按钮
    /// </summary>
    public static void GetTabsWidgetPostfix(MetaScreen screen, HelpTab tab, HelpTab validTabs, ref IMetaWidgetOld __result)
    {
        try
        {
            List<IMetaParallelPlacableOld> tabs = new();

            // 添加原版标签页
            foreach (var info in AllHelpTabInfo)
            {
                if ((validTabs & info.Tab) != 0)
                {
                    tabs.Add(info.GetButton(screen, tab, validTabs));
                }
            }

            // 添加星标标签页按钮
            tabs.Add(new MetaWidgetOld.Button(() =>
            {
                ShowStarsScreenCustom(screen);
            }, TabButtonAttr)
            {
                TranslationKey = StarsTabTranslateKey,
                Color = Virial.Color.Gray,  // 默认不高亮
                Alignment = IMetaWidgetOld.AlignmentOption.Center
            });

            // 重新组合组件
            __result = new CombinedWidgetOld(0.5f, tabs.ToArray());
        }
        catch (Exception ex)
        {
            HsgDebug.LogError($"[HelpPatch] GetTabsWidgetPostfix 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示星标界面（自定义实现）
    /// </summary>
    private static void ShowStarsScreenCustom(MetaScreen screen)
    {
        // 创建主界面布局
        MetaWidgetOld widget = new();
        Image? backImage = null;

        // 添加标签栏（复用原版方法，使用 Search 作为默认选中）
        HelpTab validTabs = HelpTab.Search | HelpTab.Roles | HelpTab.Overview | HelpTab.Options | HelpTab.Achievements | HelpTab.Stamps;

        widget.Append(GetTabsWidgetWithStars(screen, HelpTab.Search, validTabs));
        widget.Append(new MetaWidgetOld.VerticalMargin(0.1f));

        // 添加星标内容
        widget.Append(ShowStarsScreenContent(out backImage));

        screen.SetWidget(widget);
        screen.SetBackImage(backImage, 0.2f);
    }

    /// <summary>
    /// 获取包含星标按钮的标签栏
    /// </summary>
    private static IMetaWidgetOld GetTabsWidgetWithStars(MetaScreen screen, HelpTab tab, HelpTab validTabs)
    {
        List<IMetaParallelPlacableOld> tabs = new();

        // 添加原版标签页
        foreach (var info in AllHelpTabInfo)
        {
            if ((validTabs & info.Tab) != 0)
            {
                tabs.Add(info.GetButton(screen, tab, validTabs));
            }
        }

        // 添加星标标签页按钮（高亮显示）
        tabs.Add(new MetaWidgetOld.Button(() =>
        {
            ShowStarsScreenCustom(screen);
        }, TabButtonAttr)
        {
            TranslationKey = StarsTabTranslateKey,
            Color = Virial.Color.White,  // 星标页激活时高亮
            Alignment = IMetaWidgetOld.AlignmentOption.Center
        });

        return new CombinedWidgetOld(0.5f, tabs.ToArray());
    }

    /// <summary>
    /// 显示星标界面内容
    /// </summary>
    private static IMetaWidgetOld ShowStarsScreenContent(out Image? backImage)
    {
        backImage = null;

        // 获取所有星标的可分配对象
        var starred = StarsManager.GetAllStarred().ToList();

        // 如果没有星标，显示空提示
        if (starred.Count == 0)
        {
            return new MetaWidgetOld.Text(new TextAttributeOld(TextAttributeOld.BoldAttr)
            {
                Size = new Vector2(5f, 0.5f),
                Alignment = TMPro.TextAlignmentOptions.Center,
                FontMaterial = VanillaAsset.StandardMaskedFontMaterial
            })
            {
                RawText = Language.Translate("help.stars.empty"),
                Alignment = IMetaWidgetOld.AlignmentOption.Center
            };
        }

        // 按阵营分组显示
        MetaWidgetOld inner = new();

        // 添加分组内容
        void AddCategory(string categoryKey, IEnumerable<DefinedAssignable> assignables, Virial.Color color)
        {
            var list = assignables.ToList();
            if (list.Count == 0) return;

            if (inner.Count > 0)
                inner.Append(new MetaWidgetOld.VerticalMargin(0.2f));

            // 添加分类标题
            inner.Append(new MetaWidgetOld.WrappedWidget(
                NebulaAPI.GUI.Text(
                    GUIAlignment.Left,
                    NebulaAPI.GUI.GetAttribute(AttributeAsset.DocumentTitle),
                    new ColorTextComponent(color, new TranslateTextComponent(categoryKey))
                )
            ));

            inner.Append(new MetaWidgetOld.VerticalMargin(0.1f));

            // 添加角色按钮
            inner.Append(list, CreateAssignableButton, 4, -1, 0, 0.6f);
        }

        // 按阵营分组：内鬼、中立、船员、幽灵角色、修改器
        AddCategory("role.category.impostor",
            starred.Where(r => r is DefinedRole role && role.Category == RoleCategory.ImpostorRole),
            Virial.Color.ImpostorColor);

        AddCategory("role.category.neutral",
            starred.Where(r => r is DefinedRole role && role.Category == RoleCategory.NeutralRole),
            new Virial.Color(1f, 0.7f, 0f));

        AddCategory("role.category.crewmate",
            starred.Where(r => r is DefinedRole role && role.Category == RoleCategory.CrewmateRole),
            Virial.Color.CrewmateColor);

        AddCategory("role.category.ghost",
            starred.Where(r => r is DefinedGhostRole),
            Virial.Color.Gray);

        AddCategory("role.category.modifier",
            starred.Where(r => r is DefinedModifier),
            Virial.Color.White);

        // 返回带滚动条的界面
        return new MetaWidgetOld.ScrollView(new Vector2(7.4f, 4.1f), inner)
        {
            Alignment = IMetaWidgetOld.AlignmentOption.Center
        };
    }

    /// <summary>
    /// 创建可分配对象按钮
    /// </summary>
    private static IMetaParallelPlacableOld CreateAssignableButton(DefinedAssignable assignable)
    {
        return new CombinedWidgetOld(
            new MetaWidgetOld.HorizonalMargin(0.12f),
            new MetaWidgetOld.Button(() =>
            {
                // 调用 HelpScreen.OpenAssignableHelp 打开详情页
                OpenAssignableHelpCustom(assignable);
            }, RoleTitleAttr)
            {
                RawText = assignable.DisplayColoredName,
                PostBuilder = (PassiveButton button, SpriteRenderer renderer, TMPro.TextMeshPro text) =>
                {
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    button.OnMouseOver.AddListener(() =>
                    {
                        NebulaManager.Instance.SetHelpWidget(button, GetAssignableOverlay(assignable));
                    });
                    button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
                    text.transform.localPosition += new Vector3(0.07f, 0f, 0f);
                    button.transform.localPosition -= new Vector3(0.15f, 0f, 0f);

                    // 添加角色图标
                    var roleIcon = assignable.GetRoleIcon()?.GetSprite();
                    if (roleIcon != null)
                    {
                        var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", button.transform, new Vector3(-0.73f, 0f, -0.01f));
                        icon.sprite = roleIcon;
                        icon.material = RoleIcon.GetRoleIconMaterial(assignable, 0.8f);
                        icon.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        icon.transform.localScale = new Vector3(0.275f, 0.275f, 1f);
                    }
                },
                Alignment = IMetaWidgetOld.AlignmentOption.Center,
                TextHorizonotalExtraMargin = 0.15f,
            });
    }

    /// <summary>
    /// 打开可分配对象的帮助文档
    /// </summary>
    private static void OpenAssignableHelpCustom(DefinedAssignable assignable)
    {
        try
        {
            if (_cachedOpenAssignableHelpMethod != null)
            {
                _cachedOpenAssignableHelpMethod.Invoke(null, new object[] { assignable });
            }
        }
        catch (Exception ex)
        {
            HsgDebug.LogError($"[HelpPatch] OpenAssignableHelpCustom 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取可分配对象的悬浮信息
    /// </summary>
    private static GUIWidget GetAssignableOverlay(DefinedAssignable assignable)
    {
        List<GUIWidget> widgets = new();

        widgets.Add(NebulaAPI.GUI.RawText(
            GUIAlignment.Left,
            NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayTitle),
            assignable.DisplayColoredName
        ));

        widgets.Add(NebulaAPI.GUI.RawText(
            GUIAlignment.Left,
            NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayContent),
            ""
        ));

        // 添加详情信息
        var detail = assignable.ConfigurationHolder?.Detail;
        if (detail != null)
        {
            widgets.Add(NebulaAPI.GUI.Text(
                GUIAlignment.Left,
                NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayContent),
                detail
            ));
        }

        // 添加引用信息
        if (assignable is HasCitation hc && hc.Citation != null)
        {
            var citation = hc.Citation;
            widgets.Add(NebulaAPI.GUI.Margin(new FuzzySize(null, 0.35f)));
            widgets.Add(NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Left,
                NebulaAPI.GUI.RawText(GUIAlignment.Bottom, NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayContent), "from"),
                NebulaAPI.GUI.HorizontalMargin(0.12f),
                citation.LogoImage != null
                    ? NebulaAPI.GUI.Image(GUIAlignment.Bottom, citation.LogoImage, new FuzzySize(1.5f, 0.37f))
                    : NebulaAPI.GUI.Text(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayTitle), citation.Name)
            ));
        }

        var holder = NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left, widgets);
        holder.BackImage = assignable.ConfigurationHolder?.Illustration;
        return holder;
    }
}