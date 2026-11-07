using System;
using System.Collections.Generic;
using UnityEngine;
using Virial.Game;
using NebulaN.Roles.Modifier;

namespace NebulaN.Core;

public static class KarmaVisualManager
{
    private static readonly Dictionary<byte, GameObject> VisualObjects = new();
    private static Sprite[] cachedFrames = null;

    public static void ShowKarmaVisual(GamePlayer target)
    {
        if (target == null) return;

        if (VisualObjects.TryGetValue(target.PlayerId, out var existing))
        {
            // 场景切换后 Unity 对象会变成 null，但字典里的 key 还在；这里顺手清掉旧条目。
            if (existing != null) return;
            VisualObjects.Remove(target.PlayerId);
        }

        var obj = new GameObject("KarmaVisual_" + target.PlayerId);
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 1000;

        var vanilla = target.VanillaPlayer;
        if (vanilla != null)
        {
            // 对齐到身体精灵的实际位置，而不是 PlayerControl 根节点；不同体型/皮肤下根节点会有偏移。
            var initialBody = vanilla.cosmetics?.currentBodySprite?.BodySprite;
            var initialPos = initialBody != null ? initialBody.transform.position : vanilla.transform.position;
            obj.transform.position = new Vector3(initialPos.x, initialPos.y, -1f);
        }

        var frames = LoadFrames();
        if (frames.Length == 0)
        {
            UnityEngine.Object.Destroy(obj);
            return;
        }

        var animator = obj.AddComponent<KarmaVisualAnimator>();
        animator.Init(target, renderer, frames);
        VisualObjects.Add(target.PlayerId, obj);
    }

    public static void RemoveKarmaVisual(byte id)
    {
        if (!VisualObjects.TryGetValue(id, out var obj)) return;
        if (obj != null) UnityEngine.Object.Destroy(obj);
        VisualObjects.Remove(id);
    }

    public static void Clear()
    {
        foreach (var obj in VisualObjects.Values)
            if (obj != null) UnityEngine.Object.Destroy(obj);
        VisualObjects.Clear();
        // 场景切换时贴图可能被卸载，缓存也要跟着失效，下局重新加载。
        cachedFrames = null;
    }

    private static Sprite[] LoadFrames()
    {
        if (cachedFrames != null) return cachedFrames;

        List<Sprite> result = new();
        int index = 0;
        while (true)
        {
            var image = NebulaAPI.AddonAsset.GetResource($"KarmaVisual/{index}.png")?.AsImage(100f);
            if (image == null) break;
            var sprite = image.GetSprite();
            if (sprite == null) break;
            result.Add(sprite);
            index++;
        }

        cachedFrames = result.ToArray();
        if (cachedFrames.Length == 0)
            HsgDebug.LogWarning("[HSG] KarmaVisual 素材缺失：Resources/KarmaVisual/0.png");
        return cachedFrames;
    }
}

/// <summary>
/// 自定义 MonoBehaviour 在 IL2CPP 下必须先 RegisterTypeInIl2Cpp 并提供 IntPtr 构造函数，
/// 否则 AddComponent 会在运行时失败（参考 CowboyDuelMinigame 的写法）。
/// 素材位于 Resources/KarmaVisual/*.png，由 LoadFrames 按序号循环加载。
/// </summary>
public class KarmaVisualAnimator : MonoBehaviour
{
    static KarmaVisualAnimator() => Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<KarmaVisualAnimator>();
    public KarmaVisualAnimator(IntPtr ptr) : base(ptr) { }
    public KarmaVisualAnimator() : base(Il2CppInterop.Runtime.Injection.ClassInjector.DerivedConstructorPointer<KarmaVisualAnimator>())
    {
        Il2CppInterop.Runtime.Injection.ClassInjector.DerivedConstructorBody(this);
    }

    private GamePlayer target;
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float timer;
    private int frame;
    private const float FrameTime = 0.12f;
    private byte playerId;
    private float elapsed;

    public void Init(GamePlayer target, SpriteRenderer renderer, Sprite[] frames)
    {
        this.target = target;
        this.playerId = target.PlayerId;
        this.spriteRenderer = renderer;
        this.frames = frames;
        renderer.sprite = frames[0];
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        // 目标死亡或 Karma 被移除后，挂在上面的标记也要跟着消失。
        // 2s 宽限：AddModifier 的本地 RPC 有时会在会议结束的 RPC 段里延后执行。
        if (target == null || target.IsDead ||
            (elapsed > 2f && !target.TryGetModifier<Karma.Instance>(out _)))
        {
            KarmaVisualManager.RemoveKarmaVisual(playerId);
            return;
        }

        var pc = target.VanillaPlayer;
        if (pc == null) return;

        // 跟随 AU 原版视野：bodySprite 的 alpha 由游戏/Nebula 根据墙体、灯光、隐身状态设置，
        // 这里直接复用，特效就不会透过墙或在黑暗里露出来。
        float visibility = 1f;
        var body = pc.cosmetics?.currentBodySprite?.BodySprite;
        if (body != null) visibility = body.color.a;
        if (!pc.Visible) visibility = 0f;

        // 固定在玩家身体上，不做上下浮动。
        var basePos = body != null ? body.transform.position : pc.transform.position;
        transform.position = new Vector3(basePos.x, basePos.y, -1f);
        timer += Time.deltaTime;
        if (timer >= FrameTime)
        {
            timer = 0;
            frame++;
            if (frame >= frames.Length) frame = 0;
            spriteRenderer.sprite = frames[frame];
        }
        float pulse = 0.8f + Mathf.Sin(Time.time * 2f) * 0.1f;
        spriteRenderer.color = new Color(1f, 1f, 1f, visibility * pulse);
    }
}
