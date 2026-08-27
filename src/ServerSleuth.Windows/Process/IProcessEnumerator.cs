namespace ServerSleuth.Windows.Process;

public interface IProcessEnumerator
{
    IReadOnlyList<ProcessSnapshot> GetSnapshots();
}
