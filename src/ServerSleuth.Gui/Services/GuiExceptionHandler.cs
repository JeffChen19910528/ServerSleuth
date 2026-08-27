using Microsoft.Extensions.Logging;

namespace ServerSleuth.Gui.Services;

/// <summary>The only <see cref="IGuiExceptionHandler"/> implementation. Deliberately does NOT
/// bind <see cref="Exception.Message"/> or <see cref="Exception.StackTrace"/> into application
/// state — a fixed, generic phrase is published instead, so no exception (including one thrown
/// by future code this phase cannot anticipate) can leak secret material through this path
/// merely by having it in its message.</summary>
public sealed class GuiExceptionHandler(ILogger<GuiExceptionHandler> logger, IApplicationStateService applicationStateService) : IGuiExceptionHandler
{
    public const string GenericUserFacingMessage = "An unexpected error occurred. See application logs for details.";

    public void Handle(Exception exception)
    {
        logger.LogError(exception, "Unhandled GUI exception.");
        applicationStateService.Update(state => state with { LastErrorMessage = GenericUserFacingMessage });
    }
}
