using Common.Auth.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PaymentService.Clients;
using PaymentService.Config;
using PaymentService.Data;
using PaymentService.Extensions;
using PaymentService.Logging;
using PaymentService.Repositories;
using PaymentService.Services;
using System.Security.Claims;

namespace PaymentService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Logging first
        builder.AddAppLogging();

        // Add services to the container
        ConfigureServices(builder);

        var app = builder.Build();

        // Configure the HTTP request pipeline
        ConfigureMiddleware(app);

        app.Run();
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Configuration
        var keycloakSection = builder.Configuration.GetSection("Keycloak");
        var redisSection = builder.Configuration.GetSection("Redis");
        var jwtSection = builder.Configuration.GetSection("JwtSettings");
        
        builder.Services.Configure<KeycloakSettings>(keycloakSection);
        builder.Services.Configure<RedisSettings>(redisSection);
        builder.Services.Configure<JwtSettings>(jwtSection);
        builder.Services.Configure<BkashSettings>(builder.Configuration.GetSection("Bkash"));
        builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
        builder.Services.Configure<ServiceConfig>(
            builder.Configuration.GetSection("ServiceConfig"));

        // Redis Cache
        ConfigureRedisCache(builder, redisSection);

        // Application Services
        ConfigureApplicationServices(builder);

        // Authentication & Authorization
        ConfigureAuthentication(builder, keycloakSection);

        // Database
        ConfigureDatabase(builder);

        // API Documentation
        ConfigureSwagger(builder, keycloakSection);

        // Add OrderService client
        builder.Services.AddHttpClient<IOrderServiceClient, OrderServiceClient>();

        // Add repositories
        builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
    }

    private static void ConfigureRedisCache(WebApplicationBuilder builder, IConfigurationSection redisSection)
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis");
            options.InstanceName = redisSection.Get<RedisSettings>()!.InstanceName;
        });
    }

    private static void ConfigureApplicationServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ICacheService, RedisCacheService>();
        builder.Services.AddScoped<IPaymentService, BkashPaymentService>();
        builder.Services.AddScoped<IStripePaymentService, StripePaymentService>();
        builder.Services.AddScoped<IServiceTokenProvider, ServiceTokenProvider>();
        builder.Services.AddHttpClient<BkashClient>();

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
    }

    private static void ConfigureAuthentication(WebApplicationBuilder builder, IConfigurationSection keycloakSection)
    {
        // Authorization Policies
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdminRole", policy =>
                policy.RequireRole("admin"));
            options.AddPolicy("RequireUserRole", policy =>
                policy.RequireRole("user"));
        });

        // JWT Authentication
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = keycloakSection.Get<KeycloakSettings>()!.RequireHttpsMetadata;
                options.Authority = keycloakSection.Get<KeycloakSettings>()!.Authority;
                options.Audience = keycloakSection.Get<KeycloakSettings>()!.Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role,
                    ValidIssuer = keycloakSection.Get<KeycloakSettings>()!.Authority,
                    ValidAudience = keycloakSection.Get<KeycloakSettings>()!.Audience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var claimsIdentity = context.Principal.Identity as ClaimsIdentity;
                        if (claimsIdentity != null)
                        {
                            // Map realm roles
                            var realmRoles = context.Principal.FindFirst("realm_access")?.Value;
                            if (!string.IsNullOrEmpty(realmRoles))
                            {
                                var parsed = System.Text.Json.JsonDocument.Parse(realmRoles);
                                if (parsed.RootElement.TryGetProperty("roles", out var rolesElement) && rolesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var role in rolesElement.EnumerateArray())
                                    {
                                        claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.GetString()));
                                    }
                                }
                            }
                            // Map client roles
                            var resourceAccess = context.Principal.FindFirst("resource_access")?.Value;
                            if (!string.IsNullOrEmpty(resourceAccess))
                            {
                                var parsed = System.Text.Json.JsonDocument.Parse(resourceAccess);
                                if (parsed.RootElement.TryGetProperty("payment-service", out var paymentServiceElement) &&
                                    paymentServiceElement.TryGetProperty("roles", out var clientRolesElement) &&
                                    clientRolesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var role in clientRolesElement.EnumerateArray())
                                    {
                                        claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role.GetString()));
                                    }
                                }
                            }
                        }
                        return Task.CompletedTask;
                    }
                };
            });
    }

    private static void ConfigureDatabase(WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<PaymentDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    }

    private static void ConfigureSwagger(WebApplicationBuilder builder, IConfigurationSection keycloakSection)
    {
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{keycloakSection.Get<KeycloakSettings>()!.Authority}/protocol/openid-connect/auth"),
                        TokenUrl = new Uri($"{keycloakSection.Get<KeycloakSettings>()!.Authority}/protocol/openid-connect/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            { "openid", "OpenID Connect" },
                            { "profile", "User Profile" },
                            { "email", "Email" }
                        }
                    }
                }
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "oauth2"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        // Apply migrations
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            db.Database.Migrate();
        }

        // Middlewares
        app.UseMiddleware<ActivityLogMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                var keycloakSettings = app.Configuration.GetSection("Keycloak").Get<KeycloakSettings>();
                options.OAuthClientId(keycloakSettings!.ClientId);
                options.OAuthClientSecret(keycloakSettings.ClientSecret);
                options.OAuthUsePkce();
            });
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }
}
