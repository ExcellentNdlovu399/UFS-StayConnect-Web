using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------
// DATABASE
// -----------------------------------------

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));


// -----------------------------------------
// IDENTITY
// -----------------------------------------

builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("IdentityConnection")));


builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password requirements
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;

    // User requirements
    options.User.RequireUniqueEmail = true;

    // Account lockout
    options.Lockout.DefaultLockoutTimeSpan =
        TimeSpan.FromMinutes(10);

    options.Lockout.MaxFailedAccessAttempts = 5;

})
.AddEntityFrameworkStores<ApplicationIdentityDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!)),

            ValidateLifetime = true,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddScoped<IRepositoryWrapper, RepositoryWrapper>();
builder.Services.AddHostedService<UFSStayConnect.Services.PaymentReminderService>();


builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});


// -----------------------------------------
// MVC
// -----------------------------------------

builder.Services.AddControllersWithViews();


// -----------------------------------------
// SESSION
// -----------------------------------------

builder.Services.AddSession();


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    await DatabaseInitializer.InitializeAsync(
        app.Configuration,
        app.Environment);

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;

        var identityDb =
            services.GetRequiredService<ApplicationIdentityDbContext>();

        await identityDb.Database.MigrateAsync();

        var appDb =
            services.GetRequiredService<AppDbContext>();

        await appDb.Database.MigrateAsync();

        await SeedIdentityData.SeedRolesAsync(services);
        await SeedData.InitializeAsync(services);
    }
}



// -----------------------------------------
// MIDDLEWARE
// -----------------------------------------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();


// -----------------------------------------
// MVC ROUTING
// -----------------------------------------

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();