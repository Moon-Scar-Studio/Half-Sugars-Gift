/*
 * 赛博佛祖 镇楼
 * 永无BUG
 * 
 *                  _ooOoo_
 *                 o8888888o
 *                 88" . "88
 *                 (| -_- |)
 *                 O\  =  /O
 *              ____/`---'\____
 *            .'  \\|     |//  `.
 *           /  \\|||  :  |||//  \
 *          /  _||||| -:- |||||_  \
 *          |   | \\\  -  /// |   |
 *          | \_|  ''\---/''  |_/ |
 *          \  .-\__  `-`  ___/-. /
 *        ___`. .'  /--.--\  `. .'___
 *      ."" '<  `.___\_<|>_/___.' >' "".
 *     | | :  `- \`.;`\ _ /`;.`/ - ` : | |
 *     \  \ `-.   \_ __\ /__ _/   .-` /  /
 *======`-.____`-.___\_____/___.-`____.-'======
 *                   `=---='
 * ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
 *          菩提本无树    明镜亦非台
 *          本来无BUG    何必常修改
 * ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
 */
#region 全局引用
global using BepInEx.Unity.IL2CPP.Utils.Collections;
global using HalfSugarGift.Core;
global using HalfSugarGift.Core.Patch;
global using HalfSugarGift.Core.Settings;
global using HarmonyLib;
global using InnerNet;
global using Nebula;
global using Nebula.Behavior;
global using Nebula.Documents;
global using Nebula.Extensions;
global using Nebula.Game;
global using Nebula.Game.Statistics;
global using Nebula.Modules;
global using Nebula.Modules.ScriptComponents;
global using Nebula.Player;
global using Nebula.Roles;
global using Nebula.Roles.Abilities;
global using Nebula.Roles.Crewmate;
global using Nebula.Roles.Impostor;
global using Nebula.Roles.Modifier;
global using Nebula.Roles.Neutral;
global using Nebula.Utilities;
global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Reflection;
global using System.Text;
global using System.Threading.Tasks;
global using UnityEngine;
global using UnityEngine.Networking;
global using Virial;
global using Virial.Assignable;
global using Virial.Attributes;
global using Virial.Compat;
global using Virial.Components;
global using Virial.Configuration;
global using Virial.DI;
global using Virial.Events.Game;
global using Virial.Events.Game.Meeting;
global using Virial.Events.Player;
global using Virial.Game;
global using Virial.Media;
global using Virial.Runtime;
global using Virial.Text;
global using Virial.Utilities;
global using Citations = HalfSugarGift.Core.Citations;
global using Color = UnityEngine.Color;
global using ColorHelper = HalfSugarGift.Core.Patch.ColorHelper;
global using GamePlayer = Virial.Game.Player;
global using Vector2 = UnityEngine.Vector2;
global using Vector3 = UnityEngine.Vector3;
global using BepInEx.Unity.IL2CPP.Utils;
global using Nebula.Roles.Complex;
global using NebulaN.Roles.Modifier;
global using NebulaN.Roles.Neutral;
global using System.Linq.Expressions;
global using TMPro;
#endregion


namespace HalfSugarGift.Core.Patch;


#region Color & State & Team Class
static public class Cor
{
    static public Virial.Color cyan = new(0f, 1f, 1f);
    static public Virial.Color impRed = new(Palette.ImpostorRed.r, Palette.ImpostorRed.g, Palette.ImpostorRed.b);
    static public Virial.Color lightYellow = new(1f, 0.9f, 0.6f);
    static public Virial.Color green = new(0f, 1f, 0f);
    static public Virial.Color blue = new(0f, 0f, 1f);
    static public Virial.Color White = new(1f, 1f, 1f);
    static public Virial.Color SpiritCor = new(0f, 0.1f, 0.4f);
    static public Virial.Color MPCor = new(0.902f, 0.902f, 1f);
    static public Virial.Color Yellow = new(1f, 1f, 0f);
    static public Virial.Color LurkerCor = new(0.8f, 0, 0);
    static public Virial.Color PurpleWitchJudge = new(0.45f, 0.1f, 0.55f);
    static public Virial.Color ImaginationCor = new(128, 128, 128);
    static public Virial.Color Golden = new(0.8313725f, 0.6862745f, 0.2156863f);
    static public Virial.Color Violet = new(0.93f, 0.51f, 0.93f);
}
public class State
{
    /// <summary>
    /// 死因：碎望
    /// </summary>
    public static TranslatableTag BrokenWish = new TranslatableTag("state.brokewish");
    /// <summary>
    /// 死因：抑郁
    /// </summary>
    public static TranslatableTag Depression = new TranslatableTag("state.imaginationDepression");
    /// <summary>
    /// 死因：舞会事故
    /// </summary>
    public static TranslatableTag PartyAccident = new TranslatableTag("state.partyAccident");
    /// <summary>
    /// 死因：散灵
    /// </summary>
    public static TranslatableTag SanLing = new TranslatableTag("state.sanling");
    /// <summary>
    /// 死因：魂归
    /// </summary>
    public static TranslatableTag SoulBack = new TranslatableTag("state.soulback"); // 本来我想叫这个state.sb。
    /// <summary>
    /// 死因：无形
    /// </summary>
    public static TranslatableTag INVISIBLE = new TranslatableTag("state.invisible");
    /// <summary>
    /// 死因：审判长处刑
    /// </summary>
    public static TranslatableTag ExecutedByJudge = new TranslatableTag("state.executedByJudge");
    public static TranslatableTag BeeDieBecauseHost = new TranslatableTag("state.bdbh");
    /// <summary>
    /// 死因：护身牺牲
    /// </summary>
    public static TranslatableTag TaoistSacrifice = new TranslatableTag("state.taoistSacrifice");
    /// <summary>
    /// 死因：符咒反噬
    /// </summary>
    public static TranslatableTag AmuletTriggered = new TranslatableTag("state.amuletTriggered");
}
public static class Team
{
    /// <summary>
    /// 灵阵营
    /// </summary>
    public static readonly RoleTeam SpiritTeam = NebulaAPI.Preprocessor.CreateTeam("teams.spirit", new Virial.Color(0f, 0.1f, 0.4f), 0);
    public static readonly GameEnd SpiritWin = NebulaAPI.Preprocessor.CreateEnd("spiritWin", SpiritTeam.Color, 100);
    /// <summary>
    /// 魔女审判长阵营
    /// </summary>
    public static readonly RoleTeam WitchJudgeTeam = NebulaAPI.Preprocessor.CreateTeam("teams.witchJudge", new Virial.Color(0.45f, 0.1f, 0.55f), 0);
    public static readonly GameEnd WitchJudgeWin = NebulaAPI.Preprocessor.CreateEnd("witchJudgeWin", WitchJudgeTeam.Color, 100);
    public static readonly ExtraWin ExtraWitchJudgeWin = NebulaAPI.Preprocessor.CreateExtraWin("witchJudgeExtraWin", WitchJudgeTeam.Color);
    public static readonly RoleTeam ImaginationTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.imagination", new Virial.Color(128, 128, 128), TeamRevealType.OnlyMe);
    public static readonly GameEnd ImaginationWin = NebulaAPI.Preprocessor!.CreateEnd("end.imaginationWin", ImaginationTeam.Color,80);
    public static readonly ExtraWin ExtraBeeWin = NebulaAPI.Preprocessor.CreateExtraWin("extrawin.bee",new Virial.Color(1.0f, 0.8431373f, 0.0f));
    public static readonly RoleTeam BeeTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.bee", new Virial.Color(128, 128, 128),TeamRevealType.OnlyMe);
    /// <summary>
    /// 道士阵营
    /// </summary>
    public static readonly RoleTeam TaoistTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.taoist", new Virial.Color(0.8f, 0.7f, 0.2f), 0);
    public static readonly GameEnd TaoistWin = NebulaAPI.Preprocessor!.CreateEnd("taoistWin", TaoistTeam.Color, 100);
    public static readonly ExtraWin ExtraTaoistWin = NebulaAPI.Preprocessor!.CreateExtraWin("taoistExtraWin", TaoistTeam.Color);
}

#endregion

