using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Domain;

namespace Finance.Host;

/// <summary>
/// <see cref="Money"/> crosses the API boundary as a plain decimal shekel amount
/// — the same representation <see cref="Finance.Domain.Reporting.Measures"/> uses
/// — not as its internal <c>{ agorot, shekels, … }</c> shape.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Money.FromShekels(reader.GetDecimal());

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Shekels);
}
