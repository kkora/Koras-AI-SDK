using System.Globalization;
using System.Reflection;
using System.Text;

namespace Koras.AI.Templates;

/// <summary>
/// A parse-once, render-many prompt template with <c>{{name}}</c> placeholders. Literal braces
/// are written by doubling the delimiters (<c>{{{{</c> renders <c>{{</c>, <c>}}}}</c> renders
/// <c>}}</c>). Deliberately logic-free: no conditionals, loops, or filters. Instances are
/// immutable and thread-safe.
/// </summary>
/// <example>
/// <code>
/// var template = PromptTemplate.Parse("Summarize for {{audience}}:\n{{text}}");
/// string prompt = template.Render(new { audience = "executives", text = document });
/// </code>
/// </example>
public sealed class PromptTemplate
{
    private abstract record Segment;
    private sealed record LiteralSegment(string Text) : Segment;
    private sealed record PlaceholderSegment(string Name) : Segment;

    private readonly IReadOnlyList<Segment> _segments;

    private PromptTemplate(string source, IReadOnlyList<Segment> segments, IReadOnlyList<string> parameterNames)
    {
        Source = source;
        _segments = segments;
        ParameterNames = parameterNames;
    }

    /// <summary>The original template text.</summary>
    public string Source { get; }

    /// <summary>The distinct placeholder names, in order of first appearance.</summary>
    public IReadOnlyList<string> ParameterNames { get; }

    /// <summary>Parses a template.</summary>
    /// <param name="template">The template text.</param>
    /// <returns>The parsed, reusable template.</returns>
    /// <exception cref="FormatException">The template contains an unterminated or invalid placeholder.</exception>
    public static PromptTemplate Parse(string template)
    {
        Guard.NotNull(template);

        var segments = new List<Segment>();
        var names = new List<string>();
        var literal = new StringBuilder();
        var position = 0;

        while (position < template.Length)
        {
            if (HasDelimiterAt(template, position, '{'))
            {
                if (HasDelimiterAt(template, position + 2, '{'))
                {
                    literal.Append("{{");
                    position += 4;
                    continue;
                }

                int close = template.IndexOf("}}", position + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    throw new FormatException($"Unterminated placeholder starting at position {position}.");
                }

                string name = template[(position + 2)..close].Trim();
                if (name.Length == 0 || !name.All(static c => char.IsLetterOrDigit(c) || c is '_' or '.'))
                {
                    throw new FormatException(
                        $"Invalid placeholder name '{name}' at position {position}. " +
                        "Names may contain letters, digits, underscores, and dots.");
                }

                FlushLiteral(segments, literal);
                segments.Add(new PlaceholderSegment(name));
                if (!names.Contains(name))
                {
                    names.Add(name);
                }

                position = close + 2;
                continue;
            }

            if (HasDelimiterAt(template, position, '}'))
            {
                if (HasDelimiterAt(template, position + 2, '}'))
                {
                    literal.Append("}}");
                    position += 4;
                    continue;
                }

                throw new FormatException($"Unexpected '}}}}' without a matching '{{{{' at position {position}.");
            }

            literal.Append(template[position]);
            position++;
        }

        FlushLiteral(segments, literal);
        return new PromptTemplate(template, segments, names);
    }

    /// <summary>Renders the template with values from a dictionary.</summary>
    /// <param name="values">Placeholder values keyed by name. Values are formatted with the invariant culture.</param>
    /// <returns>The rendered text.</returns>
    /// <exception cref="KeyNotFoundException">A placeholder has no corresponding entry.</exception>
    public string Render(IReadOnlyDictionary<string, object?> values)
    {
        Guard.NotNull(values);
        var builder = new StringBuilder(Source.Length);

        foreach (Segment segment in _segments)
        {
            switch (segment)
            {
                case LiteralSegment literalSegment:
                    builder.Append(literalSegment.Text);
                    break;

                case PlaceholderSegment placeholder:
                    if (!values.TryGetValue(placeholder.Name, out object? value))
                    {
                        throw new KeyNotFoundException(
                            $"No value was provided for template placeholder '{placeholder.Name}'.");
                    }

                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Renders the template using the public properties of <paramref name="values"/> as the value bag.</summary>
    /// <param name="values">An object (often anonymous) whose readable public properties supply placeholder values.</param>
    /// <returns>The rendered text.</returns>
    /// <exception cref="KeyNotFoundException">A placeholder has no corresponding property.</exception>
    public string Render(object values)
    {
        Guard.NotNull(values);
        var bag = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (PropertyInfo property in values.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead)
            {
                bag[property.Name] = property.GetValue(values);
            }
        }

        return Render(bag);
    }

    private static bool HasDelimiterAt(string text, int position, char delimiter)
        => position + 1 < text.Length && text[position] == delimiter && text[position + 1] == delimiter;

    private static void FlushLiteral(List<Segment> segments, StringBuilder literal)
    {
        if (literal.Length > 0)
        {
            segments.Add(new LiteralSegment(literal.ToString()));
            literal.Clear();
        }
    }
}
