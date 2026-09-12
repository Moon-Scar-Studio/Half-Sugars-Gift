namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>
/// 道具躲猫猫的全屏演出层：躲藏阶段的黑幕、居中大号倒计时、阶段切换提示。
///
/// 【为什么不复用 PatchManager.ShowScreenOverlay】
/// 那个方法在非 pulse 分支里无条件写死 <c>c.a = 0.5f</c>，
/// 传进去的 alpha 会被丢掉 —— 所以旧版「抓捕者黑屏」实际只有半透明，地图照样看得见。
/// 而且它的存续时间是开局一次性写死的，房主中途调整倒计时（击空扣时、嘲讽缩时）之后就对不上了。
///
/// 这里改成完全由 <see cref="PropHuntState.Phase"/> 驱动：
/// 每帧检查该不该显示，该显示就建、不该显示就收。时间只有一个来源，
/// 因此「抓捕方的黑屏倒计时」与「躲藏方的躲藏倒计时」天然相等，
/// 躲藏倒计时归零的同一帧黑幕消失、抓捕者解冻、追捕倒计时接管。
///
/// 【渲染层次】
/// 黑幕是 HudManager.FullScreen 的克隆（这是原版用来做全屏闪烁的精灵，尺寸天然铺满屏幕）。
/// 文字用 VanillaAsset.StandardTextPrefab，HSG 的牛仔对决已经在用同一个 prefab。
/// 两者的前后关系不靠 z 值，而是显式写 sortingOrder —— 同一 sorting layer 内
/// 先比 order 再比 z，写死 order 才能保证文字一定压在黑幕上面。
///
/// 【失败处理】
/// 任何一步抛异常就永久降级（unavailable = true）并收干净，
/// 不会每帧刷异常把日志淹掉。倒计时本身在左下角的 HUD 文本里仍然有，不至于瞎打。
/// </summary>
public static class PropHuntOverlay
{
    #region 状态

    private static SpriteRenderer? blackout;
    private static TextMeshPro? bigText;
    private static TextMeshPro? subText;
    private static TextMeshPro? flashText;

    /// <summary>建造失败过一次就不再尝试。</summary>
    private static bool unavailable;

    private static float flashTimer;
    private static Color flashColor = Color.white;

    private static PropHuntPhase lastPhase = PropHuntPhase.None;

    #endregion

    #region 生命周期

    /// <summary>开局初始化。由 PropHuntRuntime.OnGameStart 调用。</summary>
    public static void Setup()
    {
        Teardown();
        unavailable = false;
        lastPhase = PropHuntPhase.None;
    }

    /// <summary>结束 / 中途退出时收尾。</summary>
    public static void Teardown()
    {
        DestroyObject(blackout?.gameObject);
        DestroyObject(bigText?.gameObject);
        DestroyObject(subText?.gameObject);
        DestroyObject(flashText?.gameObject);

        blackout = null;
        bigText = null;
        subText = null;
        flashText = null;

        flashTimer = 0f;
        lastPhase = PropHuntPhase.None;
    }

    private static void DestroyObject(GameObject? target)
    {
        try
        {
            if (target != null) UnityEngine.Object.Destroy(target);
        }
        catch { /* HUD 可能已经销毁 */ }
    }

    #endregion

    #region 对外：一次性提示

    /// <summary>在屏幕中央打一条短提示（阶段切换、技能反馈等）。</summary>
    public static void ShowFlash(string message, Color color, float duration = 2f)
    {
        if (unavailable) return;

        try
        {
            // 这里刻意不用 ??= ：Unity 的对象被销毁后是「假 null」，
            // ??= 走的是 is null 语义会漏判，而 == null 被 UnityEngine.Object 重载过，能认出来。
            // 尺寸留得下两行：主提示 + 一行小字操作说明。
            if (flashText == null) flashText = CreateText(2.9f, new Vector2(10f, 2.4f));
            if (flashText == null) return;

            flashText.text = message;
            flashColor = color;
            flashTimer = duration;
        }
        catch (Exception exception)
        {
            HsgDebug.LogWarning($"[HSG] 道具躲猫猫：提示文字创建失败：{exception.Message}");
        }
    }

    #endregion

    #region 每帧

    /// <summary>由 PropHuntRuntime.OnUpdate 调用。</summary>
    public static void Update()
    {
        if (unavailable) return;
        if (!PropHuntGameModeRegistration.IsPropHuntMode) return;
        if (!HudManager.InstanceExists) return;

        try
        {
            UpdateInner();
        }
        catch (Exception exception)
        {
            unavailable = true;
            HsgDebug.LogWarning($"[HSG] 道具躲猫猫：全屏演出层不可用，已降级为纯 HUD 文字：{exception.Message}");
            Teardown();
        }
    }

