using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.Messages;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PlataformaCreditos.Services;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    public RabbitMqConsumerService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumerService> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Requerimiento: Desactivar el consumidor con RabbitMq__ConsumerEnabled=false para pruebas
        var consumerEnabled = _configuration.GetValue<bool>("RabbitMq:ConsumerEnabled", true);

        if (!consumerEnabled)
        {
            _logger.LogWarning(">>> [RABBITMQ CONSUMER APAGADO] RabbitMq:ConsumerEnabled=false. El consumidor en BackgroundService está inactivo; los mensajes permanecerán encolados en CloudAMQP.");
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);
            }
            return;
        }

        var connectionString = _configuration["RabbitMq:ConnectionString"]
            ?? _configuration["RabbitMq__ConnectionString"]
            ?? Environment.GetEnvironmentVariable("RabbitMq__ConnectionString");

        var queueName = _configuration["RabbitMq:QueueName"]
            ?? _configuration["RabbitMq__QueueName"]
            ?? Environment.GetEnvironmentVariable("RabbitMq__QueueName")
            ?? "solicitudes.notificaciones";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning(">>> [RABBITMQ CONSUMER] No se configuró cadena de conexión. Consumidor no iniciado.");
            return;
        }

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            await using var connection = await factory.CreateConnectionAsync(stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Requerimiento: Declarar cola durable solicitudes.notificaciones
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            // QoS: Procesar de a 1 mensaje a la vez con ACK manual
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var deliveryTag = ea.DeliveryTag;
                var body = ea.Body.ToArray();

                try
                {
                    var json = Encoding.UTF8.GetString(body);
                    var message = JsonSerializer.Deserialize<SolicitudRegistradaMessage>(json);

                    // Requerimiento: Un mensaje inválido debe rechazarse sin reencolar y dejar evidencia en logs
                    if (message == null || message.MessageId == Guid.Empty || string.IsNullOrWhiteSpace(message.UsuarioId))
                    {
                        _logger.LogError(">>> [RABBITMQ REJECT] Mensaje inválido o corrupto recibido. Descartado sin reencolar: {Json}", json);
                        await channel.BasicRejectAsync(deliveryTag, requeue: false);
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Requerimiento: Unicidad de MessageId para que una redelivery no genere duplicados.
                    // Si ya fue procesado, confirmar sin insertar nuevamente.
                    bool yaProcesado = await dbContext.Notificaciones.AnyAsync(n => n.MessageId == message.MessageId);
                    if (yaProcesado)
                    {
                        _logger.LogWarning(">>> [RABBITMQ DEDUPLICADO] MessageId {MessageId} ya existe en SQLite. Confirmando con ACK manual sin duplicar.", message.MessageId);
                        await channel.BasicAckAsync(deliveryTag, multiple: false);
                        return;
                    }

                    // Requerimiento: Guardar en SQLite una Notificacion
                    var notificacion = new Notificacion
                    {
                        MessageId = message.MessageId,
                        SolicitudId = message.SolicitudId,
                        UsuarioId = message.UsuarioId,
                        Texto = !string.IsNullOrWhiteSpace(message.Texto) 
                            ? message.Texto 
                            : "Recibimos tu solicitud de crédito y está pendiente de evaluación.",
                        FechaProcesamientoUtc = DateTime.UtcNow
                    };

                    dbContext.Notificaciones.Add(notificacion);
                    await dbContext.SaveChangesAsync();

                    // Requerimiento: Confirmar el mensaje con ACK manual SOLAMENTE después de guardar la notificación
                    await channel.BasicAckAsync(deliveryTag, multiple: false);
                    _logger.LogInformation(">>> [RABBITMQ MANUAL ACK] Notificación #{NotifId} persistida para Solicitud #{SolicitudId}. ACK exitoso.",
                        notificacion.Id, message.SolicitudId);
                }
                catch (Exception ex)
                {
                    // Requerimiento: Si falla el procesamiento, registrar el error y no confirmar como exitoso. Evitar reintentos infinitos.
                    _logger.LogError(ex, ">>> [RABBITMQ CONSUMER ERROR] Fallo al procesar mensaje con DeliveryTag {DeliveryTag}. Rechazando sin reencolar para evitar bucle infinito.", deliveryTag);
                    await channel.BasicRejectAsync(deliveryTag, requeue: false);
                }
            };

            // Iniciar consumo con autoAck = false (ACK manual obligatorio)
            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation(">>> [RABBITMQ CONSUMER ACTIVO] Escuchando mensajes en cola durable '{Queue}' de CloudAMQP...", queueName);

            // Mantener el BackgroundService vivo
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [RABBITMQ CONSUMER FATAL] Error crítico de conexión al broker CloudAMQP.");
        }
    }
}
