using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models.ViewModels;

public class CrearSolicitudViewModel
{
    [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Display(Name = "Monto Solicitado")]
    public decimal MontoSolicitado { get; set; }

    // Propiedades de contexto para visualización y validación
    public decimal IngresosMensuales { get; set; }
    public decimal LimiteMaximo10X => IngresosMensuales * 10;
    public bool ClienteActivo { get; set; } = true;
    public bool TieneSolicitudPendiente { get; set; }

    // Mensajes de retroalimentación en la misma vista
    public string? MensajeExito { get; set; }
    public string? MensajeError { get; set; }
    public int? SolicitudIdCreada { get; set; }
}