#region PatchManager主类
public static partial class PatchManager
{
    public static readonly IConfigurationHolder MVS = NebulaAPI.Configurations.Holder(
        NebulaAPI.GUI.LocalizedTextComponent("options.hsg.mvs.holder.title"),
        NebulaAPI.GUI.LocalizedTextComponent("options.hsg.mvs.holder.detail"),
        new[] { ConfigurationTab.Settings },
        GameModes.AllGameModes
    );
    public static readonly IConfigurationHolder RandomEvents = NebulaAPI.Configurations.Holder(
        NebulaAPI.GUI.LocalizedTextComponent(""),
        NebulaAPI.GUI.LocalizedTextComponent(""),
        new[] { ConfigurationTab.Settings },
        GameModes.AllGameModes
        );
    public static readonly IConfigurationHolder FunMode = NebulaAPI.Configurations.Holder(
    NebulaAPI.GUI.LocalizedTextComponent("options.hsg.fm.holder.title"),
    NebulaAPI.GUI.LocalizedTextComponent("options.hsg.fm.holder.detail"),
    new[] { ConfigurationTab.Settings },
    GameModes.AllGameModes
    );
    public static RemoteProcess<byte> RpcPlayMeetingDeath = new("PlayMeetingDeath", (victimId, _) =>
    {
        var victim = GamePlayer.GetPlayer(victimId);
        if (victim == null) return;
        NebulaManager.Instance.StartCoroutine(CoMeetingDeath(victim).WrapToIl2Cpp());
    });
    static PatchManager()
    {
        LoadMVS();
        LoadRandomEventConfiguration();
        LoadFunModeConfiguration();
    }
    static void LoadMVS()
    {
        MVS.AppendConfiguration(MoreVoteSettings.EnableVoteTimeChange);
        MVS.AppendConfiguration(MoreVoteSettings.TriggerCount);
        MVS.AppendConfiguration(MoreVoteSettings.VoteDuration);
        HsgDebug.Log("MVS 加载");
    }
    //static void LoadPictures()
    //{
    //    Hint WithImage(string id) => new HintWithImage(NebulaAPI.AddonAsset.GetResource("Hints/" + id.HeadUpper() + ".png")!.AsImage()!, new TranslateTextComponent("hint." + id.HeadLower() + ".title"), new TranslateTextComponent("hint." + id.HeadLower() + ".detail"));
    //    HintManager.AllHints = [];
    //}
    static void LoadRandomEventConfiguration()
    {
        RandomEvents.AppendConfiguration(RandomEventSettings.EnableRandomEventsSettings);
        RandomEvents.AppendConfiguration(RandomEventSettings.EnableChangeRandomTime);
        RandomEvents.AppendConfiguration(RandomEventSettings.RandomTime);
        RandomEvents.AppendConfiguration(RandomEventSettings.MeetingWeight);
        RandomEvents.AppendConfiguration(RandomEventSettings.KillWeight);
        RandomEvents.AppendConfiguration(RandomEventSettings.SwapWeight);
        RandomEvents.AppendConfiguration(RandomEventSettings.FogWeight);
        RandomEvents.AppendConfiguration(RandomEventSettings.StoneWeight);
        RandomEvents.AppendConfiguration(RandomEventSettings.PartyWeight);
        RandomEvents.AppendConfiguration(RandomEventSettings.GetKeyWeight);
        HsgDebug.Log("随机事件配置加载");
    }
    static void LoadFunModeConfiguration()
    {
        FunMode.AppendConfiguration(FunModes.EnableMiniMode);
        FunMode.AppendConfiguration(FunModes.MiniScale);
    }
    static RemoteProcess<(byte playerId, float x, float y)> RpcRequestMove = new("RpcRequestMove", (msg, _) =>
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var target = GamePlayer.GetPlayer(msg.playerId);
        if (target == null || target.IsDead) return;
        target.VanillaPlayer.NetTransform.RpcSnapTo(new Vector2(msg.x, msg.y));
    });
    public static void MovePlayer(GamePlayer player, Vector2 position)
    {
        if (player == null) return;
        if (AmongUsClient.Instance.AmHost)
        {
            player.VanillaPlayer.NetTransform.RpcSnapTo(position);
        }
        else
        {
            RpcRequestMove.Invoke((player.PlayerId, position.x, position.y));
        }
    }
    /// <summary>
    /// 显示全屏半透明颜色覆盖层
    /// </summary>
    /// <param name="color">覆盖颜色</param>
    /// <param name="duration">持续时间（秒），-1 表示永久</param>
    /// <param name="pulse">是否启用透明度脉冲闪烁</param>
    /// <param name="pulseCount">脉冲次数（每次为 0.2→0.5 或 0.5→0.2 的单程变化）</param>
    /// <returns>覆盖层 GameObject，可用于提前移除</returns>
    public static GameObject ShowScreenOverlay(Color color, float duration = -1f, bool pulse = false, int pulseCount = 3)
    {
        var flash = GameObject.Instantiate(HudManager.Instance.FullScreen, HudManager.Instance.transform);
        flash.color = color;
        flash.enabled = true;
        flash.gameObject.SetActive(true);

        if (pulse && pulseCount > 0)
        {
            float startAlpha = 0.2f;
            float maxAlpha = 0.5f;
            float minAlpha = 0.2f;
            float durationPerStep = 0.3f;
            HudManager.Instance.StartCoroutine(CoPulse(flash, startAlpha, maxAlpha, minAlpha, durationPerStep, pulseCount));
        }
        else
        {
            var c = flash.color;
            c.a = 0.5f;
            flash.color = c;
        }
        if (duration > 0)
        {
            HudManager.Instance.StartCoroutine(CoRemoveAfter(flash, duration));
        }

        return flash.gameObject;
    }
    private static IEnumerator CoPulse(SpriteRenderer flash, float startAlpha, float maxAlpha, float minAlpha, float stepDuration, int maxChanges)
    {
        float current = startAlpha;
        bool increasing = true;
        int changesDone = 0;
        var color = flash.color;

        while (changesDone < maxChanges && flash != null)
        {
            float target = increasing ? maxAlpha : minAlpha;
            float elapsed = 0f;
            float start = current;

            while (elapsed < stepDuration && flash != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / stepDuration;
                current = Mathf.Lerp(start, target, t);
                color.a = current;
                flash.color = color;
                yield return null;
            }
            current = target;
            color.a = current;
            flash.color = color;

            changesDone++;
            increasing = !increasing;
        }

    }
    public static void Tip(IEnumerable<GamePlayer> players, Color? color = null, float fadeIn = 0.2f, float fadeOut = 0.8f)
    {
        if (players == null) return;
        string hex = color.HasValue ? ColorUtility.ToHtmlStringRGB(color.Value) : "#FF0000";
        foreach (var p in players)
        {
            if (p == null || p.IsDead) continue;
            RpcFlashCustom.Invoke((p.PlayerId, hex, fadeIn, fadeOut));
        }
    }

    public static void Tip(GamePlayer player, Color? color = null, float fadeIn = 0.2f, float fadeOut = 0.8f)
    {
        if (player == null || player.IsDead) return;
        Tip(new[] { player }, color, fadeIn, fadeOut);
    }
    private static IEnumerator CoRemoveAfter(SpriteRenderer flash, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (flash != null)
        {
            flash.enabled = false;
            GameObject.Destroy(flash.gameObject);
        }
    }
    static IEnumerator CoMeetingDeath(GamePlayer victim)
    {
        var hud = MeetingHud.Instance;
        if (hud == null) yield break;
        var states = hud.playerStates;
        foreach (var state in states) state.gameObject.SetActive(false);
        yield return null;
        var victimState = states.FirstOrDefault(s => s.PlayerId == victim.PlayerId);
        if (victimState != null) victimState.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.2f);
        victim.MurderPlayer(victim, PlayerStates.Dead, null, KillParameter.NormalKill);
        yield return new WaitForSeconds(1);
        foreach (var DeadBody in UnityEngine.Object.FindObjectsOfType<DeadBody>())
        {
            if (DeadBody.ParentId == victim.PlayerId)
            {
                UnityEngine.Object.Destroy(DeadBody.gameObject);
                break;
            }
        }
        yield return new WaitForSeconds(0.5f);
        AmongUsUtil.PlayQuickFlash(Cor.impRed);
        yield return new WaitForSeconds(1f);
        foreach (var state in states) state.gameObject.SetActive(true);
    }

    public static void Play(GamePlayer victim)
    {
        if (AmongUsClient.Instance.AmHost)
            RpcPlayMeetingDeath.Invoke(victim.PlayerId);
    }
    public static string GetPlayerHexColor(GamePlayer player)
    {
        PlayerControl pc = null;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p.PlayerId == player.PlayerId)
            {
                pc = p;
                break;
            }
        }
        if (pc == null) return "#FFFFFF";

        int colorId = pc.Data.DefaultOutfit.ColorId;
        UnityEngine.Color color = Palette.PlayerColors[colorId];
        return ColorUtility.ToHtmlStringRGB(color);
    }
    /// <summary>
    /// 获取玩家当前颜色的本地化名称
    /// </summary>
    public static string GetPlayerColorName(GamePlayer player)
    {
        var pc = player.VanillaPlayer;
        if (pc == null) return "白色";

        int colorId = pc.Data.DefaultOutfit.ColorId;
        try
        {
            var names = Palette.ColorNames;
            if (names != null && colorId >= 0 && colorId < names.Length)
            {
                var name = DestroyableSingleton<TranslationController>.Instance.GetString(names[colorId]);
                if (!string.IsNullOrEmpty(name)) return name;
            }
        }
        catch { }
        return $"玩家{colorId}";
    }
    public static RemoteProcess<GamePlayer> RpcRemoveBody = new("AvengerRemoveBody",(player, _) =>
    {
        foreach (var body in Helpers.AllDeadBodies().Where(b => b.ParentId == player.PlayerId))
        {
            UnityEngine.Object.Destroy(body.gameObject);
        }
    });
    public static RemoteProcess<(byte targetId, string colorHex)> RpcFlash = new("RpcCustomFlash", (msg, _) =>
    {
        if (GamePlayer.GetPlayer(msg.targetId)?.AmOwner == true)
        {
            if (ColorUtility.TryParseHtmlString(msg.colorHex, out Color color))
                AmongUsUtil.PlayQuickFlash(color.ToVirialColor());
            else
                AmongUsUtil.PlayQuickFlash(Color.red.ToVirialColor());
            AmongUsUtil.PlayCustomFlash(color.ToVirialColor(), 1f, 1f, 0.5f, 10f);
        }
    });
    public static RemoteProcess<string> RpcFlashAll = new("RpcCustomFlash", (colorHex, _) =>
    {
        var local = GamePlayer.LocalPlayer;
        if (local == null || local.IsDead) return;
        if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
            color = Color.red;
        AmongUsUtil.PlayQuickFlash(color.ToVirialColor());
    });
    public static RemoteProcess<(byte targetId, string colorHex, float fadeIn, float fadeOut)> RpcFlashCustom = new("RpcFlashCustom", (msg, _) =>
    {
        var player = GamePlayer.GetPlayer(msg.targetId);
        if (player?.AmOwner != true) return;

        if (ColorUtility.TryParseHtmlString(msg.colorHex, out Color color))
        {
            AmongUsUtil.PlayCustomFlash(color.ToVirialColor(), msg.fadeIn, msg.fadeOut);
        }
    });
    public static Virial.Color ToVirialColor(this Color color)
    {
        return new Virial.Color(color.r, color.g, color.b, color.a);
    }
    static public bool OpenRoleSelectWindowUsingTabs(
    IEnumerable<DefinedRole>? roles, (string? tab, Predicate<DefinedRole>? predicate)[] tabs, bool impRolesArrangeAtFirst, string underText,
    Action<DefinedRole> onSelected, ref MetaScreen __result, bool showCloseButton = false)
    {
        var window = MetaScreen.GenerateWindow(
            new(7.6f, 4.2f),
            HudManager.Instance.transform,
            new Vector3(0, 0, -50f),
            true, false, withCloseButton: showCloseButton
        );

        MetaWidgetOld widget = new();
        MetaWidgetOld inner = new();
        if (roles == null)
        {
            HashSet<DefinedRole> roleSet = [];
            foreach (var r in Nebula.Roles.Roles.AllRoles)
                foreach (var abilityRole in r.GetGuessableAbilityRoles())
                    roleSet.Add(abilityRole);
            foreach (var type in AssignmentType.AllTypes)
            {
                if (!type.CanGuessAsAbility) continue;
                foreach (var r in Nebula.Roles.Roles.AllRoles)
                {
                    if (type.Predicate.Invoke(r.AssignmentStatus, r) &&
                        r.GetCustomAllocationParameters(type)?.RoleCountSum > 0)
                        roleSet.Add(r);
                }
            }
            roles = roleSet;
        }

        int CategoryToInt(RoleCategory roleCategory) => roleCategory switch
        {
            RoleCategory.ImpostorRole => impRolesArrangeAtFirst ? 0 : 1,
            RoleCategory.CrewmateRole => impRolesArrangeAtFirst ? 1 : 0,
            _ => 2
        };

        bool isFirst = true;
        foreach (var tab in tabs)
        {
            var ary = roles.Where(r => tab.predicate?.Invoke(r) ?? true).ToArray();
            ary.Sort((r1, r2) =>
            {
                if (r1.Category == r2.Category) return r1.InternalName.CompareTo(r2.InternalName);
                return CategoryToInt(r1.Category).CompareTo(CategoryToInt(r2.Category));
            });

            if (isFirst) isFirst = false;
            else inner.Append(new MetaWidgetOld.VerticalMargin(0.1f));

            if (tab.tab != null)
                inner.Append(new MetaWidgetOld.Text(MeetingRoleSelectWindow.TabAttribute)
                {
                    MyText = new RawTextComponent(tab.tab),
                    Alignment = IMetaWidgetOld.AlignmentOption.Center
                });

            inner.Append(ary, r => new CombinedWidgetOld(
                new MetaWidgetOld.HorizonalMargin(0.1f),
                new MetaWidgetOld.Button(() => onSelected.Invoke(r), MeetingRoleSelectWindow.ButtonAttribute)
                {
                    RawText = r.DisplayColoredName,
                    TextHorizonotalExtraMargin = 0.15f,
                    PostBuilder = (button, renderer, text) =>
                    {
                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        button.transform.localPosition += new Vector3(0.05f, 0f, 0f);
                        text.transform.localPosition += new Vector3(0.072f, 0f, 0f);
                        var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", button.transform, new(-0.65f, 0f, -0.1f));
                        icon.sprite = r.GetRoleIcon()?.GetSprite();
                        icon.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        icon.material = RoleIcon.GetRoleIconMaterial(r, 0.8f);
                        icon.transform.localScale = new(0.253f, 0.253f, 1f);
                        icon.SetBothOrder(15);
                    }
                }), 4, -1, 0, 0.59f);
        }
        MetaWidgetOld.ScrollView scroller = new(new(6.9f, 3f), inner, true)
        {
            Alignment = IMetaWidgetOld.AlignmentOption.Center
        };

        widget.Append(scroller);
        widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttr)
        {
            MyText = new RawTextComponent(underText),
            Alignment = IMetaWidgetOld.AlignmentOption.Center
        });

        window.SetWidget(widget);
        IEnumerator CoCloseOnResult()
        {
            if (MeetingHud.Instance)
                while (MeetingHud.Instance.state != MeetingHud.MeetingStates.Results) yield return null;
            else
                while (!MeetingHud.Instance) yield return null;
            window.CloseScreen();
        }
        window.StartCoroutine(CoCloseOnResult().WrapToIl2Cpp());

        __result = window;
        return false;
    }
    private static bool _initialized;
    private static readonly Dictionary<byte, float> _mutedUntilTime = new();
    private static readonly HashSet<byte> _mutedInMeeting = new();
    private static bool _lastMuteState = false;

    private static readonly RemoteProcess<(byte targetId, float duration, bool onlyMeeting)> _rpcMute =
        new RemoteProcess<(byte targetId, float duration, bool onlyMeeting)>("RpcMutePlayer", (msg, _) =>
        {
            var local = AmongUsLLImpl.LocalPlayer;
            if (msg.onlyMeeting)
            {
                _mutedInMeeting.Add(msg.targetId);


                if (AmongUsLLImpl.LocalPlayer != null && AmongUsLLImpl.LocalPlayer.PlayerId == msg.targetId)
                {
                    if (MeetingHud.Instance != null)
                        SendLocalMessage(Language.Translate("mute.meetingmute"));
                    else
                        AmongUsUtil.PlayQuickFlash(Cor.MPCor);
                }
            }
            else
            {
                _mutedUntilTime[msg.targetId] = Time.time + msg.duration;
                if (AmongUsLLImpl.LocalPlayer != null && AmongUsLLImpl.LocalPlayer.PlayerId == msg.targetId)
                {
                    if (MeetingHud.Instance != null)
                        SendLocalMessage(Language.Translate("mute.normalmute").Replace("%DUR%",msg.duration.ToString()));
                    else
                        AmongUsUtil.PlayQuickFlash(Cor.MPCor);
                }
            }
        });

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        var game = NebulaAPI.CurrentGame;
        var harmony = new Harmony("MuteUtility");
        harmony.Patch(
            original: typeof(ChatController).GetMethod(nameof(ChatController.SendChat)),
            prefix: new HarmonyMethod(typeof(MutePatch).GetMethod(nameof(MutePatch.Prefix)))
        );

        GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(_ =>
        {
            _mutedInMeeting.Clear();
        },game);

        GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(_ =>
        {
            var local = AmongUsLLImpl.LocalPlayer;
            if (local == null) return;

            byte pid = local.PlayerId;
            bool isMuted = _mutedInMeeting.Contains(pid) ||
                           (_mutedUntilTime.TryGetValue(pid, out float until) && until > Time.time);
            if (_lastMuteState && !isMuted)
            {
                if (MeetingHud.Instance != null)
                    SendLocalMessage(Language.Translate("mute.muteend"));
                else
                    AmongUsUtil.PlayQuickFlash(Cor.green);
            }
            _lastMuteState = isMuted;
        },game);
    }

    public static void MutePlayer(GamePlayer target, float seconds)
    {
        if (target == null) return;
        _rpcMute.Invoke((target.PlayerId, seconds, false));
    }

    public static void MutePlayerUntilMeetingEnd(Player target)
    {
        if (target == null) return;
        _rpcMute.Invoke((target.PlayerId, 0f, true));
    }

    private static class MutePatch
    {
        public static bool Prefix(ChatController __instance)
        {
            var local = AmongUsLLImpl.LocalPlayer;
            if (local == null) return true;

            byte pid = local.PlayerId;
            if (_mutedInMeeting.Contains(pid))
                return false;

            if (_mutedUntilTime.TryGetValue(pid, out float until) && until > Time.time)
                return false;

            return true;
        }
    }
    /// <summary>
    /// 获取随机玩家。太有用了。
    /// </summary>
    /// <param name="count">获取数量。</param>
    /// <param name="predicate">过滤条件。默认只要活的。</param>
    /// <returns>玩家的列表。</returns>
    public static List<GamePlayer> GetRandomPlayers(int count,Func<GamePlayer,bool> predicate = null)
    {
        try
        {
            var ap = GamePlayer.AllPlayers;

            if (predicate != null)
                ap = ap.Where(predicate);
            else if (predicate == null)
                ap = ap.Where(p => !p.IsDead);
            var l = ap.ToList();
            var randomPlayers = l.OrderBy(a => Guid.NewGuid()).Take(count).ToList();
            return randomPlayers;

        }
        catch(Exception ex)
        {
            HsgDebug.LogWarning($"未知错误：{ex.Message},触发点：public static List<GamePlayer> GetRandomPlayers()，将返回全部玩家。");
            return [.. GamePlayer.AllPlayers];
        }
    }
    /// <summary>
    /// 获取一个随机玩家。太有用了。
    /// </summary>
    /// <param name="predicate">过滤条件。默认只要活的。</param>
    /// <returns>一个随机玩家。</returns>
    public static GamePlayer GetRandomPlayer(Func<GamePlayer, bool> predicate = null)
    {
        try
        {
            var ap = GamePlayer.AllPlayers.ToList();
            if (predicate != null)
                ap = GamePlayer.AllPlayers.Where(predicate).ToList();
            else if(predicate == null)
                ap = GamePlayer.AllPlayers.Where(p => !p.IsDead).ToList();
            int index = UnityEngine.Random.Range(0, ap.Count);
            var randomPlayer = ap[index];
            return randomPlayer;
        }
        catch(Exception ex)
        {
            HsgDebug.LogWarning($"未知错误：{ex.Message},触发点：public static GamePlayer GetRandomPlayer，将返回本地玩家。");
            return GamePlayer.LocalPlayer;
        }
        
    }
    /// <summary>
    /// 获取伪装者列表。太有用了。
    /// </summary>
    /// <param name="predicate">过滤条件。默认啥都要。只要是狼。       饿了啥都吃。</param>
    /// <returns>红狼列表。</returns>
    public static List<GamePlayer> GetImpostors(Func<GamePlayer, bool> predicate = null)
    {
        var impostors = GamePlayer.AllPlayers.Where(p => p.IsImpostor).ToList();
        if(predicate != null)
            impostors = impostors.Where(predicate).ToList();
        return impostors;
    }
    /// <summary>
    /// 获取船员列表。太有用了。
    /// </summary>
    /// <param name="predicate">过滤条件。默认啥都要。只要是船员。</param>
    /// <returns>船员列表。</returns>
    public static List<GamePlayer> GetCrewmate(Func<GamePlayer, bool> predicate = null)
    {
        var crewmate = GamePlayer.AllPlayers.Where(p => p.IsCrewmate).ToList();
        if (predicate != null)
            crewmate = crewmate.Where(predicate).ToList();
        return crewmate;
    }
    /// <summary>
    /// 获取中立列表。太有用了。
    /// </summary>
    /// <param name="predicate">过滤条件。默认啥都要。只要是中立。</param>
    /// <returns>中立列表。</returns>
    public static List<GamePlayer> GetNeutral(Func<GamePlayer, bool> predicate = null)
    {
        var neutral = GamePlayer.AllPlayers.Where(p => !p.IsCrewmate && !p.IsImpostor).ToList();
        if (predicate != null)
            neutral = neutral.Where(predicate).ToList();
        return neutral;
    }
}


