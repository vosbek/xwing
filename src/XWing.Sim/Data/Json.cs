using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XWing.Sim.Data;

public static class SimJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.Converters.Add(new Vector3ArrayConverter());
        o.Converters.Add(new CurvePointConverter());
        return o;
    }

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException($"Empty {typeof(T).Name} JSON");

    public static string ReadEmbedded(string fileName)
    {
        var asm = typeof(SimJson).Assembly;
        using var stream = asm.GetManifestResourceStream($"XWing.Sim.Data.{fileName}")
            ?? throw new FileNotFoundException($"Embedded resource not found: {fileName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Vectors are written as [x, y, z].</summary>
    private sealed class Vector3ArrayConverter : JsonConverter<Vector3>
    {
        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var a = JsonSerializer.Deserialize<float[]>(ref reader, options);
            if (a is not { Length: 3 }) throw new JsonException("Vector3 must be [x, y, z]");
            return new Vector3(a[0], a[1], a[2]);
        }

        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, new[] { value.X, value.Y, value.Z }, options);
    }

    /// <summary>Curve points are written as [x, y].</summary>
    private sealed class CurvePointConverter : JsonConverter<Core.CurvePoint>
    {
        public override Core.CurvePoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var a = JsonSerializer.Deserialize<float[]>(ref reader, options);
            if (a is not { Length: 2 }) throw new JsonException("Curve point must be [x, y]");
            return new Core.CurvePoint(a[0], a[1]);
        }

        public override void Write(Utf8JsonWriter writer, Core.CurvePoint value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, new[] { value.X, value.Y }, options);
    }
}
