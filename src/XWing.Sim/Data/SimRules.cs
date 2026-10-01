using XWing.Sim.Core;

namespace XWing.Sim.Data;

/// <summary>
/// Global rules of the X-Wing runtime that are not per ship class.
/// Defaults are hypotheses; see docs/SPEC.md for the ID of each one.
/// </summary>
public sealed class SimRules
{
    /// <summary>Fixed simulation rate. [SPEC T-01] Original is likely frame-coupled; unknown.</summary>
    public int TickRate { get; init; } = 60;

    /// <summary>
    /// Turn-rate multiplier as a function of speed / max speed. [SPEC F-03]
    /// Hypothesis: X-Wing's fighters turn best at mid throttle, like SWOTL's prop planes.
    /// </summary>
    public Curve ManeuverCurve { get; init; } = new(
        new CurvePoint(0.00f, 0.80f),
        new CurvePoint(0.33f, 1.00f),
        new CurvePoint(0.67f, 0.92f),
        new CurvePoint(1.00f, 0.75f));

    /// <summary>Seconds for angular rate to reach the commanded rate; 0 = instantaneous. [SPEC F-04]</summary>
    public float AngularResponseTime { get; init; } = 0.08f;

    /// <summary>Fraction of max speed lost per recharge level above "normal" (and gained per level below). [SPEC E-01]</summary>
    public float EngineCostPerLevel { get; init; } = 0.125f;
    public float EngineFactorMin { get; init; } = 0.5f;
    public float EngineFactorMax { get; init; } = 1.25f;

    /// <summary>Shield points moved per second from the unfocused to the focused side. [SPEC E-04]</summary>
    public float ShieldFocusTransferRate { get; init; } = 4f;

    /// <summary>Laser charge (fraction of one cannon) moved per energy-transfer keypress. [SPEC E-05]</summary>
    public float TransferChunk { get; init; } = 0.25f;
    /// <summary>Shield points produced per unit of laser charge transferred (and the inverse). [SPEC E-05]</summary>
    public float TransferShieldPerCharge { get; init; } = 25f;

    /// <summary>Hull/shield damage per m/s of closing speed in a ship-ship collision. [SPEC C-04]</summary>
    public float CollisionDamagePerSpeed { get; init; } = 0.5f;

    public float Dt => 1f / TickRate;
}
