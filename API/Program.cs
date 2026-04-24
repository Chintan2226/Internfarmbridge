using System.Text;
using API.BAL;
using API.Models.Settings;
using API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Npgsql;
using StackExchange.Redis;
using QuestPDF.Infrastructure;   // ✅ Added

AppContext.SetSwitch("System.Net.DisableIPv6", true);
var builder = WebApplication.CreateBuilder(args);

// ✅ QuestPDF License (Fix Warning Message)
QuestPDF.Settings.License = LicenseType.Community;

// All helper injection
builder.Services.AddScoped<NotificationHelper>();
builder.Services.AddScoped<AdminHelper>();
builder.Services.AddScoped<AuthHelper>();
builder.Services.AddScoped<FarmerHelper>();
builder.Services.AddScoped<FieldOfficerHelper>();
builder.Services.AddScoped<VendorHelper>(); 
builder.Services.AddScoped<StaffAuthHelper>();
builder.Services.AddScoped<GoogleAuthBal>();

// Service Injection
builder.Services.AddScoped<RedisService>();
builder.Services.AddScoped<RabbitMqService>();
builder.Services.AddHostedService<NotificationConsumer>();
builder.Services.AddScoped<CloudinaryService>();
builder.Services.AddScoped<ElasticService>();
builder.Services.AddHttpClient<AiInventoryService>();


//Email
builder.Services.Configure<API.Models.Settings.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<API.Services.EmailService>();
builder.Services.AddHttpClient<SerpApiService>();


// JWT services add
builder.Services.AddScoped<JwtService>();

// ✅ 1. Controllers
builder.Services.AddControllers();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddEndpointsApiExplorer();

// ✅ 2. Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

    c.AddSecurityDefinition(
        "token",
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "Bearer",
            In = ParameterLocation.Header,
            Name = HeaderNames.Authorization,
        }
    );

    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "token",
                    },
                },
                Array.Empty<string>()
            },
        }
    );
});

builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("Cloudinary")
);

// ✅ 3. PostgreSQL (Npgsql)
builder.Services.AddScoped<NpgsqlConnection>(opt =>
{
    var conn = opt.GetRequiredService<IConfiguration>()
        .GetConnectionString("DefaultConnection");
    return new NpgsqlConnection(conn);
});

// ✅ 4. Redis
var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisConnectionString)
);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "FarmBridge_";
});

builder.Services.AddSingleton<IDatabase>(sp =>
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase()
);

// ✅ 5. CORS
builder.Services.AddCors(p =>
    p.AddPolicy(
        "corsapp",
        policy =>
        {
            policy.WithOrigins("*")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    )
);

builder.Services.AddScoped<RazorpayService>();

// ✅ 6. JWT Authentication
builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(
                        builder.Configuration["Jwt:Key"]
                    )
                ),
            };
    });


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}
app.UseCors("corsapp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();