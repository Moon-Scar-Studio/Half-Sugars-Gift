using System;
using System.Collections.Generic;
using UnityEngine;
using Virial.Game;

namespace NebulaN.Core;

public static class KarmaVisualManager
{
    private static readonly Dictionary<byte, GameObject> VisualObjects = new();
    private static readonly Dictionary<byte, Sprite[]> Cache = new();

    public static void ShowKarmaVisual(GamePlayer target)
    {
        if (target == null) return;
        if (VisualObjects.ContainsKey(target.PlayerId)) return;
        var obj = new GameObject("KarmaVisual_" + target.PlayerId);
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 1000;
        var frames = LoadFrames();
        if (frames.Length == 0) { UnityEngine.Object.Destroy(obj); return; }
        Cache[target.PlayerId] = frames;
        var animator = obj.AddComponent<KarmaVisualAnimator>();
        animator.Init(target, renderer, frames);
        VisualObjects.Add(target.PlayerId, obj);
    }

    public static void RemoveKarmaVisual(byte id)
    {
        if (!VisualObjects.TryGetValue(id, out var obj)) return;
        UnityEngine.Object.Destroy(obj);
        VisualObjects.Remove(id);
        Cache.Remove(id);
    }

    public static void Clear()
    {
        foreach (var obj in VisualObjects.Values)
            if (obj != null) UnityEngine.Object.Destroy(obj);
        VisualObjects.Clear();
        Cache.Clear();
    }

    private static Sprite[] LoadFrames()
    {
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
        return result.ToArray();
    }
}
public class KarmaVisualAnimator : MonoBehaviour
{
    private GamePlayer target;
    private SpriteRenderer renderer;
    private Sprite[] frames;
    private float timer;
    private int frame;
    private const float FrameTime = 0.12f;
    private float floatOffset;

    public void Init(GamePlayer target, SpriteRenderer renderer, Sprite[] frames)
    {
        this.target = target;
        this.renderer = renderer;
        this.frames = frames;
        renderer.sprite = frames[0];
        floatOffset = UnityEngine.Random.Range(0f, 10f);
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }
        var pc = target.VanillaPlayer;
        if (pc == null) return;
        transform.position = pc.transform.position + new Vector3(0, 0.7f + Mathf.Sin(Time.time * 2f + floatOffset) * 0.05f, -1);
        timer += Time.deltaTime;
        if (timer >= FrameTime)
        {
            timer = 0;
            frame++;
            if (frame >= frames.Length) frame = 0;
            renderer.sprite = frames[frame];
        }
        renderer.color = new Color(1f, 1f, 1f, 0.75f + Mathf.Sin(Time.time * 2f) * 0.15f);
    }
}