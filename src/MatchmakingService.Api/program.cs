using MatchmakingService.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMatchmakingService, MatchmakingService.Application.Services.MatchmakingService>();
builder.Services.AddHostedService<MatchmakingWorker>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Matchmaking Service",
        Version = "v1",
        Description = "REST API for matchmaking in multiplayer games"
    });
    c.AddServer(new() { Url = "http://localhost:8080" });
});

var app = builder.Build();
app.UseExceptionHandler(error =>
{
    error.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { error = "An unexpected error occurred" }));
    });
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Matchmaking Service v1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

app.Run();