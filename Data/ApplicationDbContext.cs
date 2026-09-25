using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cliente>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Cliente_IngresosMensuales", "IngresosMensuales > 0");
            });

            entity.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SolicitudCredito>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Solicitud_MontoSolicitado", "MontoSolicitado > 0");
            });

            entity.HasOne(s => s.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restricción: Un cliente solo puede tener una solicitud en estado Pendiente (0 = Pendiente)
            entity.HasIndex(s => new { s.ClienteId, s.Estado })
                .HasFilter("Estado = 0")
                .IsUnique();
        });

        // Notificacion con unicidad de MessageId para deduplicación e idempotencia
        builder.Entity<Notificacion>(entity =>
        {
            entity.HasIndex(n => n.MessageId)
                .IsUnique();
        });
    }
}
