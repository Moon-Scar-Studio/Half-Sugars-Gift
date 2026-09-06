namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>
/// 道具形态的持有与同步。
/// 对应原版 PropHunt 的 PropManager.cs + RPCHandler.cs 中的 PropSync / PropPos / Revert 三条 RPC。
///
/// 实现方式与原版一致：给每个 PlayerControl 挂一个子 SpriteRenderer，
/// 变身时把最近控制台的 sprite 复制过去并隐藏本体，变回时清空 sprite 并恢复本体。
/// </summary>
[NebulaRPCHolder]
public static class PropManager
{
    /// <summary>原版的道具缩放系数，用于抵消玩家本体的缩放。</summary>
    public const float PropScaleFactor = 1.429f;

    /// <summary>道具渲染器的 Z 偏移，保证盖在玩家本体之上。</summary>
    public const float PropZOffset = -3f;

    /// <summary>玩家 → 道具渲染器。</summary>
    private static readonly Dictionary<byte, SpriteRenderer> PlayerToProp = new();

    #region 生命周期

    /// <summary>给玩家挂载道具渲染器。由 PlayerControl.Start 的补丁调用。</summary>
    public static void Attach(PlayerControl player)
    {
        if (player == null) return;
        if (PlayerToProp.TryGetValue(player.PlayerId, out var existing) && existing != null) return;

        var propObj = new GameObject("HsgProp") { layer = 11 };
        var renderer = propObj.AddComponent<SpriteRenderer>();
        propObj.transform.SetParent(player.transform);
        propObj.transform.localScale = Vector2.one;
        propObj.transform.localPosition = new Vector3(0f, 0f, PropZOffset);

        PlayerToProp[player.PlayerId] = renderer;
    }

    /// <summary>移除某个玩家的道具渲染器。</summary>
    public static void Detach(byte playerId)
    {
        if (!PlayerToProp.TryGetValue(playerId, out var renderer)) return;
        if (renderer != null) UnityEngine.Object.Destroy(renderer.gameObject);
        PlayerToProp.Remove(playerId);
    }

    /// <summary>清空全部道具状态。开局与退出对局时调用。</summary>
    public static void Clear()
    {
        foreach (var renderer in PlayerToProp.Values)
        {
            if (renderer != null) UnityEngine.Object.Destroy(renderer.gameObject);
        }
        PlayerToProp.Clear();
    }

    #endregion

    #region 查询

    public static SpriteRenderer? Get(byte playerId)
        => PlayerToProp.TryGetValue(playerId, out var renderer) && renderer != null ? renderer : null;

    public static SpriteRenderer? Get(PlayerControl? player)
        => player == null ? null : Get(player.PlayerId);

    /// <summary>该玩家当前是否正伪装成道具。</summary>
    public static bool IsDisguised(byte playerId)
    {
        var renderer = Get(playerId);
        return renderer != null && renderer.sprite != null;
    }

    public static bool IsDisguised(PlayerControl? player)
        => player != null && IsDisguised(player.PlayerId);

    #endregion

    #region RPC

    /// <summary>
    /// 变身为指定索引的控制台。
    /// 原版用 string 传索引（RPCPropSync(player, i + "")），这里直接用 int，语义不变。
    /// </summary>
    public static readonly RemoteProcess<(byte playerId, int consoleIndex)> RpcPropSync =
        new("HSG_PropHuntPropSync", (message, _) =>
        {
            var renderer = Get(message.playerId);
            if (renderer == null) return;

            var ship = ShipStatus.Instance;
            if (ship == null || ship.AllConsoles == null) return;
            if (message.consoleIndex < 0 || message.consoleIndex >= ship.AllConsoles.Length) return;

            var console = ship.AllConsoles[message.consoleIndex];
            if (console == null) return;

            var consoleObject = console.gameObject;
            var consoleRenderer = consoleObject.GetComponent<SpriteRenderer>();
            if (consoleRenderer == null || consoleRenderer.sprite == null) return;

            renderer.transform.localScale = consoleObject.transform.lossyScale * PropScaleFactor;
            renderer.transform.localPosition = new Vector3(0f, 0f, PropZOffset);
            renderer.sprite = consoleRenderer.sprite;

            SetBodyVisible(message.playerId, false);
        });

