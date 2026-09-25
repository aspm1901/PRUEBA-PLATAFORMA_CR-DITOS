using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Aplicar migraciones si faltan
        await context.Database.MigrateAsync();

        // 1. Roles requeridos
        string[] roles = ["Analista", "Cliente"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Usuario Analista
        const string analistaEmail = "analista@creditos.com";
        var analistaUser = await userManager.FindByEmailAsync(analistaEmail);
        if (analistaUser == null)
        {
            analistaUser = new IdentityUser
            {
                UserName = analistaEmail,
                Email = analistaEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(analistaUser, "Password123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(analistaUser, "Analista");
            }
        }

        // 3. Cliente 1
        const string cliente1Email = "cliente1@creditos.com";
        var cliente1User = await userManager.FindByEmailAsync(cliente1Email);
        if (cliente1User == null)
        {
            cliente1User = new IdentityUser
            {
                UserName = cliente1Email,
                Email = cliente1Email,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(cliente1User, "Password123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(cliente1User, "Cliente");
            }
        }

        var cliente1 = await context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == cliente1User!.Id);
        if (cliente1 == null && cliente1User != null)
        {
            cliente1 = new Cliente
            {
                UsuarioId = cliente1User.Id,
                IngresosMensuales = 3000.00m,
                Activo = true
            };
            context.Clientes.Add(cliente1);
            await context.SaveChangesAsync();
        }

        // 4. Cliente 2
        const string cliente2Email = "cliente2@creditos.com";
        var cliente2User = await userManager.FindByEmailAsync(cliente2Email);
        if (cliente2User == null)
        {
            cliente2User = new IdentityUser
            {
                UserName = cliente2Email,
                Email = cliente2Email,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(cliente2User, "Password123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(cliente2User, "Cliente");
            }
        }

        var cliente2 = await context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == cliente2User!.Id);
        if (cliente2 == null && cliente2User != null)
        {
            cliente2 = new Cliente
            {
                UsuarioId = cliente2User.Id,
                IngresosMensuales = 5000.00m,
                Activo = true
            };
            context.Clientes.Add(cliente2);
            await context.SaveChangesAsync();
        }

        // 5. Solicitudes de Crédito iniciales
        // Al menos 2 solicitudes: una Pendiente y una Aprobada
        if (!await context.SolicitudesCredito.AnyAsync())
        {
            if (cliente1 != null)
            {
                context.SolicitudesCredito.Add(new SolicitudCredito
                {
                    ClienteId = cliente1.Id,
                    MontoSolicitado = 10000.00m,
                    FechaSolicitud = DateTime.UtcNow.AddDays(-2),
                    Estado = EstadoSolicitud.Pendiente
                });
            }

            if (cliente2 != null)
            {
                context.SolicitudesCredito.Add(new SolicitudCredito
                {
                    ClienteId = cliente2.Id,
                    MontoSolicitado = 15000.00m,
                    FechaSolicitud = DateTime.UtcNow.AddDays(-5),
                    Estado = EstadoSolicitud.Aprobado
                });
            }

            await context.SaveChangesAsync();
        }
    }
}
