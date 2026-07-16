# Prompt Templates

## Overview

`Koras.AI.Templates.PromptTemplate` is a parse-once, render-many template with `{{name}}`
placeholders. It is deliberately logic-free — no conditionals, loops, or filters — so prompts
stay reviewable text. Literal braces are escaped by doubling the delimiters: `{{{{` renders
`{{` and `}}}}` renders `}}`. Placeholder names may contain letters, digits, underscores, and
dots.

## When to use it

Use templates when the same prompt shape is filled with different values across requests:
system prompts with product context, extraction prompts wrapping user documents, evaluation
rubrics. Parse once at startup, render per request.

## When not to use it

For one-off prompts, string interpolation is fine. If you need conditional sections or loops,
compose strings in C# and feed the result into a template (or skip the template entirely) —
the template language will not grow logic.

## Required packages

- `Koras.AI` (the template lives in the core package, namespace `Koras.AI.Templates`).

## Basic usage

```csharp
using Koras.AI.Templates;

PromptTemplate template = PromptTemplate.Parse(
    "Summarize the following for {{audience}} in at most {{maxWords}} words:\n{{text}}");

string prompt = template.Render(new
{
    audience = "executives",
    maxWords = 100,
    text = document,
});
```

The dictionary overload suits dynamic value bags:

```csharp
string prompt = template.Render(new Dictionary<string, object?>
{
    ["audience"] = "executives",
    ["maxWords"] = 100,
    ["text"] = document,
});
```

Inspect `template.ParameterNames` (distinct names, in order of first appearance) to validate
inputs up front, and `template.Source` to recover the original text.

## Dependency-injection usage

Templates are plain immutable objects; register parsed instances as singletons:

```csharp
builder.Services.AddSingleton(PromptTemplate.Parse("Translate to {{language}}:\n{{text}}"));
```

Then combine with the injected chat client:

```csharp
public sealed class Translator(IChatClient chat, PromptTemplate template)
{
    public async Task<string?> TranslateAsync(string text, string language, CancellationToken ct)
    {
        string prompt = template.Render(new { language, text });
        ChatResponse response = await chat.CompleteAsync(ChatRequest.FromPrompt(prompt), ct);
        return response.Text;
    }
}
```

## Error handling

- `PromptTemplate.Parse` throws `FormatException` naming the character position for an
  unterminated placeholder, an invalid placeholder name, or an unmatched `}}`.
- `Render` throws `KeyNotFoundException` naming the placeholder that has no value — missing
  values fail loudly instead of silently rendering empty text.

These are standard BCL exceptions, not `AiException`; template failures are programming
errors, not provider failures.

## Cancellation

Parsing and rendering are synchronous, in-memory operations; there is nothing to cancel.
Cancellation applies to the chat call you make with the rendered prompt.

## Formatting behavior

Values render via `Convert.ToString(value, CultureInfo.InvariantCulture)` — numbers and dates
are culture-stable. Format values yourself (for example `date.ToString("yyyy-MM-dd")`) before
passing them when you need a specific representation. `null` renders as an empty string.

## Security considerations

Rendered values are inserted verbatim — a template gives no protection against prompt
injection from user-supplied text. Keep untrusted input in clearly delimited sections of the
prompt and validate model output accordingly. Never render secrets into prompts.

## Performance considerations

`Parse` walks the template once and stores segments; `Render` is a single `StringBuilder`
pass. Cache parsed templates (they are cheap, but re-parsing per request is wasted work).

## Thread safety

`PromptTemplate` instances are immutable and thread-safe: parse once, render concurrently.

## Testing applications using this feature

Templates are trivially unit-testable:

```csharp
PromptTemplate template = PromptTemplate.Parse("Hello {{name}}!");
Assert.Equal(["name"], template.ParameterNames);
Assert.Equal("Hello Ada!", template.Render(new { name = "Ada" }));
Assert.Throws<KeyNotFoundException>(() => template.Render(new { }));
```

Because rendering is deterministic, snapshot tests on rendered prompts catch accidental
prompt drift without calling a model.

## Common mistakes

- Expecting logic (`{{#if}}`, loops, filters) — the language is placeholders only.
- Forgetting to escape literal braces in JSON examples inside prompts: write `{{{{"key"...}}}}`
  to render `{{"key"...}}`.
- Passing an anonymous object whose property casing does not match the placeholder — the
  property-bag overload matches names ordinally.
- Re-parsing the template on every request instead of caching the parsed instance.

## Related features

- [Chat completion](chat-completion.md)
- [Structured output](structured-output.md)
- [Dependency injection](dependency-injection.md)
