using System.Reflection;
using System.Text.Json;

namespace Koras.AI;

/// <summary>
/// A tool (function) the model may call. Create an executable tool from a delegate with
/// <see cref="Create"/> — the parameter schema is generated from the delegate's signature — or
/// a declaration-only tool with <see cref="Declare"/> when the application dispatches calls
/// itself. Instances are immutable and safe to share across requests and threads.
/// </summary>
public class AiTool
{
    private readonly Delegate? _handler;

    private AiTool(string name, string? description, JsonElement parametersSchema, Delegate? handler)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Description = description;
        ParametersSchema = parametersSchema;
        _handler = handler;
    }

    /// <summary>The tool name presented to the model. Use lower_snake_case for widest provider compatibility.</summary>
    public string Name { get; }

    /// <summary>A description that helps the model decide when to call the tool.</summary>
    public string? Description { get; }

    /// <summary>The JSON Schema describing the tool's arguments object.</summary>
    public JsonElement ParametersSchema { get; }

    /// <summary>Whether this tool carries a handler and can be executed via <see cref="InvokeAsync"/>.</summary>
    public bool CanInvoke => _handler is not null;

    /// <summary>
    /// Creates an executable tool from a delegate. Parameters become the tool's argument
    /// schema (annotate with <see cref="System.ComponentModel.DescriptionAttribute"/> for
    /// better model guidance); a <see cref="CancellationToken"/> parameter, if present,
    /// receives the caller's token. The handler may return <see cref="string"/>, any
    /// serializable value, or <see cref="Task{TResult}"/>/<see cref="ValueTask{TResult}"/> of
    /// either.
    /// </summary>
    /// <param name="name">The tool name presented to the model.</param>
    /// <param name="description">A description that helps the model decide when to call the tool.</param>
    /// <param name="handler">The code to execute when the model calls the tool.</param>
    public static AiTool Create(string name, string? description, Delegate handler)
    {
        Guard.NotNull(handler);
        JsonElement schema = AiJsonSchema.FromMethodParameters(handler.Method);
        return new AiTool(name, description, schema, handler);
    }

    /// <summary>
    /// Creates a declaration-only tool: the model can call it, but the application inspects
    /// <see cref="ChatMessage.ToolCalls"/> and executes the operation itself.
    /// </summary>
    /// <param name="name">The tool name presented to the model.</param>
    /// <param name="description">A description that helps the model decide when to call the tool.</param>
    /// <param name="parametersSchema">The JSON Schema for the tool's arguments object.</param>
    public static AiTool Declare(string name, string? description, JsonElement parametersSchema)
        => new(name, description, parametersSchema, handler: null);

    /// <summary>
    /// Executes the tool handler with the model-provided arguments and returns the result
    /// serialized as a string (string results pass through; other values serialize as JSON).
    /// </summary>
    /// <param name="argumentsJson">The arguments JSON object produced by the model.</param>
    /// <param name="cancellationToken">Propagated to a <see cref="CancellationToken"/> handler parameter, when declared.</param>
    /// <returns>The tool result to send back to the model.</returns>
    /// <exception cref="InvalidOperationException">The tool is declaration-only (<see cref="CanInvoke"/> is <see langword="false"/>).</exception>
    /// <exception cref="AiException">
    /// Thrown with <see cref="AiErrorCode.InvalidResponse"/> when arguments cannot be bound to
    /// the handler's parameters.
    /// </exception>
    public async Task<string> InvokeAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        if (_handler is null)
        {
            throw new InvalidOperationException(
                $"Tool '{Name}' is declaration-only and cannot be invoked. Use AiTool.Create to attach a handler.");
        }

        object?[] arguments = BindArguments(argumentsJson, _handler.Method, cancellationToken);

        object? result;
        try
        {
            result = _handler.DynamicInvoke(arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }

        result = await UnwrapAsync(result).ConfigureAwait(false);

        return result switch
        {
            null => "null",
            string text => text,
            _ => JsonSerializer.Serialize(result, AiJson.DefaultOptions),
        };
    }

    private object?[] BindArguments(string argumentsJson, MethodInfo method, CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        }
        catch (JsonException ex)
        {
            throw new AiException(
                $"The model's arguments for tool '{Name}' are not valid JSON.",
                AiErrorCode.InvalidResponse,
                ex)
            {
                ProviderErrorBody = argumentsJson,
            };
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            ParameterInfo[] parameters = method.GetParameters();
            var bound = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (parameter.ParameterType == typeof(CancellationToken))
                {
                    bound[i] = cancellationToken;
                    continue;
                }

                string parameterName = parameter.Name ?? $"arg{parameter.Position}";
                if (root.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(root, parameterName, out JsonElement value))
                {
                    try
                    {
                        bound[i] = value.Deserialize(parameter.ParameterType, AiJson.DefaultOptions);
                    }
                    catch (JsonException ex)
                    {
                        throw new AiException(
                            $"Argument '{parameterName}' for tool '{Name}' could not be converted to {parameter.ParameterType.Name}.",
                            AiErrorCode.InvalidResponse,
                            ex)
                        {
                            ProviderErrorBody = argumentsJson,
                        };
                    }
                }
                else if (parameter.HasDefaultValue)
                {
                    bound[i] = parameter.DefaultValue;
                }
                else
                {
                    throw new AiException(
                        $"The model omitted required argument '{parameterName}' for tool '{Name}'.",
                        AiErrorCode.InvalidResponse)
                    {
                        ProviderErrorBody = argumentsJson,
                    };
                }
            }

            return bound;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static async Task<object?> UnwrapAsync(object? result)
    {
        switch (result)
        {
            case Task task:
                await task.ConfigureAwait(false);
                Type taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    return taskType.GetProperty("Result")?.GetValue(task);
                }

                return null;

            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                return null;

            default:
                Type? resultType = result?.GetType();
                if (resultType is { IsGenericType: true }
                    && resultType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    var asTask = (Task)resultType.GetMethod("AsTask")!.Invoke(result, null)!;
                    await asTask.ConfigureAwait(false);
                    return asTask.GetType().GetProperty("Result")?.GetValue(asTask);
                }

                return result;
        }
    }
}
