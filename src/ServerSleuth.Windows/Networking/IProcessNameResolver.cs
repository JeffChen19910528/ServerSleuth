namespace ServerSleuth.Windows.Networking;

public interface IProcessNameResolver
{
    string? GetProcessName(int pid);
}
