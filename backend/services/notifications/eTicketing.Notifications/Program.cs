using eTicketing.Notifications.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddNotificationsInfrastructure();

var host = builder.Build();
await host.RunAsync();
