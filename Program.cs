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
// The DbContext is scoped (one per request), so the repository that depends on it has
// to be scoped too - a singleton repository would hold on to one DbContext forever (a
// "captive dependency"), and ASP.NET Core refuses to start in Development if you try.
// Swapping InMemoryStudySessionRepository for EfStudySessionRepository is the only
// change the rest of the app needs: the service and controller don't know about it.
builder.Services.AddDbContext<StudySpotDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("StudySpot")));
builder.Services.AddScoped<IStudySessionRepository, EfStudySessionRepository>();
builder.Services.AddScoped<IStudySessionService, StudySessionService>();

var app = builder.Build();

// Create or upgrade the database to the latest migration, then add the sample sessions
// if the table is empty. A DbContext is scoped, so it has to come from a scope - there
// is no request (and so no request scope) while the app is starting.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudySpotDbContext>();
    await db.Database.MigrateAsync();
    await SampleData.SeedAsync(db);
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
