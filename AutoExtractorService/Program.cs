using AutoExtractorService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AutoExtractorService";
});

builder.Services.Configure<ExtractorOptions>(builder.Configuration.GetSection(ExtractorOptions.Position));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
