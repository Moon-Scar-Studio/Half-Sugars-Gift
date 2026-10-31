using AmongUs.Data;
using AmongUs.Data.Player;
using Assets.InnerNet;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace HalfSugarGift.Core.Patch;

/// <summary>
/// 一条 HSG 公告。字段名与 Resources/News/News_*.json 里的键一一对应。
/// 结构与 NoS 主程序集的 Nebula.Patches.ModNews 保持一致，方便日后对齐上游改动。
///
/// ── 正文写法速查 ─────────────────────────────────────────────
/// 换行      <br>               （不能用真实换行，NoS 的 Json 解析器会把它吃掉）
/// 粗斜体    <b></b> <i></i>
/// 颜色      <color=#FFB7DD></color>   （会被自动改写成 <#FFB7DD>，原因见下）
/// 链接      [网址]显示文字[]           （后面会被自动补一个空格，原因见下）
///
/// 链接例子：[https://github.com/Half-Sugar-s-Gift/Half-Sugars-Gift]我们的仓库[]
///
/// 不要用 <size=...>，原版会把它吞掉，而且没有等价的无等号写法。
/// ─────────────────────────────────────────────────────────
/// </summary>
public class HsgNews
{
    /// <summary>公告序号。同一个 id 的公告，远程文件会覆盖包内文件</summary>
    [JsonSerializableField]
    public int id = 0;

    /// <summary>阅读界面顶部的大标题</summary>
    [JsonSerializableField]
    public string title = "";

    /// <summary>左侧列表里显示的短标题，留空则自动使用 title</summary>
    [JsonSerializableField]
    public string shortTitle = "";

    /// <summary>大标题下方的小字</summary>
    [JsonSerializableField]
    public string subTitle = "";

    /// <summary>日期。排序按字符串比较，务必统一写成 yyyy-MM-dd</summary>
    [JsonSerializableField]
    public string date = "";

    /// <summary>正文。写法见类注释</summary>
    [JsonSerializableField]
    public string detail = "";

    /// <summary>true 时只在 NoS 调试模式下显示，正式玩家看不到</summary>
    [JsonSerializableField]
    public bool debug = false;

    /// <summary>
    /// 这一条公告要不要交给原版的超链接解析器处理。默认 true。
    ///
    /// true  ：正常走原版流程，[网址]显示文字[] 会变成可点击的链接。
    /// false ：整段跳过解析。正文照常渲染（富文本、换行、颜色都还在），
    ///         但链接只会以纯文本出现。只有在某条公告怎么都渲染不出来时才需要改成 false。
    /// </summary>
    [JsonSerializableField]
    public bool allowLinks = true;

    public Announcement ToAnnouncement()
    {
        var result = new Announcement();
        result.Id = "HsgNews";
        result.Number = HsgNewsManager.NumberOffset + id;
        result.Title = title;
        result.SubTitle = subTitle;
        result.ShortTitle = string.IsNullOrEmpty(shortTitle) ? title : shortTitle;
        result.Text = HsgNewsManager.PreprocessBody(detail);
        result.Date = date ?? "";
        result.Language = (uint)DataManager.Settings.Language.CurrentLanguage;
        result.PinState = false;

        HsgNewsManager.RegisterNews(result.Number, result.Text, allowLinks);

        return result;
    }
}

