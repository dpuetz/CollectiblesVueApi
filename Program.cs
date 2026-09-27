using CollectiblesApi.Data;
using CollectiblesApi.Repositories;
using Dapper;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using CollectiblesApi.Services;
using Microsoft.OpenApi.Models;

SqlMapper.AddTypeHandler(new DecimalTypeHandler());

var builder = WebApplication.CreateBuilder(args);

//not necessary - just to be explicit about the logger
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddCors(options =>
{
  options.AddPolicy("AllowFrontends", policy =>
  {
    policy.WithOrigins("http://localhost:8080")
            .AllowAnyHeader()
            .AllowAnyMethod();
  });
});

// SQLite database wiring
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "collectibles.sqlite");
var connectionString = $"Data Source={dbPath}";

builder.Services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(connectionString));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IVisitStatsRepository, VisitStatsRepository>();
builder.Services.AddScoped<IUserRepository>(_ => new UserRepository(connectionString));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddSwaggerGen(options =>
{
  options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
  {
    Title = "Collectibles API",
    Version = "v1"
  });
  options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Name = "Authorization",
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "Enter your token below. No need to type 'Bearer ' — Swagger adds it for you."
  });

  options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
      options.TokenValidationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
      };
    });

//builder.Services.AddAuthorization();
builder.Services.AddAuthorization(options =>
{
  options.FallbackPolicy = new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .Build();
});

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
  FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "Images")),
  RequestPath = "/images"
});

app.UseCors("AllowFrontends");
app.UseMiddleware<CollectiblesApi.Middleware.RequestLoggingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
  options.SwaggerEndpoint("v1/swagger.json", "Collectibles API v1");
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();