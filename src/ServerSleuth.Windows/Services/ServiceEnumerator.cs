using System.ServiceProcess;

namespace ServerSleuth.Windows.Services;

/// <summary>
/// ServiceController.GetServices() enumerates Win32 services only (not device drivers),
/// which matches skill.md §7's scope for the Windows Service Scanner.
/// </summary>
public sealed class ServiceEnumerator : IServiceEnumerator
{
    public IReadOnlyList<ServiceSnapshot> GetSnapshots()
    {
        var snapshots = new List<ServiceSnapshot>();

        foreach (var controller in ServiceController.GetServices())
        {
            using (controller)
            {
                snapshots.Add(new ServiceSnapshot
                {
                    ServiceName = controller.ServiceName,
                    DisplayName = controller.DisplayName,
                    Status = controller.Status.ToString(),
                    ServiceType = controller.ServiceType.ToString()
                });
            }
        }

        return snapshots;
    }
}
