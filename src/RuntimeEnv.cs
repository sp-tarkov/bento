using Spectre.Console;

namespace Bento;

/// <summary>
/// How this run may interact with the user. Containers and --no-config produce fully-flagged behaviour: no config file,
/// no prompts, and missing input fails fast.
/// </summary>
public sealed record RuntimeEnv(bool InContainer, bool UseConfig, bool Interactive)
{
    /// <summary>
    /// Derives the interaction mode from environment variables and console capabilities.
    /// </summary>
    public static RuntimeEnv Detect(bool noConfig)
    {
        // DOTNET_RUNNING_IN_CONTAINER is set to "true" (or "1") in every Microsoft .NET base image, including the SDK
        // image the bento Dockerfile builds on.
        var inContainer =
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") is { } value
            && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

        var useConfig = !noConfig && !inContainer;
        var interactive = useConfig && AnsiConsole.Profile.Capabilities.Interactive;
        return new RuntimeEnv(inContainer, useConfig, interactive);
    }
}
