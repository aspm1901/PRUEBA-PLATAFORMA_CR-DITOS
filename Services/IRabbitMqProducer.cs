using PlataformaCreditos.Models.Messages;

namespace PlataformaCreditos.Services;

public interface IRabbitMqProducer
{
    Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistradaMessage mensaje);
}
