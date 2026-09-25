using PlataformaCreditos.Models;

namespace PlataformaCreditos.Services;

public interface ISolicitudesCacheService
{
    Task<List<SolicitudCredito>?> GetSolicitudesUsuarioAsync(string usuarioId, Cliente? cliente = null);
    Task SetSolicitudesUsuarioAsync(string usuarioId, List<SolicitudCredito> solicitudes, TimeSpan? duracion = null);
    Task InvalidateSolicitudesUsuarioAsync(string usuarioId);
    Task InvalidateSolicitudesPorClienteIdAsync(int clienteId);
}