    private static void UpdateInner()
    {
        var local = GamePlayer.LocalPlayer;
        var phase = PropHuntState.Phase;

        // --- 阶段切换提示：躲藏结束的那一刻给双方一个明确信号 ---
        if (phase != lastPhase)
        {
            if (lastPhase == PropHuntPhase.Hiding && phase == PropHuntPhase.Hunting)
            {
                ShowFlash(
                    Language.Translate("hsg.propHunt.hud.huntStart"),
                    new Color(1f, 0.35f, 0.35f),
                    2f);
            }
            lastPhase = phase;
        }

        bool isSeeker = local != null && local.IsImpostor;
        bool isAlive = local != null && !local.IsDead;
        bool inHiding = phase == PropHuntPhase.Hiding;

        // --- 黑幕：只给活着的抓捕者，且开关为开 ---
        bool wantBlackout = inHiding && isSeeker && isAlive && PropHuntSettings.SeekerBlackout;
        ApplyBlackout(wantBlackout);

        // --- 大号倒计时：躲藏阶段双方都显示，位置与配色按阵营区分 ---
        bool wantCountdown = inHiding && local != null && isAlive;
        ApplyCountdown(wantCountdown, isSeeker);

        // --- 一次性提示的淡出 ---
        ApplyFlash();
    }

    private static void ApplyBlackout(bool want)
    {
        if (!want)
        {
            if (blackout != null && blackout.gameObject.activeSelf) blackout.gameObject.SetActive(false);
            return;
        }

        if (blackout == null)
        {
            blackout = UnityEngine.Object.Instantiate(HudManager.Instance.FullScreen, HudManager.Instance.transform);
            blackout.enabled = true;
        }
        if (blackout == null) return;

        // 每帧写一次颜色：原版有别的地方会去改 FullScreen 家族的 alpha，写回来最省心。
        blackout.color = new Color(0f, 0f, 0f, 1f);
        if (!blackout.gameObject.activeSelf) blackout.gameObject.SetActive(true);
    }

    private static void ApplyCountdown(bool want, bool isSeeker)
    {
        if (!want)
        {
            SetActive(bigText, false);
            SetActive(subText, false);
            return;
        }

        // 同样不用 ??= ，理由见 ShowFlash 里的注释。
        if (bigText == null) bigText = CreateText(6.5f, new Vector2(8f, 2f));
        if (subText == null) subText = CreateText(2.2f, new Vector2(10f, 1f));
        if (bigText == null || subText == null) return;

        int seconds = Mathf.CeilToInt(PropHuntState.Remaining);
        bigText.text = $"{seconds / 60:00}:{seconds % 60:00}";

        if (isSeeker)
        {
            // 抓捕者：整块屏幕都是黑的，倒计时摆正中间。
            bigText.transform.localPosition = new Vector3(0f, 0.45f, -8f);
            subText.transform.localPosition = new Vector3(0f, -0.85f, -8f);
            bigText.color = Color.white;
            subText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            subText.text = Language.Translate("hsg.propHunt.hud.blackout");
        }
        else
        {
            // 躲藏者：屏幕要留给找藏点，倒计时压到上方且缩小。
            bigText.transform.localPosition = new Vector3(0f, 1.85f, -8f);
            subText.transform.localPosition = new Vector3(0f, 1.15f, -8f);
            bigText.color = new Color(0.53f, 0.87f, 1f, 1f);
            subText.color = new Color(0.53f, 0.87f, 1f, 0.75f);
            subText.text = Language.Translate("hsg.propHunt.hud.hidingBig");
        }

        // 抓捕者的字要压在黑幕上；躲藏者没有黑幕，给个够高的默认值即可。
        int baseOrder = blackout != null && blackout.gameObject.activeSelf ? blackout.sortingOrder : 50;
        bigText.sortingOrder = baseOrder + 10;
        subText.sortingOrder = baseOrder + 10;

        SetActive(bigText, true);
        SetActive(subText, true);
    }

    private static void ApplyFlash()
    {
        if (flashText == null) return;

        if (flashTimer <= 0f)
        {
            SetActive(flashText, false);
            return;
        }

        flashTimer -= Time.deltaTime;

        // 最后半秒淡出，避免硬切。
        float alpha = flashTimer < 0.5f ? Mathf.Clamp01(flashTimer / 0.5f) : 1f;
        flashText.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
        flashText.transform.localPosition = new Vector3(0f, -1.6f, -8f);

        int baseOrder = blackout != null && blackout.gameObject.activeSelf ? blackout.sortingOrder : 50;
        flashText.sortingOrder = baseOrder + 11;

        SetActive(flashText, true);
    }

    #endregion

    #region 构造

    private static TextMeshPro? CreateText(float fontSize, Vector2 size)
    {
        if (!HudManager.InstanceExists) return null;

        var text = UnityEngine.Object.Instantiate(VanillaAsset.StandardTextPrefab, HudManager.Instance.transform);

        new TextAttributeOld(TextAttributeOld.BoldAttr)
        {
            Alignment = TMPro.TextAlignmentOptions.Center,
            Size = new Virial.Compat.Vector2(size.x, size.y),
            FontSize = fontSize,
            FontMinSize = fontSize,
            FontMaxSize = fontSize,
            AllowAutoSizing = false,
        }.Reflect(text);

        text.text = string.Empty;
        text.gameObject.SetActive(false);
        return text;
    }

    private static void SetActive(TextMeshPro? text, bool active)
    {
        if (text == null) return;
        if (text.gameObject.activeSelf != active) text.gameObject.SetActive(active);
    }

    #endregion
}
