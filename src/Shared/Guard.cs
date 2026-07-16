using System.Runtime.CompilerServices;

namespace Koras.AI;

/// <summary>Internal argument validation helpers.</summary>
internal static class Guard
{
    public static T NotNull<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, name);
        return value;
    }

    public static string NotNullOrWhiteSpace(string value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value;
    }
}