    /// <summary>同步道具的微调位置（松开 Shift 时广播一次，与原版一致）。</summary>
    public static readonly RemoteProcess<(byte playerId, float x, float y)> RpcPropPos =
        new("HSG_PropHuntPropPos", (message, _) =>
        {
            var renderer = Get(message.playerId);
            if (renderer == null) return;
            renderer.transform.localPosition = new Vector3(message.x, message.y, PropZOffset);
        });

    /// <summary>变回船员形态。</summary>
    public static readonly RemoteProcess<byte> RpcRevert =
        new("HSG_PropHuntRevert", (playerId, _) =>
        {
            var renderer = Get(playerId);
            if (renderer == null) return;

            renderer.sprite = null;
            renderer.transform.localPosition = new Vector3(0f, 0f, PropZOffset);
            SetBodyVisible(playerId, true);
        });

    private static void SetBodyVisible(byte playerId, bool visible)
    {
        var control = Helpers.GetPlayer(playerId);
        if (control != null) control.Visible = visible;
    }

    #endregion

    #region 本地操作（供输入补丁调用）

    /// <summary>尝试把本地玩家变成最近的控制台。返回是否成功。</summary>
    public static bool TryDisguiseLocal()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null) return false;

        var ship = ShipStatus.Instance;
        if (ship == null || ship.AllConsoles == null) return false;

        var closest = PropHuntUtility.FindClosestConsole(
            local.gameObject, PropHuntSettings.PropSearchRadius.GetValue());
        if (closest == null) return false;

        var console = closest.GetComponent<Console>();
        if (console == null) return false;

        for (int i = 0; i < ship.AllConsoles.Length; i++)
        {
            if (ship.AllConsoles[i] != console) continue;
            RpcPropSync.Invoke((local.PlayerId, i));
            return true;
        }
        return false;
    }

    /// <summary>把本地玩家变回船员形态。</summary>
    public static void RevertLocal()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null) return;
        if (!IsDisguised(local.PlayerId)) return;
        RpcRevert.Invoke(local.PlayerId);
    }

    /// <summary>广播本地道具的当前微调位置。</summary>
    public static void BroadcastLocalPropPosition()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null) return;
        var renderer = Get(local.PlayerId);
        if (renderer == null) return;

        var position = renderer.transform.localPosition;
        RpcPropPos.Invoke((local.PlayerId, position.x, position.y));
    }

    #endregion
}

/// <summary>道具躲猫猫用到的通用工具。对应原版的 Utility.cs。</summary>
public static class PropHuntUtility
{
    /// <summary>在给定半径内寻找最近的控制台。原版 Utility.FindClosestConsole。</summary>
    public static GameObject? FindClosestConsole(GameObject origin, float radius)
    {
        if (origin == null) return null;

        Collider2D? best = null;
        float bestDistance = float.MaxValue;

        foreach (var collider in Physics2D.OverlapCircleAll(origin.transform.position, radius))
        {
            if (collider == null) continue;
            if (collider.GetComponent<Console>() == null) continue;

            float distance = Vector2.Distance(origin.transform.position, collider.transform.position);
            if (distance >= bestDistance) continue;

            best = collider;
            bestDistance = distance;
        }

        return best != null ? best.gameObject : null;
    }

    /// <summary>击空时的红屏 + 破坏音效。原版 Utility.KillConsoleAnimation。</summary>
    public static void PlayMissEffect()
    {
        if (!Constants.ShouldPlaySfx()) return;

        var ship = ShipStatus.Instance;
        if (ship != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound(ship.SabotageSound, false, 0.8f, null);
        }

        PatchManager.ShowScreenOverlay(new Color(1f, 0f, 0f, 0.375f), 0.5f);
    }
}
