using System.ComponentModel;
using System.Text.Json;

namespace Koras.AI.UnitTests.Tools;

public class AiJsonSchemaTests
{
    [Description("An invoice extracted from text")]
    private sealed record Invoice(
        [property: Description("The invoice number")] string Number,
        decimal Total,
        DateOnly? DueDate,
        InvoiceStatus Status,
        IReadOnlyList<string> LineItems);

    private enum InvoiceStatus
    {
        Draft,
        Sent,
        Paid,
    }

    [Fact]
    public void FromType_produces_strict_object_schema()
    {
        JsonElement schema = AiJsonSchema.FromType<Invoice>();

        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());

        string[] required = [.. schema.GetProperty("required").EnumerateArray().Select(static e => e.GetString()!)];
        Assert.Equal(5, required.Length); // strict mode: every property is required

        JsonElement properties = schema.GetProperty("properties");
        Assert.Equal("An invoice extracted from text", schema.GetProperty("description").GetString());
        Assert.Equal("The invoice number", properties.GetProperty("number").GetProperty("description").GetString());
    }

    [Fact]
    public void FromType_camel_cases_property_names()
    {
        JsonElement schema = AiJsonSchema.FromType<Invoice>();
        JsonElement properties = schema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("lineItems", out _));
        Assert.False(properties.TryGetProperty("LineItems", out _));
    }

    [Fact]
    public void FromType_represents_enums_as_string_constraints()
    {
        JsonElement schema = AiJsonSchema.FromType<Invoice>();
        JsonElement status = schema.GetProperty("properties").GetProperty("status");
        Assert.True(status.TryGetProperty("enum", out JsonElement values));
        Assert.Equal(3, values.GetArrayLength());
    }

    [Fact]
    public void FromType_handles_nested_arrays_and_objects()
    {
        JsonElement schema = AiJsonSchema.FromType<Order>();
        JsonElement items = schema.GetProperty("properties").GetProperty("lines");
        Assert.Equal("array", items.GetProperty("type").GetString());
        JsonElement itemSchema = items.GetProperty("items");
        Assert.False(itemSchema.GetProperty("additionalProperties").GetBoolean()); // strictness recurses
    }

    [Fact]
    public void FromMethodParameters_reflects_the_signature()
    {
        JsonElement schema = AiJsonSchema.FromMethodParameters(
            new Func<string, int, CancellationToken, string>(static (a, b, ct) => a).Method);

        JsonElement properties = schema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("arg1", out _) || properties.EnumerateObject().Count() == 2);
        Assert.Equal(2, properties.EnumerateObject().Count()); // CancellationToken excluded
    }

    private sealed record Order(string Id, IReadOnlyList<OrderLine> Lines);

    private sealed record OrderLine(string Sku, int Quantity);
}
