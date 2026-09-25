using System.Text;
using System.Text.Json;
using PlataformaCreditos.Models.Messages;
using RabbitMQ.Client;

namespace PlataformaCreditos.Services;

public class RabbitMqProducer : IRabbitMqProducer
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqProducer> _logger;

    public RabbitMqProducer(IConfiguration configuration, ILogger<RabbitMqProducer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistradaMessage mensaje)
    {
        var connectionString = _configuration["RabbitMq:ConnectionString"]
            ?? _configuration["RabbitMq__ConnectionString"]
            ?? Environment.GetEnvironmentVariable("RabbitMq__ConnectionString");

        var queueName = _configuration["RabbitMq:QueueName"]
            ?? _configuration["RabbitMq__QueueName"]
            ?? Environment.GetEnvironmentVariable("RabbitMq__QueueName")
            ?? "solicitudes.notificaciones";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning(">>> [RABBITMQ PRODUCER] No hay cadena de conexión configurada para RabbitMQ. Mensaje {MessageId} no enviado.", mensaje.MessageId);
            return false;
        }

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            await using var connection = await factory.CreateConnectionAsync();

            // Requerimiento: Usar confirmación del publicador para verificar aceptación por el broker
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true
            );
            await using var channel = await connection.CreateChannelAsync(channelOptions);

            // Requerimiento: Declarar la cola durable solicitudes.notificaciones
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // Requerimiento: Mensaje JSON persistente con MessageId (UUID), SolicitudId, UsuarioId y FechaEventoUtc
            var json = JsonSerializer.Serialize(mensaje);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent,
                ContentType = "application/json",
                MessageId = mensaje.MessageId.ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            // Publicar con Publisher Confirm
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body
            );

            _logger.LogInformation(">>> [RABBITMQ PUBLISHER CONFIRM OK] Mensaje {MessageId} encolado en '{Queue}' para Solicitud #{SolicitudId}.",
                mensaje.MessageId, queueName, mensaje.SolicitudId);

            return true;
        }
        catch (Exception ex)
        {
            // Requerimiento: Ante una falla de publicación, conservar la solicitud, registrar el error y advertir
            _logger.LogError(ex, ">>> [RABBITMQ PUBLISHER ERROR] Falla al conectar o publicar en CloudAMQP para Solicitud #{SolicitudId} (MessageId: {MessageId}). La solicitud permanece en BD.",
                mensaje.SolicitudId, mensaje.MessageId);
            return false;
        }
    }
}
