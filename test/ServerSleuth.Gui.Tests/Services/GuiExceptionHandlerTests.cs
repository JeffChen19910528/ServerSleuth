using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Services;

/// <summary>GUI-1 §8: the exception boundary's sanitization behavior, tested independently of
/// WPF's <c>Application</c>/dispatcher lifecycle (see <see cref="GuiExceptionHandler"/>'s own
/// doc comment for why this separation exists).</summary>
public class GuiExceptionHandlerTests
{
    [Fact]
    public void Handle_PublishesAGenericMessage_NeverTheRawExceptionMessage()
    {
        var stateService = new ApplicationStateService();
        var handler = new GuiExceptionHandler(NullLogger<GuiExceptionHandler>.Instance, stateService);

        const string sentinelSecret = "SERVER_SLEUTH_TEST_GUI_SECRET_4d2a";
        handler.Handle(new InvalidOperationException($"failure containing {sentinelSecret}"));

        var message = stateService.Current.LastErrorMessage;
        Assert.Equal(GuiExceptionHandler.GenericUserFacingMessage, message);
        Assert.DoesNotContain(sentinelSecret, message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Handle_NeverPublishesAStackTrace()
    {
        var stateService = new ApplicationStateService();
        var handler = new GuiExceptionHandler(NullLogger<GuiExceptionHandler>.Instance, stateService);

        Exception thrown;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            thrown = ex; // now has a real, non-null StackTrace
        }

        handler.Handle(thrown);

        Assert.DoesNotContain("at ServerSleuth", stateService.Current.LastErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Handle_DoesNotThrow_EvenForAnExceptionWithNoMessage()
    {
        var stateService = new ApplicationStateService();
        var handler = new GuiExceptionHandler(NullLogger<GuiExceptionHandler>.Instance, stateService);

        var exception = Record.Exception(() => handler.Handle(new Exception()));

        Assert.Null(exception);
    }
}
