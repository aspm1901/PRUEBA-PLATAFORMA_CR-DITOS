using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Hubs;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.Messages;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISolicitudesCacheService _cacheService;
    private readonly IHubContext<SolicitudesHub> _hubContext;
    private readonly IRabbitMqProducer _rabbitMqProducer;
    private readonly ILogger<AnalistaController> _logger;

    public AnalistaController(
        ApplicationDbContext context,
        ISolicitudesCacheService cacheService,
        IHubContext<SolicitudesHub> hubContext,
        IRabbitMqProducer rabbitMqProducer,
        ILogger<AnalistaController> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _hubContext = hubContext;
        _rabbitMqProducer = rabbitMqProducer;
        _logger = logger;
    }

    // GET: /Analista
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var solicitudesPendientes = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        return View(solicitudesPendientes);
    }

    // POST: /Analista/Aprobar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            TempData["Error"] = $"No se encontró la solicitud con ID #{id}.";
            return RedirectToAction(nameof(Index));
        }

        // Validación 1: No procesar solicitudes ya aprobadas o rechazadas
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue procesada anteriormente y se encuentra en estado '{solicitud.Estado}'.";
            return RedirectToAction(nameof(Index));
        }

        // Validación 2: No aprobar si el monto solicitado excede 5 veces los ingresos mensuales
        decimal ingresos = solicitud.Cliente?.IngresosMensuales ?? 0;
        decimal limite5X = ingresos * 5;

        if (solicitud.MontoSolicitado > limite5X)
        {
            TempData["Error"] = $"RECHAZO DE APROBACIÓN: La solicitud #{id} no puede aprobarse porque el monto solicitado ({solicitud.MontoSolicitado:C}) supera 5 veces los ingresos mensuales del cliente ({limite5X:C}).";
            _logger.LogWarning("Intento de aprobación fallido para solicitud #{Id}: monto excede 5x ingresos.", id);
            return RedirectToAction(nameof(Index));
        }

        // 1. Guardar primero el estado en la base de datos
        solicitud.Estado = EstadoSolicitud.Aprobado;
        solicitud.MotivoRechazo = null;
        await _context.SaveChangesAsync();

        // 2. Invalidar su caché Redis
        if (solicitud.Cliente != null && !string.IsNullOrEmpty(solicitud.Cliente.UsuarioId))
        {
            await _cacheService.InvalidateSolicitudesUsuarioAsync(solicitud.Cliente.UsuarioId);
        }

        // 3. Emitir el evento SolicitudEstadoActualizado ÚNICAMENTE al usuario propietario (resuelto en servidor)
        if (solicitud.Cliente != null && !string.IsNullOrEmpty(solicitud.Cliente.UsuarioId))
        {
            await _hubContext.Clients.User(solicitud.Cliente.UsuarioId).SendAsync(
                "SolicitudEstadoActualizado",
                new
                {
                    solicitudId = solicitud.Id,
                    estado = "Aprobado",
                    motivoRechazo = (string?)null
                });

            _logger.LogInformation(">>> [WEBSOCKET EMIT] Notificación enviada al usuario propietario {UserId} para solicitud #{Id}",
                solicitud.Cliente.UsuarioId, solicitud.Id);
        }

        // 4. Publicar evento en CloudAMQP para la bandeja de notificaciones del cliente
        if (solicitud.Cliente != null && !string.IsNullOrEmpty(solicitud.Cliente.UsuarioId))
        {
            await _rabbitMqProducer.PublicarSolicitudRegistradaAsync(new SolicitudRegistradaMessage
            {
                MessageId = Guid.NewGuid(),
                SolicitudId = solicitud.Id,
                UsuarioId = solicitud.Cliente.UsuarioId,
                FechaEventoUtc = DateTime.UtcNow,
                Texto = $"¡Felicidades! Tu solicitud de crédito #{solicitud.Id} por {solicitud.MontoSolicitado:C} ha sido Aprobada."
            });
        }

        TempData["Success"] = $"¡Solicitud #{id} aprobada con éxito por un monto de {solicitud.MontoSolicitado:C}!";
        _logger.LogInformation("Solicitud #{Id} aprobada por analista {User}.", id, User.Identity?.Name);

        return RedirectToAction(nameof(Index));
    }

    // POST: /Analista/Rechazar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            TempData["Error"] = $"No se encontró la solicitud con ID #{id}.";
            return RedirectToAction(nameof(Index));
        }

        // Validación 1: No procesar solicitudes ya aprobadas o rechazadas
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue procesada anteriormente (Estado actual: '{solicitud.Estado}').";
            return RedirectToAction(nameof(Index));
        }

        // Validación 2: Motivo obligatorio en rechazo
        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            TempData["Error"] = $"ERROR: Debe ingresar obligatoriamente un motivo para rechazar la solicitud #{id}.";
            return RedirectToAction(nameof(Index));
        }

        // 1. Guardar primero el estado en la base de datos
        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo.Trim();
        await _context.SaveChangesAsync();

        // 2. Invalidar su caché Redis
        if (solicitud.Cliente != null && !string.IsNullOrEmpty(solicitud.Cliente.UsuarioId))
        {
            await _cacheService.InvalidateSolicitudesUsuarioAsync(solicitud.Cliente.UsuarioId);
        }

        // 3. Emitir el evento SolicitudEstadoActualizado ÚNICAMENTE al usuario propietario (resuelto en servidor)
        if (solicitud.Cliente != null && !string.IsNullOrEmpty(solicitud.Cliente.UsuarioId))
        {
            await _hubContext.Clients.User(solicitud.Cliente.UsuarioId).SendAsync(
                "SolicitudEstadoActualizado",
                new
                {
                    solicitudId = solicitud.Id,
                    estado = "Rechazado",
                    motivoRechazo = solicitud.MotivoRechazo
                });

            _logger.LogInformation(">>> [WEBSOCKET EMIT] Notificación enviada al usuario propietario {UserId} para solicitud #{Id}",
                solicitud.Cliente.UsuarioId, solicitud.Id);
        }

        // 4. Publicar evento en CloudAMQP para la bandeja de notificaciones del cliente
        if (solicitud.Cliente != null && !string.IsNullOrEmpty(solicitud.Cliente.UsuarioId))
        {
            await _rabbitMqProducer.PublicarSolicitudRegistradaAsync(new SolicitudRegistradaMessage
            {
                MessageId = Guid.NewGuid(),
                SolicitudId = solicitud.Id,
                UsuarioId = solicitud.Cliente.UsuarioId,
                FechaEventoUtc = DateTime.UtcNow,
                Texto = $"Tu solicitud de crédito #{solicitud.Id} ha sido Rechazada. Motivo: {solicitud.MotivoRechazo}."
            });
        }

        TempData["Success"] = $"Solicitud #{id} rechazada correctamente. Motivo: {motivoRechazo}.";
        _logger.LogInformation("Solicitud #{Id} rechazada por analista {User}. Motivo: {Motivo}", id, User.Identity?.Name, motivoRechazo);

        return RedirectToAction(nameof(Index));
    }
}
