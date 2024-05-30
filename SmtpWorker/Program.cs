using SmtpWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("smtp.config.json");
builder.Services.AddSystemd();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