#endregion

#region PatchManagerClass2
[HarmonyPatch(typeof(HudManager), nameof(HudManager.SetHudActive), typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool))]
public static class TaskPanelPatch
{
    public static void Postfix(HudManager __instance, RoleBehaviour role, bool isActive)
    {
        if (!MeetingHud.Instance) return;
        var openPosition = __instance.TaskPanel.openPosition;
        openPosition.z = -20f;
        __instance.TaskPanel.openPosition = openPosition;
        __instance.TaskPanel.gameObject.SetActive(true);
    }
}
[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
[NebulaRPCHolder]
public static partial class PatchManager
{
    static float _lastMsgTime = -3f;
    static readonly string _cfgPath = Path.Combine(Application.persistentDataPath, "Hsg_Commands.json");
    static CommandSettings _settings;
    private static int _myTitleId = 0;

    static HashSet<string> DevCodes = new()
    {
        "copysworn#2096", // hvtXsvc
        "snowyvisit#0332",// 海豚
        "pasthusky#6309",// 妙悟
        "ablazeflex#9776"// 半糖
    };
    static HashSet<string> AdminCodes = new()
    {
        "duethree#4027",// ㊗️nes
        "soppypager#6883"// 信
    };
    static HashSet<string> SponsorCodes = new()
    {
    };

    static List<TitleInfo> _cachedTitles = new();
    static Dictionary<byte, int> _playerTitleMap = new();
    static Dictionary<byte, TitleInfo> _receivedTitleInfo = new();
    static bool _isLoading = false;

    static bool _titleEventSubscribed = false;

    public static string? GetFriendCode(PlayerControl player)
    {
        var client = GetClient(player);
        return client?.FriendCode;
    }
    public static Color GetPlayerColor(this GamePlayer player)
    {
        if (player == null) return Color.white;
        var pc = player.VanillaPlayer;
        if (pc != null && pc.Data != null)
        {
            int colorID = pc.Data.DefaultOutfit.ColorId;
            if(colorID >= 0 && colorID < Palette.PlayerColors.Length)
                return Palette.PlayerColors[colorID];
        }
            return Color.white;
    }
    public static bool IsHost(PlayerControl player) => AmongUsClient.Instance.AmHost;
    public static bool IsDev(PlayerControl player)
    {
        string code = GetFriendCode(player);
        return code != null && DevCodes.Contains(code);
    }
    public static bool IsAdmin(PlayerControl player)
    {
        string code = GetFriendCode(player);
        return code != null && AdminCodes.Contains(code);
    }
    public static bool IsSponsor(PlayerControl player)
    {
        string code = GetFriendCode(player);
        return !string.IsNullOrEmpty(code) && SponsorCodes.Contains(code);
    }
    public static bool CanManageTitles(PlayerControl player)
        => IsSponsor(player) || IsDev(player) || IsAdmin(player);

    public static void SendLocalMessage(string msg)
    {
        var pc = PlayerControl.LocalPlayer;
        string orig = pc.name;
        pc.SetName("System");
        HudManager.Instance.Chat.AddChat(pc, msg);
        pc.SetName(orig);
    }

    public static void SendLocalNotification(string msg)
    {
        var notifier = HudManager.Instance.Notifier;
        var newMessage = Instantiate<LobbyNotificationMessage>(
            notifier.notificationMessageOrigin,
            Vector3.zero, Quaternion.identity, notifier.transform);
        newMessage.transform.localPosition = new Vector3(0f, 0f, -2f);
        newMessage.SetUp(msg,
            notifier.settingsChangeSprite,
            notifier.settingsChangeColor,
            (Action)(() => notifier.OnMessageDestroy(newMessage)));
        notifier.ShiftMessages();
        notifier.AddMessageToQueue(newMessage);
        AmongUsLLImpl.SoundManagerInstance.PlaySoundImmediate(
            notifier.settingsChangeSound, false, 1f, 1f, null);
    }

    private static T Instantiate<T>(T notificationMessageOrigin, Vector3 zero, Quaternion identity, Transform transform)
    {
        throw new NotImplementedException();
    }

    public static bool SendNormalMessage(string msg)
    {
        if (Time.time - _lastMsgTime < 3f) return false;
        _lastMsgTime = Time.time;
        PlayerControl.LocalPlayer.RpcSendChat(msg);
        return true;
    }

    public static ClientData? GetClient(PlayerControl player)
    {
        try
        {
            return AmongUsClient.Instance.allClients
                .ToArray().FirstOrDefault(cd => cd.Character?.PlayerId == player.PlayerId);
        }
        catch { return null; }
    }

    public static void LoadSettings()
    {
        if (!File.Exists(_cfgPath))
        {
            _settings = new CommandSettings();
            SaveSettings();
            return;
        }
        string json = File.ReadAllText(_cfgPath);
        _settings = JsonStructure.Deserialize<CommandSettings>(json);
    }

    public static void SaveSettings()
    {
        string json = JsonStructure.Serialize(_settings);
        File.WriteAllText(_cfgPath, json);
    }
    

    static void LoadCommands()
    {
        var harmony = new Harmony("Hsg.addon.commands");
        var original = typeof(ChatController).GetMethod("SendChat");
        var prefix = new HarmonyMethod(typeof(PatchManager).GetMethod(nameof(OnSendChat), BindingFlags.Static | BindingFlags.NonPublic));
        harmony.Patch(original, prefix);
        LoadSettings();
        Initialize();
        _titleEventSubscribed = false;
        HostSendRpc.RegisterCustomRpc("HSG_SetTitle", OnReceiveSetTitle);
        // 牛仔对决开局初始化改由 CowboyDuelGameStartPatch 补丁触发（GameOperatorManager 每局重建，加载期订阅无效），
        // 标题刷新同理已移入各自补丁/模块生命周期。
    }
    static void LoadTitles()
    {
        if (GameOperatorManager.Instance != null)
        {
            //SubscribeTitleEvent();
        }
        if (GameOperatorManager.Instance != null)
        {
            GameOperatorManager.Instance.Subscribe<GameStartEvent>(_ =>
            {
                _receivedTitleInfo.Clear();
                if (_myTitleId > 0)
                {
                    ApplyTitleLocally(_myTitleId);
                }
            }, new SimpleLifespan());
        }
    }
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        LoadCommands();
        HsgDebug.Log("指令加载");
        Initialize();
        HsgDebug.Log("禁言补丁加载");
        _titleEventSubscribed = false;
        HostSendRpc.RegisterCustomRpc("HSG_SetTitle", OnReceiveSetTitle);
        LoadTitles();
        HsgDebug.Log("插件加载成功");
    }

    //private static void SubscribeTitleEvent()
    //{
    //    if (_titleEventSubscribed) return;
    //    if (GameOperatorManager.Instance == null) return;
    //    GameOperatorManager.Instance.Subscribe<PlayerDecorateNameEvent>(OnDecorateName, new SimpleLifespan());
    //    _titleEventSubscribed = true;
    //}

    //public static void EnsureTitleEventSubscribed()
    //{
    //    if (_titleEventSubscribed) return;
    //    if (GameOperatorManager.Instance != null)
    //    {
    //        SubscribeTitleEvent();
    //    }
    //    else
    //    {
    //        NebulaManager.Instance.StartCoroutine(WaitForGameOperator());
    //    }
    //}

    //static IEnumerator WaitForGameOperator()
    //{
    //    float timeout = 10f;
    //    float elapsed = 0f;
    //    while (elapsed < timeout)
    //    {
    //        if (GameOperatorManager.Instance != null)
    //        {
    //            //SubscribeTitleEvent();
    //            yield break;
    //        }
    //        elapsed += Time.deltaTime;
    //        yield return null;
    //    }
    //}

    static bool OnSendChat(ChatController __instance)
    {
        bool inLobby = LobbyBehaviour.Instance != null;
        bool isHost = IsHost(PlayerControl.LocalPlayer);
        bool isDev = IsDev(PlayerControl.LocalPlayer);
        bool isAdmin = IsAdmin(PlayerControl.LocalPlayer);

        string text = __instance.freeChatField.textArea.text.Trim();
        string raw = __instance.freeChatField.textArea.text;
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;
        if (parts[0][0] != '/')
        {
            if (_settings.SmyStatus)
            {
                bool sent = SendNormalMessage($"?! {raw} !?");
                if (sent) __instance.freeChatField.Clear();
                return false;
            }
            if (_settings.CatMode)
            {
                bool sent = SendNormalMessage($"{raw}喵~");
                if (sent) __instance.freeChatField.Clear();
                return false;
            }
            return true;
        }

        string cmd = parts[0].ToLower();
        switch (cmd)
        {
            case "/ghelp":
            case "/help":
                ShowHelp();
                __instance.freeChatField.Clear();
                return false;
            case "/return":
            case "/kickself":
            case "/quit":
                if (isHost)
                {
                    __instance.freeChatField.Clear();
                    return false;
                }
                string reason = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : null;
                RpcReturnRequest.Invoke((PlayerControl.LocalPlayer.PlayerId, reason));
                __instance.freeChatField.Clear();
                return false;

            case "/checkbait":
            case "/cb":
                BaitCheck();
                __instance.freeChatField.Clear();
                return false;
            case "/prisay":
            case "/ps":
                if (parts.Length < 3 || (!isDev && !isAdmin)) return false;
                string tName = parts[1];
                string msg = string.Join(" ", parts.Skip(2));
                PlayerControl target = null;
                foreach (var p in PlayerControl.AllPlayerControls)
                    if (p.Data.PlayerName.Contains(tName, StringComparison.OrdinalIgnoreCase)) { target = p; break; }
                if (target == null)
                {
                    SendLocalMessage($"未找到玩家: {tName}");
                    __instance.freeChatField.Clear();
                    return false;
                }
                if (target == PlayerControl.LocalPlayer)
                {
                    SendLocalMessage("不能给自己发私聊");
                    __instance.freeChatField.Clear();
                    return false;
                }
                PriMsg.Invoke(((byte)target.PlayerId, (byte)PlayerControl.LocalPlayer.PlayerId, msg));
                SendLocalMessage($"私聊发送成功！内容：{msg}");
                __instance.freeChatField.Clear();
                return false;

            case "/perm":
            case "/permission":
            case "/p":
                SendLocalMessage(isDev ? "你是开发者" : (isAdmin ? "你是管理员" : "你是普通玩家"));
                
                __instance.freeChatField.Clear();
                return false;

            case "/smy":
            case "/surprisemyself":
                try
                {
                    _settings.SmyStatus = bool.Parse(parts[1]);
                    SaveSettings();
                    SendLocalMessage($"诡异模式已{(_settings.SmyStatus ? "开启" : "关闭")}");
                }
                catch { SendLocalMessage("用法: /smy <true/false>"); }
                __instance.freeChatField.Clear();
                return false;

            case "/cat":
                try
                {
                    _settings.CatMode = bool.Parse(parts[1]);
                    SaveSettings();
                    SendLocalMessage($"猫娘模式已{(_settings.CatMode ? "开启" : "关闭")}");
                }
                catch { SendLocalMessage("用法: /cat <true/false>"); }
                __instance.freeChatField.Clear();
                return false;

            //    case "/hsgtitle":
            //    case "/ht":
            //        EnsureTitleEventSubscribed();

            //        __instance.freeChatField.Clear();
            //        if (parts.Length < 2)
            //        {
            //            SendLocalMessage("用法错误");
            //            return false;
            //        }
            //        string scmd = parts[1].ToLower();
            //        switch (scmd)
            //        {
            //            case "help":
            //                ShowTitleHelp();
            //                break;

            //            case "list":
            //                NebulaManager.Instance.StartCoroutine(CoListTitles());
            //                break;

            //            case "set":
            //            case "create":
            //            case "change":
            //            case "del":
            //                if (!CanManageTitles(PlayerControl.LocalPlayer))
            //                {
            //                    SendLocalMessage("权限不足");
            //                    return false;
            //                }
            //                if (scmd == "set")
            //                {
            //                    if (parts.Length < 3) { SendLocalMessage("用法错误"); break; }
            //                    if (!int.TryParse(parts[2], out int sid)) { SendLocalMessage("用法错误"); break; }
            //                    CoSetTitle(sid);
            //                }
            //                else if (scmd == "create")
            //                {
            //                    HandleCreateTitle(parts);
            //                }
            //                else if (scmd == "change")
            //                {
            //                    if (parts.Length < 4) { SendLocalMessage("用法错误"); break; }
            //                    if (!int.TryParse(parts[2], out int cid)) { SendLocalMessage("用法错误"); break; }
            //                    string newType = string.Join(" ", parts.Skip(3));
            //                    NebulaManager.Instance.StartCoroutine(CoChangeTitle(cid, newType));
            //                }
            //                else if (scmd == "del")
            //                {
            //                    if (parts.Length < 3) { SendLocalMessage("用法错误"); break; }
            //                    if (!int.TryParse(parts[2], out int did)) { SendLocalMessage("用法错误"); break; }
            //                    if (did == 0) { SendLocalMessage("不能删除 ID 0（不佩戴）"); break; }
            //                    NebulaManager.Instance.StartCoroutine(CoDeleteTitle(did));
            //                }
            //                break;

            //            default:
            //                SendLocalMessage($"未知子命令: {scmd}");
            //                break;
            //        }
            //return false;
            case "/hgithub":
            case "/hg":
                Application.OpenURL("https://github.com/Half-Sugar-s-Gift/Half-Sugars-Gift/");
                SendLocalMessage(Language.Translate("cmd.github.open"));
                __instance.freeChatField.Clear();
                return false;

        }
        return true;
    }

    static void ShowHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Half Sugar's Gift指令帮助");
        sb.AppendLine("====================");
        sb.AppendLine("<b>/Ghelp</b>  — 显示本帮助");
        sb.AppendLine("<b>/return</b>  <理由> — 踢出自己");
        sb.AppendLine("<b>/CheckBait</b>  — 击杀诱饵时提示");
        sb.AppendLine("<b>/perm</b>  — 权限查询");
        sb.AppendLine("====================");
        SendLocalMessage(sb.ToString());
    }

    static void ShowTitleHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== /hsgtitle 帮助 ===");
        sb.AppendLine("/hsgtitle help - 此帮助");
        sb.AppendLine("/hsgtitle list - 列出所有头衔");
        sb.AppendLine("/hsgtitle set <ID> - 佩戴头衔 (0=不佩戴)");
        sb.AppendLine("/hsgtitle create <名称> [性质] [玩家] - 创建头衔");
        sb.AppendLine("  性质: colourful / random / color;#RRGGBB");
        sb.AppendLine("  玩家: 仅开发者可指定");
        sb.AppendLine("/hsgtitle change <ID> <新性质> - 修改头衔性质");
        sb.AppendLine("/hsgtitle del <ID> - 删除头衔 (0不可删除)");
        SendLocalMessage(sb.ToString());
    }

    static bool _subBait = false;
    static void BaitCheck()
    {
        _settings.CheckBaitEnabled = !_settings.CheckBaitEnabled;
        SaveSettings();
        SendLocalMessage(_settings.CheckBaitEnabled ? "已启用诱饵提示助手" : "已禁用诱饵提示助手");
        if (!_subBait)
        {
            _subBait = true;
            GameOperatorManager.Instance.Subscribe<PlayerKillPlayerEvent>(ev =>
            {
                if (_settings.CheckBaitEnabled && ev.Murderer.AmOwner && ev.Dead.Role.Role.InternalName == "bait")
                    SendLocalMessage(Language.Translate("cmd.checkbait.congruatulation"));
            }, NebulaAPI.CurrentGame);
        }
    }

    static RemoteProcess<(byte playerId, string? reason)> RpcReturnRequest = new("ReturnRequest",
        (data, _) =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            var target = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(p => p.PlayerId == data.playerId);
            if (target == null) return;
            string msg = $" {target.Data.PlayerName}离开了房间";
            if (!string.IsNullOrEmpty(data.reason)) msg += $" Reason：{data.reason}";
            PlayerControl.LocalPlayer.RpcSendChat(msg);
            AmongUsClient.Instance.KickPlayer(GetClient(target)!.Id, false);
        });

    static RemoteProcess<(byte targt, byte senderId, string msg)> PriMsg = new("PrivateMessage",
        (data, _) =>
        {
            byte localId = PlayerControl.LocalPlayer.PlayerId;
            bool isSend = localId == data.senderId;
            bool isTarget = localId == data.targt;
            bool isDead = PlayerControl.LocalPlayer.Data.IsDead;
            if (!isSend && !isTarget && !isDead) return;

            PlayerControl sender = null, target = null;
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p.PlayerId == data.senderId) sender = p;
                if (p.PlayerId == data.targt) target = p;
                if (sender != null && target != null) break;
            }
            if (sender == null || target == null) return;

            bool isDevSender = IsDev(sender);
            string title = isDevSender ? "开发者" : "管理员";
            var pc = PlayerControl.LocalPlayer;
            string orig = pc.name;
            pc.SetName("System");
            string display = isDead && !isSend && !isTarget
                ? $"{title}-{sender.Data.PlayerName} 对 {target.Data.PlayerName} 悄悄说: <br>{data.msg}"
                : $"{title} -{sender.Data.PlayerName} 对你悄悄说: <br>{data.msg}";
            HudManager.Instance.Chat.AddChat(pc, display);
            pc.SetName(orig);
        });

    static void OnReceiveSetTitle(object[] args)
    {
        if (args.Length < 4)
        {
            HsgDebug.LogError($"[Title RPC] 参数个数不足，期望4，实际{args.Length}");
            return;
        }

        byte playerId = (byte)args[0];
        string titleName = (string)args[1];
        string type = (string)args[2];
        string colorCode = (string)args[3];

        if (string.IsNullOrEmpty(titleName))
        {
            _receivedTitleInfo.Remove(playerId);
        }
        else
        {
            var info = new TitleInfo
            {
                name = titleName,
                type = type,
                colorCode = colorCode
            };
            _receivedTitleInfo[playerId] = info;
        }
    }

    static IEnumerator CoSendRequest<T>(string endpoint, Action<T> onSuccess, Action<string> onError, string method = "GET", object data = null)
    {
        string url = $"https://hvtXsvc.top{endpoint}";

        UnityWebRequest req = new UnityWebRequest(url, method);
        try
        {
            if (data != null && (method == "POST" || method == "PUT"))
            {
                string json = JsonStructure.Serialize(data);
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();

            string fc = GetFriendCode(PlayerControl.LocalPlayer);
            if (!string.IsNullOrEmpty(fc))
                req.SetRequestHeader("X-Friend-Code", fc);

            if (PlayerControl.LocalPlayer?.Data != null)
            {
                string playerName = PlayerControl.LocalPlayer.Data.PlayerName;
                if (!string.IsNullOrEmpty(playerName))
                    req.SetRequestHeader("X-Player-Name", playerName);
            }

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string errMsg = $"{req.responseCode} {req.error}";
                onError?.Invoke(errMsg);
                yield break;
            }

            string responseText = req.downloadHandler.text;

            try
            {
                var result = JsonStructure.Deserialize<T>(responseText);
                if (result == null)
                {
                    onError?.Invoke("反序列化结果为 null");
                }
                else
                {
                    onSuccess?.Invoke(result);
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke($"反序列化失败: {ex.Message}");
            }
        }
        finally
        {
            req.Dispose();
        }
    }

    static IEnumerator CoListTitles()
    {
        if (_isLoading) { SendLocalMessage("正在加载..."); yield break; }
        _isLoading = true;
        yield return CoSendRequest<TitleListData>(
            "/title",
            onSuccess: resp =>
            {
                var titles = resp.titles;
                _cachedTitles = titles;
                var sb = new StringBuilder();
                sb.AppendLine($"=== 头衔列表 - 共 {titles.Count} 个 ===");
                sb.AppendLine("[0] 不佩戴");
                foreach (var t in titles)
                    sb.AppendLine($"[{t.id}] {t.name} ({t.type}) - {t.ownerName}");
                SendLocalMessage(sb.ToString());
            },
            onError: err => SendLocalMessage($"获取列表失败: {err}")
        );
        _isLoading = false;
    }

    //static void CoSetTitle(int titleId)
    //{
    //    SendLocalMessage("正在加载头衔列表，请稍后...");
    //    PlayerControl.LocalPlayer.RpcSetName(PlayerControl.LocalPlayer.Data.PlayerName);
    //    NebulaManager.Instance.StartCoroutine(CoListThenSet(titleId));
    //}

    static IEnumerator CoListThenSet(int titleId)
    {
        yield return CoListTitles();
        ApplyTitleLocally(titleId);
    }

    static void ApplyTitleLocally(int titleId)
    {
        PlayerControl local = PlayerControl.LocalPlayer;

        if (titleId == 0)
        {
            _playerTitleMap[local.PlayerId] = 0;
            _receivedTitleInfo.Remove(local.PlayerId);
            HostSendRpc.SendCustomRpc("HSG_SetTitle", local.PlayerId, "", "", "");
            SendLocalMessage("头衔已移除");
        }
        else
        {
            var title = _cachedTitles.FirstOrDefault(t => t.id == titleId);
            if (title == null)
            {
                SendLocalMessage($"头衔 ID {titleId} 不存在");
                return;
            }

            _playerTitleMap[local.PlayerId] = titleId;
            _receivedTitleInfo[local.PlayerId] = title;
            HostSendRpc.SendCustomRpc("HSG_SetTitle", local.PlayerId, title.name, title.type, title.colorCode ?? "");
            SendLocalMessage($"头衔已设置为 {title.name}");
            _myTitleId = titleId;
        }
    }

    //static void HandleCreateTitle(string[] args)
    //{
    //    List<string> parts = new List<string>(args.Skip(2));
    //    if (parts.Count == 0)
    //    {
    //        SendLocalMessage("用法错误");
    //        return;
    //    }

    //    string titleName = "";
    //    string type = "colourful";
    //    string colorCode = null;
    //    string targetPlayer = null;
    //    int idx = 0;

    //    while (idx < parts.Count)
    //    {
    //        string word = parts[idx];
    //        if (word == "colourful" || word == "random")
    //        {
    //            if (string.IsNullOrEmpty(titleName))
    //            {
    //                SendLocalMessage("用法错误");
    //                return;
    //            }
    //            type = word;
    //            idx++;
    //            break;
    //        }
    //        else if (word.StartsWith("color;"))
    //        {
    //            if (string.IsNullOrEmpty(titleName))
    //            {
    //                SendLocalMessage("用法错误");
    //                return;
    //            }
    //            type = "color";
    //            colorCode = word.Substring(6);
    //            if (string.IsNullOrEmpty(colorCode) || !colorCode.StartsWith("#") || colorCode.Length != 7)
    //            {
    //                SendLocalMessage("用法错误");
    //                return;
    //            }
    //            idx++;
    //            break;
    //        }
    //        else
    //        {
    //            if (!string.IsNullOrEmpty(titleName)) titleName += " ";
    //            titleName += word;
    //            idx++;
    //        }
    //    }

    //    if (idx < parts.Count && !string.IsNullOrEmpty(parts[idx]))
    //    {
    //        targetPlayer = string.Join(" ", parts.Skip(idx));
    //    }

    //    if (string.IsNullOrWhiteSpace(titleName))
    //    {
    //        SendLocalMessage("用法错误");
    //        return;
    //    }

    //    if (!string.IsNullOrEmpty(targetPlayer) && !IsDev(PlayerControl.LocalPlayer))
    //    {
    //        SendLocalMessage("只有开发者可以为其他玩家创建头衔");
    //        return;
    //    }

    //    if (!string.IsNullOrEmpty(targetPlayer))
    //    {
    //        PlayerControl targetPc = null;
    //        foreach (var p in PlayerControl.AllPlayerControls)
    //            if (p.Data.PlayerName.Contains(targetPlayer, StringComparison.OrdinalIgnoreCase)) { targetPc = p; break; }
    //        if (targetPc == null)
    //        {
    //            SendLocalMessage($"未找到玩家: {targetPlayer}");
    //            return;
    //        }
    //        if (!CanManageTitles(targetPc))
    //        {
    //            if (!IsDev(PlayerControl.LocalPlayer))
    //            {
    //                SendLocalMessage("目标玩家没有权限拥有头衔（需要赞助者/开发者/管理员）");
    //                return;
    //            }
    //            SendLocalMessage("目标玩家没有权限拥有头衔，但由于你是开发者，所以继续给予。");
    //        }
    //    }

    //    CreateTitleRequest data = new()
    //    {
    //        name = titleName,
    //        type = type,
    //        color = colorCode,
    //        targetFriendCode = !string.IsNullOrEmpty(targetPlayer) ? GetFriendCodeByName(targetPlayer) : null
    //    };
    //    NebulaManager.Instance.StartCoroutine(CoCreateTitle(data));
    //}

    //static IEnumerator CoCreateTitle(CreateTitleRequest data)
    //{
    //    yield return CoSendRequest<TitleInfo>(
    //        "/title",
    //        method: "POST",
    //        data: data,
    //        onSuccess: resp => {
    //            SendLocalMessage($"头衔 {resp.name} 创建成功 (ID: {resp.id})");
    //        },
    //        onError: err => SendLocalMessage($"创建失败: {err}")
    //    );
    //}

    //static IEnumerator CoChangeTitle(int id, string newType)
    //{
    //    string colorCode = null;
    //    if (newType.StartsWith("color;"))
    //    {
    //        colorCode = newType.Substring(6);
    //        if (string.IsNullOrEmpty(colorCode) || !colorCode.StartsWith("#") || colorCode.Length != 7)
    //        {
    //            SendLocalMessage("用法错误");
    //            yield break;
    //        }
    //        newType = "color";
    //    }
    //    else if (newType != "colourful" && newType != "random")
    //    {
    //        SendLocalMessage("用法错误");
    //        yield break;
    //    }

    //    var data = new { type = newType, color = colorCode };
    //    yield return CoSendRequest<TitleInfo>(
    //        $"/title/{id}",
    //        method: "PUT",
    //        data: data,
    //        onSuccess: resp => {
    //            SendLocalMessage($"头衔 {id} 已修改为 {newType}");
    //            PlayerControl local = PlayerControl.LocalPlayer;
    //            if (_playerTitleMap.TryGetValue(local.PlayerId, out int currentId) && currentId == id)
    //            {
    //                _receivedTitleInfo[local.PlayerId] = resp;
    //                HostSendRpc.SendCustomRpc("HSG_SetTitle", local.PlayerId, resp.name, resp.type, resp.colorCode ?? "");
    //            }
    //        },
    //        onError: err => SendLocalMessage($"修改失败: {err}")
    //    );
    //}

    //static IEnumerator CoDeleteTitle(int id)
    //{
    //    yield return CoSendRequest<DeleteResponse>(
    //        $"/title/{id}",
    //        method: "DELETE",
    //        onSuccess: resp => {
    //            if (resp.success)
    //                SendLocalMessage($"头衔 {id} 已删除");
    //            else
    //                SendLocalMessage($"删除失败");
    //        },
    //        onError: err => SendLocalMessage($"请求失败: {err}")
    //    );
    //}

    //static string GetFriendCodeByName(string playerName)
    //{
    //    PlayerControl target = null;
    //    foreach (var p in PlayerControl.AllPlayerControls)
    //        if (p.Data.PlayerName.Contains(playerName, StringComparison.OrdinalIgnoreCase)) { target = p; break; }
    //    return target == null ? null : GetFriendCode(target);
    //}

    //static void OnDecorateName(PlayerDecorateNameEvent ev)
    //{
    //    var player = ev.Player;
    //    if (player == null) return;

    //    if (!_receivedTitleInfo.TryGetValue(player.PlayerId, out var title)) return;
    //    if (string.IsNullOrEmpty(title.name)) return;

    //    string displayName = $"[{title.name}]";
    //    if (displayName.Length > 10)
    //        displayName = displayName.Substring(0, 10);
    //    string coloredTitle = title.type switch
    //    {
    //        "colourful" => ColorHelper.Create(displayName, 0.35f, 0.06f),
    //        "random" => ColorHelper.CreateRandom(displayName),
    //        "color" when !string.IsNullOrEmpty(title.colorCode) =>
    //            $"<color={title.colorCode}>{displayName}</color>",
    //        _ => displayName
    //    };
    //    ev.Name = $"{coloredTitle} {ev.Name}";
    //}
}
#endregion

