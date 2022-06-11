using System.Reflection;
using FastExpressionCompiler;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Mutualify.Configuration;
using Mutualify.Database;
using Mutualify.OsuApi;
using Mutualify.OsuApi.Interfaces;
using Mutualify.Repositories;
using Mutualify.Repositories.Interfaces;
using Mutualify.Services;
using Mutualify.Services.Interfaces;
using Newtonsoft.Json;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

#region Services

var dbConfig = builder.Configuration.GetSection("Database");
var osuConfig = builder.Configuration.GetSection("osuApi");
var basePath = builder.Configuration.GetValue<string>("PathBase");

builder.Services.Configure<OsuApiConfig>(osuConfig);

TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetEntryAssembly()!);
TypeAdapterConfig.GlobalSettings.Compiler = x => x.CompileFast();
builder.Services.AddTransient<IMapper, Mapper>();

var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = dbConfig["Host"],
    Port = int.Parse(dbConfig["Port"]),
    Database = dbConfig["Database"],
    Username = dbConfig["Username"],
    Password = dbConfig["Password"]
};

builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(connectionString.ConnectionString));

builder.Services.AddAuthentication("InternalCookies")
    .AddCookie("InternalCookies", options =>
    {
        // set some paths to empty to make auth not redirect API calls
        options.LoginPath = string.Empty;
        options.AccessDeniedPath = string.Empty;
        options.LogoutPath = string.Empty;
        options.Cookie.Path = "/";
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = context =>
        {
            var name = context.Principal?.Identity?.Name;
            if (string.IsNullOrEmpty(name) || !long.TryParse(name, out _))
            {
                context.RejectPrincipal();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }

            return Task.CompletedTask;
        };

        static Task UnauthorizedRedirect(RedirectContext<CookieAuthenticationOptions> context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        }

        options.Events.OnRedirectToLogin = UnauthorizedRedirect;
        options.Events.OnRedirectToAccessDenied = UnauthorizedRedirect;
    })
    .AddCookie("ExternalCookies")
    .AddOAuth("osu", options =>
    {
        options.SignInScheme = "ExternalCookies";

        options.TokenEndpoint = "https://osu.ppy.sh/oauth/token";
        options.AuthorizationEndpoint = "https://osu.ppy.sh/oauth/authorize";
        options.ClientId = osuConfig["ClientID"];
        options.ClientSecret = osuConfig["ClientSecret"];
        options.CallbackPath = osuConfig["CallbackUrl"];
        options.Scope.Add("public");
        options.Scope.Add("friends.read");

        options.SaveTokens = true;

        options.Validate();
    });

builder.Services.AddHttpClient<OsuApiProvider>();

builder.Services.AddSingleton<IOsuApiProvider, OsuApiProvider>();
builder.Services.AddTransient<IUserRepository, UserRepository>();
builder.Services.AddTransient<IRelationRepository, RelationRepository>();

builder.Services.AddTransient<IRelationsService, RelationsService>();

builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

#endregion

#region App

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.All });

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "swagger/{documentName}/swagger.json";
        c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
        {
            var scheme = app.Environment.IsStaging() ? "https" : httpReq.Scheme;
            swaggerDoc.Servers = new List<OpenApiServer>
                { new() { Url = $"{scheme}://{httpReq.Host.Value}{basePath}" } };
        });
    });
    app.UseSwaggerUI();
}
app.UsePathBase(basePath);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetService<DatabaseContext>();
    context?.Database.Migrate();
}

app.Run();

#endregion