namespace ServerSleuth.Gui.Services;

/// <summary>
/// GUI-1 §8 (Error Boundary): the SANITIZATION half of the application-level exception
/// boundary, deliberately separated from <c>App.xaml.cs</c>'s WPF-specific event wiring
/// (<see cref="System.Windows.Threading.Dispatcher.UnhandledException"/>/
/// <see cref="AppDomain.UnhandledException"/>/<see cref="TaskScheduler.UnobservedTaskException"/>)
/// so this logic is unit-testable without a live WPF <c>Application</c>/STA thread — WPF only
/// allows one <c>Application</c> instance per process and requires it to run on a dispatcher
/// thread, which would make the sanitization behavior itself untestable if it stayed inline in
/// the event handlers.
/// </summary>
public interface IGuiExceptionHandler
{
    /// <summary>Logs the full exception (existing <c>Microsoft.Extensions.Logging</c>
    /// infrastructure — no second logging framework) and publishes a CONCISE, credential-free
    /// message into <see cref="IApplicationStateService"/> — never the raw exception message or
    /// a stack trace, matching <c>ServerSleuth.Cli.CliApplication</c>'s own "no stack trace in
    /// normal output" convention for its top-level exception handling.</summary>
    void Handle(Exception exception);
}
