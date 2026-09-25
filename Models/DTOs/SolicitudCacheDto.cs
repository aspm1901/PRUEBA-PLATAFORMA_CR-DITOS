namespace PlataformaCreditos.Models.DTOs;

public class SolicitudCacheDto
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public decimal MontoSolicitado { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoSolicitud Estado { get; set; }
    public string? MotivoRechazo { get; set; }

    public static SolicitudCacheDto FromEntity(SolicitudCredito s) => new()
    {
        Id = s.Id,
        ClienteId = s.ClienteId,
        MontoSolicitado = s.MontoSolicitado,
        FechaSolicitud = s.FechaSolicitud,
        Estado = s.Estado,
        MotivoRechazo = s.MotivoRechazo
    };

    public SolicitudCredito ToEntity(Cliente? cliente = null) => new()
    {
        Id = Id,
        ClienteId = ClienteId,
        MontoSolicitado = MontoSolicitado,
        FechaSolicitud = FechaSolicitud,
        Estado = Estado,
        MotivoRechazo = MotivoRechazo,
        Cliente = cliente
    };
}
