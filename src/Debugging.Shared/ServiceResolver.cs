using System.ServiceProcess;

namespace Debugging.Shared;

public static class ServiceResolver
{
    public static ServiceController? Resolve(ServiceTarget target)
    {
        try
        {
            return new ServiceController(target.ServiceName);
        }
        catch (InvalidOperationException)
        {
            // Fall through to display-name lookup.
        }

        return ServiceController.GetServices()
            .FirstOrDefault(service =>
                service.DisplayName.Contains(target.DisplayNameContains, StringComparison.OrdinalIgnoreCase));
    }
}
