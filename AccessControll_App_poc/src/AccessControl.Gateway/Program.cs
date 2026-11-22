using AccessControl.Application;
using AccessControl.Application.Common.Interfaces;
using AccessControl.Gateway.Auditing;
using AccessControl.Gateway.Services;
using AccessControl.Infrastructure;
using AccessControl.Infrastructure.Aero;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Console app in development; Windows Service when installed with sc.exe / New-Service.
builder.Services.AddWindowsService(options => options.ServiceName = "AccessControl Gateway");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAeroDriver(builder.Configuration);

// No HTTP request here: audit columns written by the Gateway are attributed to the service itself.
builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentUser, GatewayCurrentUser>());

// Order matters: the driver starts before replies are processed and stops after (hosted services stop in reverse).
builder.Services.AddHostedService<DriverLifetimeService>();
builder.Services.AddHostedService<ReplyProcessingService>();

var host = builder.Build();
host.Run();
