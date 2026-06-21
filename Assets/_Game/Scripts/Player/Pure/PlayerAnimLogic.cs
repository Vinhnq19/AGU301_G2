using UnityEngine;

public enum FacingDir { Up, Down, Left, Right }

public enum AnimState { Idle, Run, Foraging }

/// <summary>
/// Pure, deterministic helpers for PlayerAnimation. No MonoBehaviour state,
/// fully unit-testable. Mirrors the ShopPure / ShopMath pattern.
/// </summary>
public static class PlayerAnimLogic
{
    /// <summary>
    /// Picks the cardinal facing from velocity. When effectively still
    /// (sqrMagnitude &lt;= thresholdSq) the current facing is kept.
    /// Vertical wins ties (|vy| &gt;= |vx|).
    /// </summary>
    public static FacingDir ComputeFacing(Vector2 velocity, float thresholdSq, FacingDir current)
    {
        if (velocity.sqrMagnitude <= thresholdSq)
        {
            return current;
        }

        if (Mathf.Abs(velocity.y) >= Mathf.Abs(velocity.x))
        {
            return velocity.y > 0f ? FacingDir.Up : FacingDir.Down;
        }

        return velocity.x > 0f ? FacingDir.Right : FacingDir.Left;
    }

    /// <summary>
    /// Resolves the animation state. Foraging always wins; otherwise Run when
    /// moving, Idle when still.
    /// </summary>
    public static AnimState ComputeState(Vector2 velocity, float thresholdSq, bool foraging)
    {
        if (foraging)
        {
            return AnimState.Foraging;
        }

        return velocity.sqrMagnitude > thresholdSq ? AnimState.Run : AnimState.Idle;
    }

    /// <summary>
    /// Maps accumulated play time to a looping frame index. Returns 0 for
    /// non-positive length or secondsPerFrame (defensive against unassigned arrays).
    /// </summary>
    public static int FrameAtTime(float elapsed, float secondsPerFrame, int length)
    {
        if (length <= 0 || secondsPerFrame <= 0f)
        {
            return 0;
        }

        int frame = Mathf.FloorToInt(elapsed / secondsPerFrame) % length;
        return frame < 0 ? frame + length : frame;
    }
}
