using System.Text;
using HelpDeskPro.Data;
using HelpDeskPro.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using HelpDeskPro.Middleware;

var builder = WebApplication.CreateBuilder(args);

// replace: builder.Services.AddControllers();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
    new JsonStringEnumConverter(allowIntegerValues: false)));
// (this lets the API send and accept "High" instead of the number 2)

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();   // add this line
builder.Services.AddHealthChecks();    

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITicketService, TicketService>();   // add this line
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IAuthService, AuthService>();

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
builder.Services.AddScoped<ICommentService, CommentService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAdminAsync(scope.ServiceProvider, app.Configuration);
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();   // who are you?   (must come first)
app.UseAuthorization();    // are you allowed? (then this)
app.MapControllers();
app.MapHealthChecks("/health"); 
app.Run();