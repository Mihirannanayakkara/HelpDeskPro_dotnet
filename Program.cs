using System.Text;
using System.Text.Json.Serialization;
using HelpDeskPro.Data;
using HelpDeskPro.Middleware;
using HelpDeskPro.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services (everything here goes BEFORE Build) ----------

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();

// CORS: which websites may call this API from a browser
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
              ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(o => o.AddPolicy("web", p => p
    .WithOrigins(origins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ICommentService, CommentService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing. Set it with: dotnet user-secrets set \"Jwt:Key\" \"...\"");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var userId = ctx.Principal!.GetUserId();
                var isActive = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId).Select(u => u.IsActive).FirstOrDefaultAsync();
                if (!isActive) ctx.Fail("User is inactive or no longer exists.");
            }
        };
    });

builder.Services.AddAuthorization();

// ---------- Build ----------

var app = builder.Build();

// ---------- Startup work: create/update database, create admin ----------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAdminAsync(scope.ServiceProvider, app.Configuration);
}

// ---------- Pipeline (order matters) ----------

app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableDocs"))
{
    app.MapOpenApi();
}

// No UseHttpsRedirection here: in Docker/hosting, the platform handles HTTPS in front of the app.

app.UseCors("web");          // must come BEFORE authentication
app.UseAuthentication();     // who are you?
app.UseAuthorization();      // are you allowed?

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();