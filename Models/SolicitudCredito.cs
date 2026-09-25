using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaCreditos.Models;

public class SolicitudCredito
{
    public int Id { get; set; }

    [Required]
    public int ClienteId { get; set; }

    [ForeignKey(nameof(ClienteId))]
    public Cliente? Cliente { get; set; }

    [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Monto Solicitado")]
    public decimal MontoSolicitado { get; set; }

    [Display(Name = "Fecha de Solicitud")]
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    [Display(Name = "Estado")]
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [MaxLength(500, ErrorMessage = "El motivo de rechazo no puede exceder los 500 caracteres.")]
    [Display(Name = "Motivo de Rechazo")]
    public string? MotivoRechazo { get; set; }

    /// <summary>
    /// Regla de negocio: No se puede aprobar si el monto supera 5 veces los ingresos mensuales.
    /// </summary>
    public bool CumpleReglaAprobacion5X(decimal ingresosMensuales) =>
        MontoSolicitado <= 5 * ingresosMensuales;

    /// <summary>
    /// Regla de negocio: El monto solicitado no puede superar 10 veces los ingresos mensuales.
    /// </summary>
    public bool CumpleReglaRegistro10X(decimal ingresosMensuales) =>
        MontoSolicitado <= 10 * ingresosMensuales;
}
