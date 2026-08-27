namespace ServerSleuth.Windows.Services;

public interface IServiceEnumerator
{
    IReadOnlyList<ServiceSnapshot> GetSnapshots();
}
