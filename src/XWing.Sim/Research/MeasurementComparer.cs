using System.Globalization;

namespace XWing.Sim.Research;

/// <summary>A value measured in the original game, with how far off the engine may be.</summary>
public sealed record ReferenceMeasurement(
    string TestId, string Ship, string Condition, double Value, string Unit, double Tolerance, string Source, string Notes);

public enum CompareStatus { Pass, Fail, Unmeasured }

public sealed record ComparisonRow(Measurement Sim, ReferenceMeasurement? Reference, CompareStatus Status, double? Delta);

public static class MeasurementComparer
{
    public const string SimHeader = "test_id,ship,condition,value,unit";
    public const string ReferenceHeader = "test_id,ship,condition,value,unit,tolerance,source,notes";

    public static string ToCsv(IEnumerable<Measurement> rows)
    {
        var sb = new System.Text.StringBuilder(SimHeader).Append('\n');
        foreach (Measurement m in rows)
            sb.Append($"{m.TestId},{m.Ship},{m.Condition},{m.Value.ToString(CultureInfo.InvariantCulture)},{m.Unit}\n");
        return sb.ToString();
    }

    public static List<Measurement> ParseSim(string csv) =>
        Rows(csv).Select(c => new Measurement(c[0], c[1], c[2], double.Parse(c[3], CultureInfo.InvariantCulture), c[4])).ToList();

    /// <summary>Lines starting with '#' are comments. Notes may contain commas (last column).</summary>
    public static List<ReferenceMeasurement> ParseReference(string csv) =>
        Rows(csv).Select(c => new ReferenceMeasurement(
            c[0], c[1], c[2],
            double.Parse(c[3], CultureInfo.InvariantCulture), c[4],
            double.Parse(c[5], CultureInfo.InvariantCulture),
            c.Length > 6 ? c[6] : "",
            c.Length > 7 ? string.Join(",", c.Skip(7)) : "")).ToList();

    public static List<ComparisonRow> Compare(IEnumerable<Measurement> sim, IEnumerable<ReferenceMeasurement> reference)
    {
        var lookup = reference.ToDictionary(r => (r.TestId, r.Ship.ToUpperInvariant(), r.Condition));
        return sim.Select(s =>
        {
            if (!lookup.TryGetValue((s.TestId, s.Ship.ToUpperInvariant(), s.Condition), out var r))
                return new ComparisonRow(s, null, CompareStatus.Unmeasured, null);
            double delta = s.Value - r.Value;
            return new ComparisonRow(s, r, Math.Abs(delta) <= r.Tolerance ? CompareStatus.Pass : CompareStatus.Fail, delta);
        }).ToList();
    }

    private static IEnumerable<string[]> Rows(string csv) =>
        csv.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith("test_id,", StringComparison.Ordinal))
            .Select(l => l.Split(','));
}