#region JSON
public class CommandSettings
{
    [JsonSerializableField(true, false)]
    public bool CheckBaitEnabled = true;

    [JsonSerializableField(true, false)]
    public bool SmyStatus = false;

    [JsonSerializableField(true, false)]
    public bool CatMode = false;
}

[Serializable]
public class TitleInfo
{
    [JsonSerializableField(true, false)] public int id;
    [JsonSerializableField(true, false)] public string name;
    [JsonSerializableField(true, false)] public string type;
    [JsonSerializableField(true, false)] public string colorCode;
    [JsonSerializableField(true, false)] public string ownerFriendCode;
    [JsonSerializableField(true, false)] public string ownerName;
}

[Serializable]
public class TitleListData
{
    [JsonSerializableField(true, false)] public List<TitleInfo> titles;
}

[Serializable]
public class DeleteResponse
{
    [JsonSerializableField(true, false)] public bool success;
}

[Serializable]
public class CreateTitleRequest
{
    [JsonSerializableField(true, false)] public string name;
    [JsonSerializableField(true, false)] public string type;
    [JsonSerializableField(true, false)] public string color;
    [JsonSerializableField(true, false)] public string targetFriendCode;
}
#endregion

#region HostSendRpc
public static class HostSendRpc
{
    static Dictionary<int, Action<object[]>> _actions = new Dictionary<int, Action<object[]>>();
    static int _nextId = 1;
    static RemoteProcess<(int opId, byte[] data)> _requestRpc = new("HostSendRpcRequest", (msg, _) =>
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (_actions.TryGetValue(msg.opId, out var action))
            action(Deserialize(msg.data));
    });
    static int Register(Action<object[]> action)
    {
        int id = _nextId++;
        _actions[id] = action;
        return id;
    }
    static void Execute(int opId, params object[] args)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            if (_actions.TryGetValue(opId, out var action))
                action(args);
        }
        else
        {
            _requestRpc.Invoke((opId, Serialize(args)));
        }
    }
    static byte[] Serialize(object[] args)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(args.Length);
        foreach (var arg in args)
        {
            if (arg is int v) { bw.Write((byte)0); bw.Write(v); }
            else if (arg is float vf) { bw.Write((byte)1); bw.Write(vf); }
            else if (arg is byte vb) { bw.Write((byte)2); bw.Write(vb); }
            else if (arg is string vs) { bw.Write((byte)3); bw.Write(vs); }
            else if (arg is GamePlayer p) { bw.Write((byte)4); bw.Write(p.PlayerId); }
            else if (arg is bool vb2) { bw.Write((byte)5); bw.Write(vb2); }
            else if (arg is Vector2 v2) { bw.Write((byte)6); bw.Write(v2.x); bw.Write(v2.y); }
            else throw new ArgumentException($"不支持的类型: {arg.GetType()}");
        }
        return ms.ToArray();
    }

    static object[] Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        int count = br.ReadInt32();
        var args = new object[count];
        for (int i = 0; i < count; i++)
        {
            byte t = br.ReadByte();
            switch (t)
            {
                case 0: args[i] = br.ReadInt32(); break;
                case 1: args[i] = br.ReadSingle(); break;
                case 2: args[i] = br.ReadByte(); break;
                case 3: args[i] = br.ReadString(); break;
                case 4: args[i] = GamePlayer.GetPlayer(br.ReadByte()); break;
                case 5: args[i] = br.ReadBoolean(); break;
                case 6: args[i] = new Vector2(br.ReadSingle(), br.ReadSingle()); break;
                default: throw new Exception("未知类型");
            }
        }
        return args;
    }
    static int _setSizeYId, _setSizeXId, _setColorId;
    static Dictionary<string, int> _customRpcIds = new Dictionary<string, int>();

    static HostSendRpc()
    {
        _setSizeYId = Register(args =>
        {
            var target = (GamePlayer)args[0];
            float y = (float)args[1];
            target.GainSizeAttribute(new Vector2(1f, y), 1000f, true, 50, "SizeTag");
        });
        _setSizeXId = Register(args =>
        {
            var target = (GamePlayer)args[0];
            float x = (float)args[1];
            target.GainSizeAttribute(new Vector2(x, 1f), 1000f, true, 50, "SizeTag");
        });
        _setColorId = Register(args =>
        {
            var target = (GamePlayer)args[0];
            byte colorId = (byte)args[1];
            _setColorId = Register(args =>
            {
                var target = (GamePlayer)args[0];
                byte colorId = (byte)args[1];
                PlayerControl pc = null;
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (p.PlayerId == target.PlayerId)
                    {
                        pc = p;
                        break;
                    }
                }
                pc?.RpcSetColor(colorId);
            });
        });
    }
    public static void SetSizeY(GamePlayer player, float y) => Execute(_setSizeYId, player, y);
    public static void SetSizeX(GamePlayer player, float x) => Execute(_setSizeXId, player, x);
    public static void SetColor(GamePlayer player, byte colorId) => Execute(_setColorId, player, colorId);
    public static void SetSizeY(IEnumerable<GamePlayer> players, float y)
    {
        foreach (var player in players)
        {
            SetSizeY(player, y);
        }
    }
    public static void RegisterCustomRpc(string name, Action<object[]> rpcAction)
    {
        if (_customRpcIds.ContainsKey(name)) return;
        _customRpcIds[name] = Register(rpcAction);
    }
    public static void SendCustomRpc(string name, params object[] args)
    {
        if (_customRpcIds.TryGetValue(name, out int id))
            Execute(id, args);
        else
            HsgDebug.Log($"未注册的 RPC 名称: {name}");
    }
}
#endregion

