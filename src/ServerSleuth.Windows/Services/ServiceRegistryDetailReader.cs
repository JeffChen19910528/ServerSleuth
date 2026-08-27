using Microsoft.Win32;
using ServerSleuth.Windows.Registry;

namespace ServerSleuth.Windows.Services;

public static class ServiceRegistryDetailReader
{
    public static ServiceRegistryDetail Read(IWindowsRegistryReader registryReader, string serviceName)
    {
        var keyPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
        var values = registryReader.GetValues(RegistryHive.LocalMachine, RegistryView.Registry64, keyPath);

        if (!values.Success || values.Value is null)
        {
            return new ServiceRegistryDetail();
        }

        var data = values.Value;

        var parameters = registryReader.GetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{keyPath}\Parameters");
        var serviceDll = parameters.Success ? parameters.Value?.GetValueOrDefault("ServiceDll") as string : null;

        return new ServiceRegistryDetail
        {
            ImagePath = data.GetValueOrDefault("ImagePath") as string,
            ObjectName = data.GetValueOrDefault("ObjectName") as string,
            Description = data.GetValueOrDefault("Description") as string,
            StartMode = data.GetValueOrDefault("Start") is int start ? start : null,
            DelayedAutoStart = data.GetValueOrDefault("DelayedAutostart") is int delayed ? delayed != 0 : null,
            DependOnService = data.GetValueOrDefault("DependOnService") as string[] ?? [],
            ServiceDll = serviceDll,
            HasRecoveryConfiguration = data.ContainsKey("FailureActions")
        };
    }
}
