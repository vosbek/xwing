using System.Numerics;
using XWing.Sim.Core;
using XWing.Sim.Missions;

namespace XWing.Audio.Music;

/// <summary>
/// Reads the simulation and decides which mood the music should be in. Adds hysteresis so the
/// score doesn't flip between cruise and combat every time a TIE drifts past the threshold.
/// </summary>
public sealed class MoodTracker
{
    public float CombatRange { get; init; } = 3500f;
    /// <summary>Seconds of calm before combat music relaxes back to cruise.</summary>
    public float CalmDelay { get; init; } = 8f;

    private float _lastThreat = float.NegativeInfinity;
    private bool _victoryPlayed;
    private bool _failurePlayed;

    public MusicCue Evaluate(World world)
    {
        MissionRuntime? mission = world.Mission;
        // Stingers are requested once; afterwards the director plays their follow-up (or silence).
        if (mission?.Result == MissionResult.Failure)
        {
            if (_failurePlayed) return MusicCue.None;
            _failurePlayed = true;
            return MusicCue.Failure;
        }
        if (mission?.ObjectivesComplete == true && !_victoryPlayed)
        {
            _victoryPlayed = true;
            return MusicCue.Victory;
        }

        if (world.Player is not { Alive: true } player) return MusicCue.Cruise;
        if (Threatened(world, player)) _lastThreat = world.Time;
        return world.Time - _lastThreat < CalmDelay ? MusicCue.Combat : MusicCue.Cruise;
    }

    private bool Threatened(World world, Ship player)
    {
        if (world.Time - player.LastHitTime < 2f) return true;
        float r2 = CombatRange * CombatRange;
        foreach (Ship s in world.Ships)
        {
            if (!s.Alive) continue;
            if (player.Iff.IsHostileTo(s.Iff) && Vector3.DistanceSquared(s.Position, player.Position) < r2) return true;
            // Friendlies under fire nearby also count.
            if (s.Iff == player.Iff && world.Time - s.LastHitTime < 2f && Vector3.DistanceSquared(s.Position, player.Position) < r2 * 4f) return true;
        }
        return false;
    }
}
