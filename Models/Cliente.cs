using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PlataformaCreditos.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [ForeignKey(nameof(UsuarioId))]
    public IdentityUser? Usuario { get; set; }

    [Required(ErrorMessage = "Los ingresos mensuales son obligatorios.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Los ingresos mensuales deben ser mayores a 0.")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Ingresos Mensuales")]
    public decimal IngresosMensuales { get; set; }

    [Display(Name = "Activo")]
    public bool Activo { get; set; } = true;

    public ICollection<SolicitudCredito> Solicitudes { get; set; } = new List<SolicitudCredito>();
}