/// <summary>
/// 把 HSG 自己的公告塞进主界面「新闻」栏。
///
/// 原理：原版打开新闻面板时，会把服务器拉到的公告数组交给
/// PlayerAnnouncementData.SetAnnouncements(Announcement[])。NoS 就是在这里插入自己的公告的
/// （见 NebulaPluginNova/Patches/Old/AnnouncementPopupPatch.cs 的 ModNewsHistory）。
/// 我们照着做一个自己的 Prefix 即可 —— 多个 Prefix 可以共存，互不影响。
///
///
/// ── 关于原版超链接解析器的坑（2026-09-13 实测结论）─────────────────
///
/// 原版渲染公告正文前，会先把正文交给 SelectableHyperLinkHelper.DecomposeAnnouncementText
/// 做超链接拆解。它先用一个正则在正文里找「链接候选」，再把每个候选交给 ExtractUrl 去拆。
///
/// 原版认的链接写法是：  [网址]显示文字[]
///
/// 但那个找候选的正则是按 <标签=值>内容</标签> 的形状匹配的（本意大概是抓 <link=...>），
/// 于是 <color=#FFB7DD>半糖</color> 这种颜色标签也会被当成链接候选丢给 ExtractUrl。
/// ExtractUrl 在里面找不到 '['，IndexOf 返回 -1，Substring 拿到负数长度，
/// 直接抛 ArgumentOutOfRangeException，整个 UpdateAnnouncementText 从中间断掉 ——
/// 表现就是「公告在列表里有，点开一片空白」。
///
/// 注意 <b> <i> <br> 这些不带等号的标签不会被误判，只有带等号的会（<color=> <size=> 等）。
/// 这个坑对所有插件的公告一视同仁，不是 HSG 独有的，但我们只给自己的公告兜底 ——
/// 别的插件的公告该怎么炸还是怎么炸，行为和没装 HSG 时完全一致。
///
/// 对策见 SafeguardExtractUrl。
/// ───────────────────────────────────────────────────────────────
/// </summary>
[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
public static class HsgNewsManager
{
    /// <summary>
    /// 公告编号基数。原版公告编号很小，NoS 用的是 100000 以上，
    /// 这里再往后挪一段，避免和 NoS 的公告撞号。
    /// </summary>
    public const int NumberOffset = 940000;

    /// <summary>本插件公告占用的编号上限（NumberOffset ~ NumberOffset + IdCapacity）</summary>
    private const int IdCapacity = 100000;

    /// <summary>
    /// 远程公告地址，{0} 会被替换成当前语言名（SChinese / English / Japanese）。
    /// 填上之后就能不发新版本更新公告；不想用远程公告就保持空字符串。
    /// </summary>
    private const string RemoteUrlFormat = "";
    // 例：
    // private const string RemoteUrlFormat =
    //     "https://raw.githubusercontent.com/Half-Sugar-s-Gift/Half-Sugars-Gift/main/News/News_{0}.json";

    /// <summary>包内公告文件路径，读不到当前语言就回退到 English</summary>
    private const string LocalPathFormat = "News/News_{0}.json";

    /// <summary>公告条目左侧的小图标</summary>
    private const string IconPath = "Citat/HalfSugarGift.png";

    /// <summary>
    /// 给原版的 ExtractUrl 装一道保险（强烈建议保持开启）。
    ///
    /// 做法是挂一个 Finalizer：只在原方法真的抛了异常时把异常吞掉，
    /// 不碰 url / visibleTextPart 两个返回值，也不改变任何正常路径的行为。
    /// 异常不再往上传回 il2cpp 的调用方，DecomposeHyperlinkText 就能继续把剩下的正文拆完，
    /// 公告照常渲染，只有那个被误判的片段不会变成可点击链接（它本来也不是链接）。
    ///
    /// 【作用范围】只对 HSG 自己的公告生效。
    /// 坑虽然在原版身上，但替别的插件（乃至原版自己）吞异常属于越界：
    /// 一来会掩盖不属于我们的问题，二来一旦上游或别的插件自己也做了处理，
    /// 两边叠加的行为无法预料。所以这里只在「当前正在渲染的是 HSG 公告」时才吞，
    /// 其余情况原样把异常抛回去，表现与没装本插件时完全一致。
    /// 判断口径见 IsHandlingOwnNews。
    /// </summary>
    private const bool SafeguardExtractUrl = true;

    /// <summary>
    /// 诊断模式。打开后不改变任何行为，只往 Gift.log 里抄原版超链接解析器的输入：
    /// ① 每条公告的完整正文原文；② 原版每次截出来当链接的那一段。
    /// 排查公告渲染问题时再打开，平时关着，免得刷屏。
    /// </summary>
    private const bool DiagnoseLinkParser = false;

    public static readonly List<HsgNews> AllNews = new();

    private static Image? newsIcon = null;

    private static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading HSG News");

        LoadLocalNews();

        if (RemoteUrlFormat.Length > 0 && NebulaPlugin.AllowHttpCommunication)
            yield return CoLoadRemoteNews();

        HsgDebug.Log($"[HsgNews] 共载入 {AllNews.Count} 条 HSG 公告");

        ApplyPatches();
    }

    #region 正文预处理

    /// <summary>
    /// 是否自动改写正文，绕开原版超链接解析器的两个坑（建议保持开启）。
    /// 关掉的话正文原样交给原版，两个坑都会现形。
    /// </summary>
    private const bool RewriteBodyForVanillaParser = true;

    /// <summary>
    /// 把作者写的正文改写成原版能正确处理的形式。实测依据见类注释，这里只讲做法。
    ///
    /// 坑一：带等号的富文本标签会被误判成链接候选。
    ///   原版找链接候选的正则是按 <标签=值>内容</标签> 的形状匹配的（本意大概是抓它自己生成的
    ///   <link="...">），于是 <color=#FFB7DD>半糖</color> 也会被抓走，交给 ExtractUrl 去拆，
    ///   拆不出来抛异常，那一段内容就从正文里凭空消失。
    ///   对策：改写成 TMP 的等价简写 <#FFB7DD>，没有等号就不会被匹配，</color> 照常闭合。
    ///   注意 <size=...> 没有这种简写，只能避免使用。
    ///
    /// 坑二：链接收尾的 [] 后面会被吃掉一个字符。
    ///   原版把 [网址]显示文字[] 换成自己的 <link> 标签时，会连带吞掉紧随其后的一个字符，
    ///   并补上一个空格。所以 []查看更多 会变成「 看更多」，[]<br> 会变成「 br>」。
    ///   原版自己的公告都写成 [] 加一个空格，被吃掉的正是那个空格 —— 他们知道这回事。
    ///   对策：凡是 [] 后面不是空格的，自动补一个空格上去当垫背。
    /// </summary>
    public static string PreprocessBody(string? detail)
    {
        var text = detail ?? "";
        if (!RewriteBodyForVanillaParser || text.Length == 0) return text;

        try
        {
            //坑一：<color=#RRGGBB> → <#RRGGBB>
            text = text.Replace("<color=#", "<#").Replace("<Color=#", "<#").Replace("<COLOR=#", "<#");

            //坑二：[] 后面没有空格就补一个
            var sb = new System.Text.StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                sb.Append(text[i]);

                if (text[i] != ']' || i == 0 || text[i - 1] != '[') continue;

                bool atEnd = i + 1 >= text.Length;
                if (atEnd || text[i + 1] != ' ') sb.Append(' ');
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            HsgDebug.Log($"[HsgNews] 正文预处理失败，改用原文: {ex}");
            return detail ?? "";
        }
    }

    #endregion

    #region 载入公告

    private static void LoadLocalNews()
    {
        try
        {
            var lang = Language.GetCurrentLanguage();

            var stream = NebulaAPI.AddonAsset.GetResource(string.Format(LocalPathFormat, lang))?.AsStream()
                      ?? NebulaAPI.AddonAsset.GetResource(string.Format(LocalPathFormat, "English"))?.AsStream();

            if (stream == null)
            {
                HsgDebug.Log("[HsgNews] 未找到包内公告文件，跳过本地公告");
                return;
            }

            List<HsgNews>? list;
            using (stream) list = JsonStructure.Deserialize<List<HsgNews>>(stream);

            if (list != null) Merge(list);
        }
        catch (Exception ex)
        {
            //注意：HsgDebug.LogError 会把异常重新抛出去，catch 块里只能用 Log
            HsgDebug.Log($"[HsgNews] 载入包内公告失败: {ex}");
        }
    }

    private static IEnumerator CoLoadRemoteNews()
    {
        var lang = Language.GetCurrentLanguage();
        var url = Helpers.ConvertUrl(string.Format(RemoteUrlFormat, lang));

        string? response = null;
        yield return NebulaWebRequest.CoGet(url, true,
            r => response = r,
            () => HsgDebug.Log($"[HsgNews] 拉取远程公告失败: {url}"));

        if (response == null) yield break;

        List<HsgNews>? list = null;
        try
        {
            list = JsonStructure.Deserialize<List<HsgNews>>(response);
        }
        catch (Exception ex)
        {
            HsgDebug.Log($"[HsgNews] 远程公告解析失败: {ex}");
        }

        if (list != null) Merge(list);
    }

    /// <summary>按 id 合并，后载入的（远程）覆盖先载入的（包内）</summary>
    private static void Merge(List<HsgNews> list)
    {
        foreach (var news in list)
        {
            if (news == null) continue;
            AllNews.RemoveAll(n => n.id == news.id);
            AllNews.Add(news);
        }
    }

    #endregion

    #region Harmony 补丁

    private static void ApplyPatches()
    {
        var harmony = new Harmony("HSG.News");

        TryPatch(harmony,
            AccessTools.Method(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements)),
            prefix: new HarmonyMethod(typeof(HsgNewsManager).GetMethod(nameof(SetAnnouncementsPrefix))),
            name: "PlayerAnnouncementData.SetAnnouncements");

        TryPatch(harmony,
            AccessTools.Method(typeof(AnnouncementPanel), nameof(AnnouncementPanel.SetUp)),
            postfix: new HarmonyMethod(typeof(HsgNewsManager).GetMethod(nameof(SetUpPanelPostfix))),
            name: "AnnouncementPanel.SetUp");

        //记录当前正在渲染哪一条公告，供 allowLinks = false 的旁路判断使用
        TryPatch(harmony,
            AccessTools.Method(typeof(AnnouncementPopUp), "UpdateAnnouncementText"),
            prefix: new HarmonyMethod(typeof(HsgNewsManager).GetMethod(nameof(UpdateAnnouncementTextPrefix))),
            postfix: new HarmonyMethod(typeof(HsgNewsManager).GetMethod(nameof(UpdateAnnouncementTextPostfix))),
            name: "AnnouncementPopUp.UpdateAnnouncementText");

        TryPatch(harmony,
            AccessTools.Method(typeof(SelectableHyperLinkHelper), nameof(SelectableHyperLinkHelper.DecomposeAnnouncementText)),
            prefix: new HarmonyMethod(typeof(HsgNewsManager).GetMethod(nameof(DecomposeAnnouncementTextPrefix))),
            postfix: new HarmonyMethod(typeof(HsgNewsManager).GetMethod(nameof(DecomposeAnnouncementTextPostfix))),
            name: "SelectableHyperLinkHelper.DecomposeAnnouncementText");

        if (SafeguardExtractUrl || DiagnoseLinkParser)
            TryPatch(harmony,
                AccessTools.Method(typeof(SelectableHyperLinkHelper), nameof(SelectableHyperLinkHelper.ExtractUrl)),
                prefix: DiagnoseLinkParser
                    ? new HarmonyMethod(typeof(HsgNewsManager).GetMethod(nameof(ExtractUrlDumpPrefix)))
                    : null,
                postfix: DiagnoseLinkParser
                    ? new HarmonyMethod(typeof(HsgNewsManager).GetMethod(nameof(ExtractUrlDumpPostfix)))
                    : null,
                finalizer: SafeguardExtractUrl
                    ? new HarmonyMethod(typeof(HsgNewsManager).GetMethod(nameof(ExtractUrlFinalizer)))
                    : null,
                name: "SelectableHyperLinkHelper.ExtractUrl");
    }

    private static void TryPatch(Harmony harmony, MethodBase? original,
        HarmonyMethod? prefix = null, HarmonyMethod? postfix = null, HarmonyMethod? finalizer = null,
        string name = "")
    {
        if (original == null)
        {
            HsgDebug.Log($"[HsgNews] 找不到补丁目标: {name}");
            return;
        }

        try
        {
            harmony.Patch(original, prefix: prefix, postfix: postfix, finalizer: finalizer);
            HsgDebug.Log($"[HsgNews] 已 Patch {name}");
        }
        catch (Exception ex)
        {
            HsgDebug.Log($"[HsgNews] Patch {name} 失败: {ex}");
        }
    }

    /// <summary>把 HSG 公告追加进原版公告数组，再按日期倒序排列</summary>
    public static void SetAnnouncementsPrefix([HarmonyArgument(0)] ref Il2CppReferenceArray<Announcement> aRange)
    {
        try
        {
            if (AllNews.Count == 0) return;

            List<Announcement> temp = new(aRange);

            //重复调用时不要重复插入
            temp.RemoveAll(a => a.Number >= NumberOffset && a.Number < NumberOffset + IdCapacity);

            temp.AddRange(AllNews
                .Where(n => !n.debug || DebugTools.DebugMode)
                .Select(n => n.ToAnnouncement()));

            temp.Sort((a1, a2) => string.Compare(a2.Date, a1.Date));

            aRange = temp.ToArray();

            HsgDebug.Log($"[HsgNews] 已向新闻栏插入 {AllNews.Count} 条公告");
        }
        catch (Exception ex)
        {
            HsgDebug.Log($"[HsgNews] 插入公告失败: {ex}");
        }
    }

    /// <summary>给 HSG 公告条目挂上自己的小图标（顶掉 NoS 默认加的那个星云图标）</summary>
    public static void SetUpPanelPostfix(AnnouncementPanel __instance, [HarmonyArgument(0)] Announcement announcement)
    {
        try
        {
            if (announcement.Number < NumberOffset || announcement.Number >= NumberOffset + IdCapacity) return;

            newsIcon ??= NebulaAPI.AddonAsset.GetResource(IconPath)?.AsImage(100f);
            if (newsIcon == null) return;

            //NoS 已经为 Number >= 100000 的公告加了一个名为 ModLabel 的图标，直接换图即可
            var label = __instance.transform.FindChild("ModLabel");
            SpriteRenderer renderer;

            if (label != null)
            {
                renderer = label.GetComponent<SpriteRenderer>();
                if (renderer == null) return;
            }
            else
            {
                renderer = UnityHelper.CreateObject<SpriteRenderer>(
                    "HsgNewsLabel", __instance.transform, new Vector3(-0.8f, 0.13f, 0.5f));
                renderer.gameObject.layer = __instance.gameObject.layer;
                renderer.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }

            renderer.sprite = newsIcon.GetSprite();
        }
        catch (Exception ex)
        {
            HsgDebug.Log($"[HsgNews] 公告图标处理失败: {ex}");
        }
    }

    #endregion

    #region 原版超链接解析器的保险与旁路

    /// <summary>
    /// 只吞异常，不改返回值。
    ///
    /// 原版的 ExtractUrl 只会拆 [网址]显示文字[]，拿到别的东西（比如被误判成链接候选的
    /// <color=#xxxxxx>文字</color>）就会 Substring 出负数长度并抛异常。
    /// Finalizer 返回 null 表示「异常已处理」，它就不会再传回 il2cpp 的调用方，
    /// DecomposeHyperlinkText 得以继续把剩下的正文拆完，公告正常渲染。
    ///
    /// 这里刻意不去填 url / visibleTextPart：上一版试过填成
    /// url = "", visibleTextPart = fullText，结果是链接显示成完整地址、
    /// 点击时执行 Application.OpenURL("") 把系统文件管理器打开了。
    /// 放着不填的行为已实测正常，就不再画蛇添足。
    ///
    /// 作用范围：只在当前渲染的是 HSG 公告时才吞。别人的公告（原版、NoS、其它插件）
    /// 一律把异常原样返回，交给原本该处理它的人 —— 我们不替别人兜底，也不掩盖别人的问题。
    /// </summary>
    public static Exception? ExtractUrlFinalizer(Exception? __exception, string fullText)
    {
        if (__exception == null) return null;

        //不是本插件的公告就原样抛回去，行为与没装 HSG 时一致
        if (!IsHandlingOwnNews) return __exception;

        try
        {
            HsgDebug.Log($"[HsgNews] 已拦下原版 ExtractUrl 的异常，触发片段: {Escape(fullText)}");
        }
        catch { }

        return null;
    }

    /// <summary>诊断用：把原版每次截出来当链接的那一段抄进日志，不改任何行为</summary>
    public static void ExtractUrlDumpPrefix(string fullText)
    {
        try
        {
            HsgDebug.Log($"[HsgNews][诊断] 候选段 >>>{Escape(fullText)}<<<");
        }
        catch { }
    }

    /// <summary>诊断用：拆解成功时把拆出来的网址和显示文字抄进日志</summary>
    public static void ExtractUrlDumpPostfix(string fullText, string url, string visibleTextPart)
    {
        try
        {
            HsgDebug.Log($"[HsgNews][诊断]   └ 拆解成功  网址=[{Escape(url)}]  显示文字=[{Escape(visibleTextPart)}]");
        }
        catch { }
    }

    /// <summary>
    /// 拆解结束，清掉「这次处理的是自己的公告」标记，免得残留到下一条别人的公告上。
    /// DiagnoseLinkParser 打开时顺便把最终拿去渲染的文字抄进日志。
    /// </summary>
    public static void DecomposeAnnouncementTextPostfix(string __result)
    {
        decomposingOwnNews = false;

        if (!DiagnoseLinkParser) return;

        try
        {
            HsgDebug.Log($"[HsgNews][诊断] 最终渲染文本 >>>\n{Escape(__result)}\n<<< 结束");
        }
        catch { }
    }

    /// <summary>正在渲染的公告是不是要跳过原版解析（allowLinks = false 的 HSG 公告）</summary>
    private static bool bypassingCurrent = false;

    /// <summary>需要旁路的 HSG 公告正文指纹，用于编号判断失效时兜底识别</summary>
    private static readonly HashSet<string> bypassBodies = new();

    /// <summary>需要旁路的 HSG 公告编号</summary>
    private static readonly HashSet<int> bypassNumbers = new();

    /// <summary>
    /// 本插件所有公告的正文指纹（不分 allowLinks）。
    /// 用来回答「原版此刻在处理的是不是我们的公告」，是保险与旁路的作用范围依据。
    /// </summary>
    private static readonly HashSet<string> ownBodies = new();

    public static void RegisterNews(int number, string? body, bool allowLinks)
    {
        var key = Fingerprint(body);
        if (key.Length > 0) ownBodies.Add(key);

        if (allowLinks)
        {
            bypassNumbers.Remove(number);
            return;
        }

        bypassNumbers.Add(number);

        if (key.Length > 0) bypassBodies.Add(key);
    }

    /// <summary>编号是否落在本插件占用的区段内</summary>
    private static bool IsOwnNumber(int number)
        => number >= NumberOffset && number < NumberOffset + IdCapacity;

    /// <summary>当前正在渲染的公告是否属于本插件（按公告编号判定）</summary>
    private static bool renderingOwnNews = false;

    /// <summary>当前这次超链接拆解处理的是否是本插件的公告正文（按正文指纹判定）</summary>
    private static bool decomposingOwnNews = false;

    /// <summary>
    /// 此刻原版正在处理的是不是本插件的公告。两条判据取或：
    ///   · 编号：UpdateAnnouncementText 拿到的公告编号落在本插件区段内；
    ///   · 正文指纹：DecomposeAnnouncementText 拿到的正文与某条 HSG 公告一致。
    /// 任何一条成立就算数 —— 哪怕以后原版改了那个参数的含义，指纹这条仍然兜得住；
    /// 两条都不成立就说明这是别人的公告，我们一律不插手。
    /// </summary>
    private static bool IsHandlingOwnNews => renderingOwnNews || decomposingOwnNews;

    /// <summary>去掉所有空白后的正文，用来做「是不是同一段文字」的比对，避免原版做过 Trim 之类的处理</summary>
    private static string Fingerprint(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new System.Text.StringBuilder(text!.Length);
        foreach (var c in text) if (!char.IsWhiteSpace(c)) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>
    /// 记录这次渲染的公告要不要旁路。
    /// UpdateAnnouncementText 的第一个参数是公告编号（Number），不是列表下标；
    /// 万一哪个版本改了含义，这里判断不出来也没关系，下面还有正文指纹兜底。
    /// </summary>
    public static void UpdateAnnouncementTextPrefix([HarmonyArgument(0)] int id)
    {
        bypassingCurrent = bypassNumbers.Contains(id);
        renderingOwnNews = IsOwnNumber(id);
        decomposingOwnNews = false;
    }

    public static void UpdateAnnouncementTextPostfix()
    {
        bypassingCurrent = false;
        renderingOwnNews = false;
        decomposingOwnNews = false;
    }

    /// <summary>
    /// allowLinks = false 的 HSG 公告在这里整段跳过超链接拆解，正文原样返回。
    /// 其他所有公告（原版的、NoS 的、别的插件的）一律 return true 走原路，不受影响。
    /// </summary>
    public static bool DecomposeAnnouncementTextPrefix(ref string __result, [HarmonyArgument(3)] string text)
    {
        try
        {
            var fingerprint = Fingerprint(text);

            //归属判定：正文与某条 HSG 公告对得上就认定是自己的。
            //ExtractUrl 只会在 DecomposeAnnouncementText 内部被调用，
            //所以这个标记的有效窗口正好覆盖保险需要生效的那一段。
            decomposingOwnNews = fingerprint.Length > 0 && ownBodies.Contains(fingerprint);

            bool bypass = bypassingCurrent || bypassBodies.Contains(fingerprint);

            if (DiagnoseLinkParser)
                HsgDebug.Log($"[HsgNews][诊断] 公告正文（{(bypass ? "旁路" : "正常")}）原文如下 >>>\n{Escape(text)}\n<<< 正文结束");

            if (!bypass) return true;

            __result = text ?? "";
            return false;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>把不可见字符显示成可读形式，免得抄进日志之后看不出区别</summary>
    private static string Escape(string? text)
    {
        if (text == null) return "(null)";

        var sb = new System.Text.StringBuilder(text.Length + 16);
        foreach (var c in text)
        {
            if (c == '\n') sb.Append("\\n");
            else if (c == '\r') sb.Append("\\r");
            else if (c == '\t') sb.Append("\\t");
            else if (c < 32 || c == '\u200B' || c == '\u200C' || c == '\u200D' || c == '\u2060' || c == '\uFEFF')
                sb.Append("\\u").Append(((int)c).ToString("X4"));
            else sb.Append(c);
        }
        return sb.ToString();
    }

    #endregion
}
