using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Hubs;
using PlataformaCreditos.Services;

using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Configuración de reenvío de encabezados para proxies inversos (Render.com)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configuración de SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=app.db";

// Asegurar que el directorio de la base de datos SQLite exista si tiene ruta de carpeta
var dbMatch = System.Text.RegularExpressions.Regex.Match(connectionString, @"Data Source=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
if (dbMatch.Success)
{
    var dbPath = dbMatch.Groups[1].Value.Trim();
    var dir = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
        Directory.CreateDirectory(dir);
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configuración de Identity con soporte de Roles
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// --- CONFIGURACIÓN DE REDIS (SESIÓN + CACHÉ DISTRIBUIDA) ---
var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? builder.Configuration["Redis__ConnectionString"]
    ?? Environment.GetEnvironmentVariable("Redis__ConnectionString");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "PlataformaCreditos_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Sesión respaldada en Redis (usa IDistributedCache registrado arriba)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".PlataformaCreditos.Session";
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ISolicitudesCacheService, SolicitudesCacheService>();

// Servicios de Mensajería Asíncrona Cloud MQ (RabbitMQ)
builder.Services.AddSingleton<IRabbitMqProducer, RabbitMqProducer>();
builder.Services.AddHostedService<RabbitMqConsumerService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

var app = builder.Build();

// Ejecutar migraciones y semilla de datos (Roles, Clientes y Solicitudes)
await DbInitializer.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();
app.UseRouting();

// Middleware de sesión (respaldada en Redis)
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<SolicitudesHub>("/hubs/solicitudes");

app.Run();
