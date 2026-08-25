using eTicketing.PdfGeneration.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddPdfGenerationInfrastructure();

var host = builder.Build();
await host.RunAsync();
