using Debugging.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Debugging";
});

builder.Services.AddHostedService<MonitorWorker>();
builder.Services.AddHostedService<ControlPipeServer>();

var host = builder.Build();
host.Run();
