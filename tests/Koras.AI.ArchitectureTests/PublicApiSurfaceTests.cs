using System.Reflection;
using System.Text;

namespace Koras.AI.ArchitectureTests;

/// <summary>
/// The public API compatibility gate (docs/api/backward-compatibility.md): every shipping
/// assembly's public surface is snapshotted into <c>PublicApi/*.approved.txt</c>. Any change
/// fails this test until the snapshot is deliberately regenerated — set
/// <c>UPDATE_PUBLIC_API=1</c> and re-run, then review the diff like any other code change.
/// </summary>
public class PublicApiSurfaceTests
{
    public static TheoryData<string> AssemblyNames { get; } = new(
    [
        "Koras.AI.Abstractions",
        "Koras.AI",
        "Koras.AI.OpenAI",
        "Koras.AI.AzureOpenAI",
        "Koras.AI.Anthropic",
        "Koras.AI.Gemini",
        "Koras.AI.Ollama",
        "Koras.AI.AspNetCore",
        "Koras.AI.OpenTelemetry",
    ]);

    [Theory]
    [MemberData(nameof(AssemblyNames))]
    public void Public_api_surface_matches_the_approved_snapshot(string assemblyName)
    {
        Assembly assembly = Assembly.Load(assemblyName);
        string actual = RenderPublicApi(assembly);

        string approvedDir = Path.Combine(FindRepoRoot(), "tests", "Koras.AI.ArchitectureTests", "PublicApi");
        Directory.CreateDirectory(approvedDir);
        string approvedFile = Path.Combine(approvedDir, assemblyName + ".approved.txt");

        if (Environment.GetEnvironmentVariable("UPDATE_PUBLIC_API") == "1")
        {
            File.WriteAllText(approvedFile, actual);
        }

        Assert.True(File.Exists(approvedFile), $"Missing snapshot {approvedFile}. Run with UPDATE_PUBLIC_API=1 to create it.");
        string approved = File.ReadAllText(approvedFile).ReplaceLineEndings("\n");

        Assert.True(
            approved == actual,
            $"Public API of {assemblyName} changed. If intentional, re-run with UPDATE_PUBLIC_API=1, review the diff " +
            $"against docs/api/public-api-review-checklist.md, and commit the snapshot.");
    }

    private static string RenderPublicApi(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (Type type in assembly.GetExportedTypes().OrderBy(static t => t.FullName, StringComparer.Ordinal))
        {
            lines.Add(RenderType(type));
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OrderBy(static m => m.ToString(), StringComparer.Ordinal))
            {
                if (member is MethodInfo { IsSpecialName: true })
                {
                    continue; // property/event accessors — the property itself is listed
                }

                lines.Add($"  {member.MemberType}: {member}");
            }
        }

        var builder = new StringBuilder();
        foreach (string line in lines)
        {
            builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }

    private static string RenderType(Type type)
    {
        string kind = type.IsInterface ? "interface"
            : type.IsEnum ? "enum"
            : type.IsValueType ? "struct"
            : type.IsAbstract && type.IsSealed ? "static class"
            : type.IsAbstract ? "abstract class"
            : type.IsSealed ? "sealed class"
            : "class";
        string baseType = type.BaseType is { } bt && bt != typeof(object) && !type.IsValueType && !type.IsEnum
            ? $" : {bt.FullName}"
            : string.Empty;
        return $"{kind} {type.FullName}{baseType}";
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Koras.AI.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
