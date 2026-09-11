namespace HalfSugarGift.GameMode.PropHunt;

/// <summary>
/// 道具躲猫猫的键盘操作。对应原版 Patches.PlayerInputControlPatch。
///
/// 操作与原版保持一致：
///   R      变成最近的道具
///   Shift  按住时用方向键微调道具位置（本体不动），松开时广播位置
///   C      变回船员
///
/// 补丁由 PropHuntHarmony.Apply 手动挂载，不使用 [HarmonyPatch] 特性，
/// 原因见 PropHuntHarmony 的注释。
/// </summary>
public static class PropHuntInput
{
    /// <summary>原版 Rewired 的方向键 action id。</summary>
    private const int ActionRight = 40;
    private const int ActionLeft = 39;
    private const int ActionUp = 44;
    private const int ActionDown = 42;

    public static bool KeyboardUpdatePrefix(KeyboardJoystick __instance)
    {
        if (!PropHuntGameModeRegistration.IsPropHuntActive) return true;

        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null || local.Data.Role == null) return true;

        // 抓捕者没有道具形态。
        if (local.Data.Role.IsImpostor) return true;

        // 死亡玩家不参与。
        if (local.Data.IsDead) return true;

        if (KeyboardJoystick.player == null) return true;

        // 会议中不处理。
        if (MeetingHud.Instance != null) return true;

        // --- C：变回船员 ---
        if (Input.GetKeyDown(KeyCode.C) && PropManager.IsDisguised(local.PlayerId))
        {
            PropManager.RevertLocal();
            local.Visible = true;
        }

        // --- R：变成最近的道具 ---
        if (Input.GetKeyDown(KeyCode.R))
        {
            PropManager.TryDisguiseLocal();
        }

        // --- Shift：微调道具位置 ---
        var renderer = PropManager.Get(local.PlayerId);
        if (renderer == null) return true;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            // 只有配合方向键才算微调道具：防止输入法的 Shift（中英切换）把本体移动误锁死
            var direction = Vector2.zero;
            if (KeyboardJoystick.player.GetButton(ActionRight)) direction.x += 1f;
            if (KeyboardJoystick.player.GetButton(ActionLeft)) direction.x -= 1f;
            if (KeyboardJoystick.player.GetButton(ActionUp)) direction.y += 1f;
            if (KeyboardJoystick.player.GetButton(ActionDown)) direction.y -= 1f;

            if (direction == Vector2.zero) return true;

            // 阻断本体移动
            __instance.del = Vector2.zero;

            float speed = PropHuntSettings.PropMoveSpeed.GetValue();
            var current = renderer.transform.localPosition;
            var next = new Vector3(
                current.x + direction.x * speed * Time.deltaTime,
                current.y + direction.y * speed * Time.deltaTime,
                PropManager.PropZOffset);

            if (Vector2.Distance(Vector2.zero, next) < PropHuntSettings.MaxPropDistance.GetValue())
            {
                renderer.transform.localPosition = next;
            }

            // 返回 false：这一帧完全接管输入，本体不会移动。
            return false;
        }

        if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            PropManager.BroadcastLocalPropPosition();
        }

        return true;
    }
}
