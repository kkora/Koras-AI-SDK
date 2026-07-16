using Koras.AI.Templates;

namespace Koras.AI.UnitTests.Templates;

public class PromptTemplateTests
{
    [Fact]
    public void Parse_and_render_with_dictionary()
    {
        var template = PromptTemplate.Parse("Summarize for {{audience}}:\n{{text}}");

        Assert.Equal(["audience", "text"], template.ParameterNames);
        string result = template.Render(new Dictionary<string, object?>
        {
            ["audience"] = "executives",
            ["text"] = "quarterly numbers",
        });

        Assert.Equal("Summarize for executives:\nquarterly numbers", result);
    }

    [Fact]
    public void Render_with_anonymous_object_property_bag()
    {
        var template = PromptTemplate.Parse("Hello {{name}}, you are {{age}}.");
        Assert.Equal("Hello Ada, you are 36.", template.Render(new { name = "Ada", age = 36 }));
    }

    [Fact]
    public void Doubled_delimiters_render_literal_braces()
    {
        var template = PromptTemplate.Parse("json: {{{{\"key\": {{value}}}}}}");
        Assert.Equal("json: {{\"key\": 1}}", template.Render(new { value = 1 }));
    }

    [Fact]
    public void Missing_value_throws_with_placeholder_name()
    {
        var template = PromptTemplate.Parse("{{present}} {{missing}}");
        var ex = Assert.Throws<KeyNotFoundException>(() => template.Render(new { present = "x" }));
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void Null_values_render_as_empty()
    {
        var template = PromptTemplate.Parse("[{{value}}]");
        Assert.Equal("[]", template.Render(new Dictionary<string, object?> { ["value"] = null }));
    }

    [Fact]
    public void Values_format_with_invariant_culture()
    {
        var template = PromptTemplate.Parse("{{number}}");
        Assert.Equal("3.14", template.Render(new { number = 3.14 }));
    }

    [Theory]
    [InlineData("{{unterminated")]
    [InlineData("{{bad name}}")]
    [InlineData("{{}}")]
    [InlineData("orphan }} closer")]
    public void Invalid_templates_throw_FormatException(string template)
        => Assert.Throws<FormatException>(() => PromptTemplate.Parse(template));

    [Fact]
    public void Repeated_placeholders_dedupe_in_ParameterNames_but_render_everywhere()
    {
        var template = PromptTemplate.Parse("{{x}} and {{x}}");
        Assert.Single(template.ParameterNames);
        Assert.Equal("1 and 1", template.Render(new { x = 1 }));
    }

    [Fact]
    public async Task Render_is_thread_safe_for_concurrent_use()
    {
        var template = PromptTemplate.Parse("{{a}}-{{b}}");
        string[] results = await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            Task.Run(() => template.Render(new Dictionary<string, object?> { ["a"] = i, ["b"] = i * 2 }))));

        for (var i = 0; i < results.Length; i++)
        {
            Assert.Equal($"{i}-{i * 2}", results[i]);
        }
    }
}
