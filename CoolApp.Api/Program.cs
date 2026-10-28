using CoolApp.Data;
using CoolApp.Repositories;
using CoolApp.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews()
    // Serialize enums (e.g. StudySession.Status) as their names ("Scheduled") instead
    // of the underlying number - readable in Swagger, the .http responses, and the UI.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Dependency Injection: tell the container which concrete type to hand out whenever a
// constructor asks for an abstraction. The controller asks for IStudySessionService, the
// service asks for IStudySessionRepository - neither one creates what it depends on.
//
// The connection string comes from configuration (appsettings.json locally), never from
// the code. It's read inside the lambda, when the first DbContext is created, so the
// integration tests can point the app at their own database.
//
// AddDbContext registers the DbContext as scoped (one per request), so the repository
// that depends on it is scoped too - a singleton repository would hold on to one
// DbContext forever (a "captive dependency"). Swapping InMemoryStudySessionRepository
// for EfStudySessionRepository is the only change the rest of the app needs.
builder.Services.AddDbContext<StudySpotDbContext>((services, options) =>
    StudySpotDbContext.Configure(options,
        services.GetRequiredService<IConfiguration>().GetConnectionString("StudySpot")
            ?? throw new InvalidOperationException("Connection string 'StudySpot' is missing")));
builder.Services.AddScoped<IStudySessionRepository, EfStudySessionRepository>();
builder.Services.AddScoped<IStudySessionService, StudySessionService>();

var app = builder.Build();

// Create or upgrade the database to the latest migration (UseSeeding then adds the sample
// sessions). Convenient during development only: in production, several instances
// starting at once would all try to migrate. A DbContext is scoped, so it has to come
// from a scope - there is no request (and so no request scope) while the app starts.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<StudySpotDbContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapFallbackToFile("index.html");

app.Run();
