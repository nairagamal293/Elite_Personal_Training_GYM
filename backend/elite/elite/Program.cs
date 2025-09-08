using elite.Data;
using elite.Interfaces;
using elite.Middleware;
using elite.Services;
using elite.Utilities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "wwwroot" // Set web root path here
});

// Add services to the container.
builder.Services.AddControllers();

// Add DbContext
builder.Services.AddDbContext<GymDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Add services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<IOnlineSessionService, OnlineSessionService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<ITrainerService, TrainerService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IMembershipTypeService, MembershipTypeService>();
builder.Services.AddScoped<IImageService, ImageService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Secret"]);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateLifetime = true
    };
});

// Add Authorization
builder.Services.AddAuthorization();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Gym Booking API",
        Version = "v1",
        Description = "Women's Gym Booking System API",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@gymbooking.com",
            Url = new Uri("https://gymbooking.com/support")
        },
        License = new OpenApiLicense
        {
            Name = "Use under LICX",
            Url = new Uri("https://example.com/license")
        }
    });

    // Use full name as Schema ID to avoid naming conflicts
    options.CustomSchemaIds(type => type.FullName);

    // Add JWT authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
            new string[] {}
        }
    });

    // Include XML comments if they exist
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Add detailed logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gym Booking API v1");
        c.RoutePrefix = "swagger";
    });
}

// Serve static files (including uploaded images)
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Use custom exception middleware
app.UseMiddleware<ExceptionMiddleware>();

// Check database connectivity
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<GymDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Try to connect to the database
        await context.Database.CanConnectAsync();
        logger.LogInformation("Database connection successful");

        // Ensure database is created
        context.Database.EnsureCreated();

        // Apply any pending migrations
        await context.Database.MigrateAsync();

        // Seed data
        var seeder = new DataSeeder(context);
        await seeder.SeedDataAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database connection or migration failed");
    }
}

app.MapControllers();

app.Run();