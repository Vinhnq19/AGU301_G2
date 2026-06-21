using NUnit.Framework;
using UnityEngine;

public class PlayerAnimLogicTests
{
    private const float T = 0.05f;       // movement threshold
    private const float T2 = T * T;      // threshold squared

    // --- ComputeFacing ---

    [Test] public void StillKeepsCurrentFacing() =>
        Assert.AreEqual(FacingDir.Up,
            PlayerAnimLogic.ComputeFacing(Vector2.zero, T2, FacingDir.Up));

    [Test] public void BelowThresholdKeepsCurrentFacing() =>
        Assert.AreEqual(FacingDir.Left,
            PlayerAnimLogic.ComputeFacing(new Vector2(0.02f, 0.01f), T2, FacingDir.Left));

    [Test] public void FacingUp() =>
        Assert.AreEqual(FacingDir.Up,
            PlayerAnimLogic.ComputeFacing(new Vector2(0f, 1f), T2, FacingDir.Down));

    [Test] public void FacingDown() =>
        Assert.AreEqual(FacingDir.Down,
            PlayerAnimLogic.ComputeFacing(new Vector2(0f, -1f), T2, FacingDir.Up));

    [Test] public void FacingRight() =>
        Assert.AreEqual(FacingDir.Right,
            PlayerAnimLogic.ComputeFacing(new Vector2(1f, 0f), T2, FacingDir.Down));

    [Test] public void FacingLeft() =>
        Assert.AreEqual(FacingDir.Left,
            PlayerAnimLogic.ComputeFacing(new Vector2(-1f, 0f), T2, FacingDir.Down));

    [Test] public void DiagonalTieGoesVertical() =>
        // |vy| == |vx| -> vertical wins -> Up
        Assert.AreEqual(FacingDir.Up,
            PlayerAnimLogic.ComputeFacing(new Vector2(1f, 1f), T2, FacingDir.Down));

    [Test] public void DiagonalMostlyVerticalWins() =>
        Assert.AreEqual(FacingDir.Down,
            PlayerAnimLogic.ComputeFacing(new Vector2(1f, -2f), T2, FacingDir.Up));

    [Test] public void DiagonalMostlyHorizontalWins() =>
        Assert.AreEqual(FacingDir.Right,
            PlayerAnimLogic.ComputeFacing(new Vector2(3f, 1f), T2, FacingDir.Down));

    // --- ComputeState ---

    [Test] public void ForagingOverridesMovement() =>
        Assert.AreEqual(AnimState.Foraging,
            PlayerAnimLogic.ComputeState(new Vector2(5f, 0f), T2, foraging: true));

    [Test] public void MovingIsRun() =>
        Assert.AreEqual(AnimState.Run,
            PlayerAnimLogic.ComputeState(new Vector2(0f, 2f), T2, foraging: false));

    [Test] public void StillIsIdle() =>
        Assert.AreEqual(AnimState.Idle,
            PlayerAnimLogic.ComputeState(Vector2.zero, T2, foraging: false));

    [Test] public void ExactlyAtThresholdIsIdle() =>
        Assert.AreEqual(AnimState.Idle,
            PlayerAnimLogic.ComputeState(new Vector2(0f, T), T2, foraging: false));

    // --- FrameAtTime ---

    [Test] public void FrameAtStartIsZero() =>
        Assert.AreEqual(0, PlayerAnimLogic.FrameAtTime(0f, 0.1f, 5));

    [Test] public void FrameAdvancesWithTime() =>
        Assert.AreEqual(3, PlayerAnimLogic.FrameAtTime(0.35f, 0.1f, 5));

    [Test] public void FrameWrapsToZero() =>
        // 0.5s / 0.1s = 5 -> 5 % 5 = 0
        Assert.AreEqual(0, PlayerAnimLogic.FrameAtTime(0.5f, 0.1f, 5));

    [Test] public void FrameWrapsPartial() =>
        // 0.62s / 0.1s = 6.2 -> floor=6 -> 6 % 5 = 1
        Assert.AreEqual(1, PlayerAnimLogic.FrameAtTime(0.62f, 0.1f, 5));

    [Test] public void EmptyArrayReturnsZero() =>
        Assert.AreEqual(0, PlayerAnimLogic.FrameAtTime(1f, 0.1f, 0));

    [Test] public void ZeroSecondsPerFrameReturnsZero() =>
        Assert.AreEqual(0, PlayerAnimLogic.FrameAtTime(1f, 0f, 5));
}
