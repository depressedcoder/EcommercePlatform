using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using OrderService.Config;
using OrderService.Data;
using OrderService.Extensions;
using OrderService.Logging;
using OrderService.Repositories;
using OrderService.Services;

namespace OrderService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddAppLogging();

        // Configuration
        var keycloakSection = builder.Configuration.GetSection("Keycloak");
        var redisSection = builder.Configuration.GetSection("Redis");
        builder.Services.Configure<KeycloakSettings>(keycloakSection);
        builder.Services.Configure<RedisSettings>(redisSection);

        // Redis Cache
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis");
            options.InstanceName = redisSection.Get<RedisSettings>()!.InstanceName;
        });

        // Services
        builder.Services.AddScoped<ICacheService, RedisCacheService>();
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<IOrderService, OrderService.Services.OrderService>();

        // Authorization
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdminRole", policy =>
                policy.RequireRole("admin"));
            options.AddPolicy("RequireUserRole", policy =>
                policy.RequireRole("user"));
        });

        // Authentication
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.Authority = builder.Configuration["Keycloak:Authority"];
                options.Audience = builder.Configuration["Keycloak:Audience"];
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role,
                    ValidIssuer = builder.Configuration["Keycloak:Authority"],
                    ValidAudience = builder.Configuration["Keycloak:Audience"],
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
                                if (parsed.RootElement.TryGetProperty("order-service", out var orderServiceElement) &&
                                    orderServiceElement.TryGetProperty("roles", out var clientRolesElement) &&
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

        // EF Core: SQL Server
        builder.Services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Controllers + Swagger
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
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

        var app = builder.Build();

        // Apply migrations
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            db.Database.Migrate();
        }

        // Middlewares
        app.UseMiddleware<ActivityLogMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.OAuthClientId(keycloakSection.Get<KeycloakSettings>()!.ClientId);
                options.OAuthClientSecret(keycloakSection.Get<KeycloakSettings>()!.ClientSecret);
                options.OAuthUsePkce();
            });
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
