namespace XWing.Sim.Data;

public sealed class ShipCatalog
{
    private readonly Dictionary<string, ShipClass> _classes;

    public ShipCatalog(IEnumerable<ShipClass> classes) =>
        _classes = classes.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ShipClass> All => _classes.Values;

    public ShipClass Get(string id) =>
        _classes.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"Unknown ship class '{id}'");

    public static ShipCatalog FromJson(string json) => new(SimJson.Deserialize<List<ShipClass>>(json));

    /// <summary>The catalog shipped with the engine (provisional values, no original game data).</summary>
    public static ShipCatalog LoadBuiltIn() => FromJson(SimJson.ReadEmbedded("ships.json"));
}
