using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models.ViewModels;

public class MisSolicitudesViewModel
{
    public List<SolicitudCredito> Solicitudes { get; set; } = new();

    public EstadoSolicitud? Estado { get; set; }

    [Display(Name = "Monto Mínimo")]
    public decimal? MontoMin { get; set; }

    [Display(Name = "Monto Máximo")]
    public decimal? MontoMax { get; set; }

    [Display(Name = "Fecha Inicio")]
    [DataType(DataType.Date)]
    public DateTime? FechaInicio { get; set; }

    [Display(Name = "Fecha Fin")]
    [DataType(DataType.Date)]
    public DateTime? FechaFin { get; set; }

    public Cliente? Cliente { get; set; }
}
