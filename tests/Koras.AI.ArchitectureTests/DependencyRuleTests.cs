using System.Reflection;
using NetArchTest.Rules;

namespace Koras.AI.ArchitectureTests;

/// <summary>Enforces docs/architecture/dependency-rules.md.</summary>
public class DependencyRuleTests
{
    private static readonly Assembly Abstractions = typeof(IChatClient).Assembly;
    private static readonly Assembly Core = typeof(KorasAiBuilder).Assembly;

    private static readonly Assembly[] ProviderAssemblies =
    [
        typeof(OpenAI.OpenAIOptions).Assembly,
        typeof(AzureOpenAI.AzureOpenAIOptions).Assembly,
        typeof(Anthropic.AnthropicOptions).Assembly,
        typeof(Gemini.GeminiOptions).Assembly,
        typeof(Ollama.OllamaOptions).Assembly,
    ];

    [Fact]
    public void Abstractions_depends_only_on_the_bcl()
    {
        string[] forbidden =
        [
            "Microsoft.Extensions",
            "Koras.AI,", // the core assembly (full-name prefix match needs the comma)
            "OpenTelemetry",
        ];

        foreach (AssemblyName reference in Abstractions.GetReferencedAssemblies())
        {
            Assert.DoesNotContain(forbidden, prefix => (reference.FullName + ",").StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Core_does_not_reference_any_provider_assembly()
    {
        string[] providerNames = [.. ProviderAssemblies.Select(static a => a.GetName().Name!)];
        foreach (AssemblyName reference in Core.GetReferencedAssemblies())
        {
            Assert.DoesNotContain(reference.Name, providerNames);
        }
    }

    [Fact]
    public void Providers_do_not_reference_each_other_except_azure_to_openai()
    {
        foreach (Assembly provider in ProviderAssemblies)
        {
            string self = provider.GetName().Name!;
            foreach (AssemblyName reference in provider.GetReferencedAssemblies())
            {
                bool referencesAnotherProvider = ProviderAssemblies.Any(
                    other => other.GetName().Name == reference.Name && other.GetName().Name != self);

                if (referencesAnotherProvider)
                {
                    Assert.True(
                        self == "Koras.AI.AzureOpenAI" && reference.Name == "Koras.AI.OpenAI",
                        $"{self} must not reference {reference.Name} (only AzureOpenAI → OpenAI is sanctioned).");
                }
            }
        }
    }

    [Fact]
    public void No_vendor_sdk_dependencies_anywhere()
    {
        string[] vendorPrefixes = ["OpenAI", "Azure.AI", "Anthropic.SDK", "Google.", "OllamaSharp", "Mscc.Generative"];
        foreach (Assembly assembly in ProviderAssemblies.Append(Core).Append(Abstractions))
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                Assert.DoesNotContain(vendorPrefixes, prefix => reference.Name!.StartsWith(prefix, StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void Public_client_and_decorator_types_are_thread_safe_shapes()
    {
        // Clients must not expose public mutable instance fields (state must be private/readonly).
        IEnumerable<Type> clientTypes = Types.InAssemblies([Core, .. ProviderAssemblies])
            .That().ImplementInterface(typeof(IChatClient))
            .GetTypes();

        Assert.NotEmpty(clientTypes);
        foreach (Type type in clientTypes)
        {
            FieldInfo[] mutablePublicFields = [.. type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(static f => !f.IsInitOnly)];
            Assert.True(mutablePublicFields.Length == 0, $"{type.FullName} exposes mutable public fields.");
        }
    }

    [Fact]
    public void Options_types_live_in_their_provider_namespace_and_end_with_Options()
    {
        foreach (Assembly provider in ProviderAssemblies)
        {
            IEnumerable<Type>? optionTypes = Types.InAssembly(provider)
                .That().HaveNameEndingWith("Options")
                .GetTypes();

            Type option = Assert.Single(optionTypes);
            Assert.StartsWith("Koras.AI.", option.Namespace);
        }
    }

    [Fact]
    public void Builder_extension_classes_follow_the_naming_convention()
    {
        foreach (Assembly provider in ProviderAssemblies)
        {
            IEnumerable<Type> extensionClasses = Types.InAssembly(provider)
                .That().HaveNameEndingWith("KorasAiBuilderExtensions")
                .GetTypes();

            Type extension = Assert.Single(extensionClasses);
            Assert.Equal("Koras.AI", extension.Namespace); // discoverable without extra usings
        }
    }
}
