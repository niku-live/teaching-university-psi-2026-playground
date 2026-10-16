using CoolApp.Repositories;
using CoolApp.Services;

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
// Singleton for the repository because the in-memory data lives inside the instance
// (see InMemoryStudySessionRepository); a service may only depend on services with the
// same or a longer lifetime, so the scoped service below can safely depend on it.
builder.Services.AddSingleton<IStudySessionRepository, InMemoryStudySessionRepository>();
builder.Services.AddScoped<IStudySessionService, StudySessionService>();

var app = builder.Build();

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