#region AudioHelper
[NebulaRPCHolder]
public static class AudioHelper
{
    private static Dictionary<string, AudioClip> _cache = new();

    private static AudioClip? LoadWav(string resourcePath)
    {
        if (_cache.TryGetValue(resourcePath, out var cached))
            return cached;

        var resource = NebulaAPI.AddonAsset.GetResource(resourcePath);
        if (resource == null) return null;

        byte[] bytes;
        using (var stream = resource.AsStream())
        {
            if (stream == null) return null;
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                bytes = ms.ToArray();
            }
        }

        var clip = ParseWav(bytes, Path.GetFileNameWithoutExtension(resourcePath));
        if (clip != null)
            _cache[resourcePath] = clip;
        return clip;
    }

    private static AudioClip? ParseWav(byte[] bytes, string clipName)
    {
        if (bytes.Length < 44) return null;
        if (Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF") return null;
        if (Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE") return null;

        int fmtPos = -1, dataPos = -1, dataSize = 0;
        int pos = 12;
        while (pos + 8 <= bytes.Length)
        {
            string id = Encoding.ASCII.GetString(bytes, pos, 4);
            int size = BitConverter.ToInt32(bytes, pos + 4);
            if (id == "fmt ")
            {
                fmtPos = pos + 8;
            }
            else if (id == "data")
            {
                dataPos = pos + 8;
                dataSize = size;
                break;
            }
            pos += 8 + size;
            if ((size & 1) == 1) pos++;
        }

        if (fmtPos < 0 || dataPos < 0) return null;
        int audioFormat = BitConverter.ToInt16(bytes, fmtPos);
        int channels = BitConverter.ToInt16(bytes, fmtPos + 2);
        int sampleRate = BitConverter.ToInt32(bytes, fmtPos + 4);
        int bitsPerSample = BitConverter.ToInt16(bytes, fmtPos + 14);
        if (audioFormat != 1 || bitsPerSample != 16) return null;

        int bytesPerSample = 2;
        int frameSize = bytesPerSample * channels;
        int frameCount = dataSize / frameSize;
        if (frameCount <= 0) return null;

        int totalSamples = frameCount * channels;
        float[] samples = new float[totalSamples];
        int p = dataPos;
        float inv = 1f / 32768f;
        for (int i = 0; i < totalSamples; i++)
        {
            short s = (short)(bytes[p] | (bytes[p + 1] << 8));
            samples[i] = s * inv;
            p += 2;
        }

        var clip = AudioClip.Create(clipName, frameCount, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    [NebulaRPC]
    public static void RpcPlayGlobal(string resourcePath, float volume = 1f)
    {
        var clip = LoadWav(resourcePath);
        if (clip == null) return;
        SoundManager.Instance.PlaySound(clip, false, volume, null);
    }

    [NebulaRPC]
    public static void RpcPlayPositional(string resourcePath, Vector2 position, float volume = 1f)
    {
        var clip = LoadWav(resourcePath);
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    [NebulaRPC]
    public static void RpcPlayPrivate(string resourcePath, float volume = 1f)
    {
        if (PlayerControl.LocalPlayer == null) return;
        var clip = LoadWav(resourcePath);
        if (clip == null) return;
        SoundManager.Instance.PlaySound(clip, false, volume, null);
    }
}
#endregion

#region ColorHelper
public static class ColorHelper
{
    /// <summary>
    /// 生成彩虹渐变文本
    /// </summary>
    /// <param name="suffix">要追加的文字（例如 "GOD"）</param>
    /// <param name="hueSpeed">色相流动速度（每秒转几圈）</param>
    /// <param name="hueStep">每个字符的色相增量</param>
    /// <param name="saturation">饱和度 (0~1)</param>
    /// <param name="value">明度 (0~1)</param>
    /// <param name="time">时间源（默认 Time.time）</param>
    /// <param name="offset">色相偏移量（用于区分不同玩家）</param>
    /// <returns>带彩虹标签的字符串</returns>
    public static string Create(string suffix, float hueSpeed = 0.35f, float hueStep = 0.06f,
        float saturation = 0.9f, float value = 1f, float? time = null, float offset = 0f)
    {
        if (string.IsNullOrEmpty(suffix)) return "";

        float t = time ?? Time.time;
        float baseHue = (t * hueSpeed + offset) % 1f;
        var sb = new StringBuilder(suffix.Length * 24);

        for (int i = 0; i < suffix.Length; i++)
        {
            char c = suffix[i];
            if (c == ' ')
            {
                sb.Append(' ');
                continue;
            }
            float hue = (baseHue + i * hueStep) % 1f;
            Color color = Color.HSVToRGB(hue, saturation, value);
            sb.Append($"<color=#{ColorToHex(color)}>{c}</color>");
        }
        return sb.ToString();
    }

    public static string ColorToHex(Color color)
    {
        byte r = (byte)(color.r * 255);
        byte g = (byte)(color.g * 255);
        byte b = (byte)(color.b * 255);
        byte a = (byte)(color.a * 255);
        return $"{r:X2}{g:X2}{b:X2}{a:X2}";
    }
    public static string ColorToHexRGB(Color color)
    {
        byte r = (byte)(Mathf.Clamp01(color.r) * 255);
        byte g = (byte)(Mathf.Clamp01(color.g) * 255);
        byte b = (byte)(Mathf.Clamp01(color.b) * 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
    /// <summary>
    /// 生成彩虹随机色文本（每个字符独立随机色相）
    /// </summary>
    /// <param name="suffix">要追加的文字</param>
    /// <param name="saturation">饱和度 (0~1)</param>
    /// <param name="value">明度 (0~1)</param>
    /// <param name="time">时间源，用作随机种子（若为 null 则使用系统时间，每次调用结果不同）</param>
    /// <returns>带随机彩虹标签的字符串</returns>
    public static string CreateRandom(string suffix, float saturation = 0.9f, float value = 1f, float? time = null)
    {
        if (string.IsNullOrEmpty(suffix)) return "";
        int seed = time.HasValue ? (int)(time.Value * 10000) : Environment.TickCount;
        var rand = new System.Random(seed);
        var sb = new StringBuilder(suffix.Length * 24);

        for (int i = 0; i < suffix.Length; i++)
        {
            char c = suffix[i];
            if (c == ' ')
            {
                sb.Append(' ');
                continue;
            }
            // 每个字符独立随机色相（0~1）
            float hue = (float)rand.NextDouble();
            Color color = Color.HSVToRGB(hue, saturation, value);
            sb.Append($"<color=#{ColorToHex(color)}>{c}</color>");
        }
        return sb.ToString();
    }
}
#endregion




