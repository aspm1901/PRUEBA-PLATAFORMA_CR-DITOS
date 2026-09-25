using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.ViewModels;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ISolicitudesCacheService _cacheService;

    public SolicitudesController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ISolicitudesCacheService cacheService)
    {
        _context = context;
        _userManager = userManager;
        _cacheService = cacheService;
    }

    // GET: Solicitudes/MisSolicitudes
    [HttpGet]
    public async Task<IActionResult> MisSolicitudes(
        EstadoSolicitud? estado,
        decimal? montoMin,
        decimal? montoMax,
        DateTime? fechaInicio,
        DateTime? fechaFin)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == user.Id);

        var viewModel = new MisSolicitudesViewModel
        {
            Estado = estado,
            MontoMin = montoMin,
            MontoMax = montoMax,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            Cliente = cliente
        };

        if (cliente == null)
        {
            ViewBag.InfoMessage = "No tienes un perfil de cliente registrado actualmente.";
            return View(viewModel);
        }

        // --- VALIDACIONES SERVER-SIDE DE FILTROS ---
        if (montoMin.HasValue && montoMin.Value < 0)
        {
            ModelState.AddModelError("MontoMin", "El monto mínimo no puede ser negativo.");
        }

        if (montoMax.HasValue && montoMax.Value < 0)
        {
            ModelState.AddModelError("MontoMax", "El monto máximo no puede ser negativo.");
        }

        if (montoMin.HasValue && montoMax.HasValue && montoMin.Value > montoMax.Value)
        {
            ModelState.AddModelError("MontoMax", "El monto máximo debe ser mayor o igual al monto mínimo.");
        }

        if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio.Value.Date > fechaFin.Value.Date)
        {
            ModelState.AddModelError("FechaFin", "La fecha de inicio no puede ser posterior a la fecha de fin.");
        }

        // Si hay errores de validación en los filtros, mostramos los errores
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        bool tieneFiltros = estado.HasValue || montoMin.HasValue || montoMax.HasValue || fechaInicio.HasValue || fechaFin.HasValue;

        // --- MANEJO DE CACHÉ REDIS (60 SEGUNDOS) ---
        // Si no hay filtros aplicados, consultamos/guardamos en la caché de Redis
        if (!tieneFiltros)
        {
            var cachedSolicitudes = await _cacheService.GetSolicitudesUsuarioAsync(user.Id, cliente);
            if (cachedSolicitudes != null)
            {
                viewModel.Solicitudes = cachedSolicitudes;
                ViewBag.OrigenDatos = "Redis Cache (TTL: 60s)";
                return View(viewModel);
            }
        }

        // Consulta directa a Base de Datos
        IQueryable<SolicitudCredito> query = _context.SolicitudesCredito
            .Where(s => s.ClienteId == cliente.Id);

        if (estado.HasValue)
        {
            query = query.Where(s => s.Estado == estado.Value);
        }

        if (montoMin.HasValue)
        {
            query = query.Where(s => s.MontoSolicitado >= montoMin.Value);
        }

        if (montoMax.HasValue)
        {
            query = query.Where(s => s.MontoSolicitado <= montoMax.Value);
        }

        if (fechaInicio.HasValue)
        {
            var inicio = fechaInicio.Value.Date;
            query = query.Where(s => s.FechaSolicitud >= inicio);
        }

        if (fechaFin.HasValue)
        {
            var fin = fechaFin.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(s => s.FechaSolicitud <= fin);
        }

        var listaDb = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        // Si no tiene filtros, guardamos en la caché de Redis por 60s
        if (!tieneFiltros)
        {
            await _cacheService.SetSolicitudesUsuarioAsync(user.Id, listaDb, TimeSpan.FromSeconds(60));
            ViewBag.OrigenDatos = "Base de Datos SQLite (Guardado en Redis por 60s)";
        }
        else
        {
            ViewBag.OrigenDatos = "Consulta Filtrada en Base de Datos";
        }

        viewModel.Solicitudes = listaDb;
        return View(viewModel);
    }

    // GET: Solicitudes/Detalle/5
    [HttpGet]
    public async Task<IActionResult> Detalle(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
            .FirstOrDefaultAsync(s => s.Id == id.Value);

        if (solicitud == null)
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(User);
        var esAnalista = User.IsInRole("Analista");

        // Seguridad: Un cliente solo puede ver sus propias solicitudes; un analista puede ver cualquiera
        if (!esAnalista && solicitud.Cliente?.UsuarioId != user?.Id)
        {
            return Forbid();
        }

        // --- SESIÓN REDIS-BACKED ---
        // Guardar la última solicitud visitada en la sesión distribuida
        HttpContext.Session.SetInt32("UltimaSolicitudId", solicitud.Id);
        HttpContext.Session.SetString("UltimaSolicitudMonto", solicitud.MontoSolicitado.ToString("C"));

        return View(solicitud);
    }

    // GET: Solicitudes/Crear
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == user.Id);

        if (cliente == null)
        {
            var vmEmpty = new CrearSolicitudViewModel
            {
                MensajeError = "No tienes un perfil de cliente registrado en el sistema."
            };
            return View(vmEmpty);
        }

        bool tienePendiente = await _context.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        var viewModel = new CrearSolicitudViewModel
        {
            IngresosMensuales = cliente.IngresosMensuales,
            ClienteActivo = cliente.Activo,
            TieneSolicitudPendiente = tienePendiente
        };

        if (!cliente.Activo)
        {
            viewModel.MensajeError = "Tu cuenta de cliente se encuentra inactiva. No puedes registrar solicitudes.";
        }
        else if (tienePendiente)
        {
            viewModel.MensajeError = "Actualmente ya tienes una solicitud de crédito en estado Pendiente. Debes esperar a que sea evaluada.";
        }

        return View(viewModel);
    }

    // POST: Solicitudes/Crear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearSolicitudViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == user.Id);

        if (cliente == null)
        {
            model.MensajeError = "No tienes un perfil de cliente registrado en el sistema.";
            return View(model);
        }

        model.IngresosMensuales = cliente.IngresosMensuales;
        model.ClienteActivo = cliente.Activo;

        // --- VALIDACIONES SERVER-SIDE DE NEGOCIO ---
        // 1. Cliente debe estar activo
        if (!cliente.Activo)
        {
            ModelState.AddModelError(string.Empty, "Tu cuenta de cliente se encuentra inactiva. No puedes solicitar créditos.");
        }

        // 2. No permitir más de una solicitud Pendiente por cliente
        bool tienePendiente = await _context.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        model.TieneSolicitudPendiente = tienePendiente;
        if (tienePendiente)
        {
            ModelState.AddModelError(string.Empty, "Ya posees una solicitud en estado Pendiente. El sistema solo permite una solicitud pendiente activa por cliente.");
        }

        // 3. Monto mayor a 0
        if (model.MontoSolicitado <= 0)
        {
            ModelState.AddModelError(nameof(model.MontoSolicitado), "El monto solicitado debe ser mayor a 0.");
        }

        // 4. El monto solicitado no puede superar 10 veces los ingresos mensuales
        decimal limite10X = cliente.IngresosMensuales * 10;
        if (model.MontoSolicitado > limite10X)
        {
            ModelState.AddModelError(nameof(model.MontoSolicitado), 
                $"El monto solicitado ({model.MontoSolicitado:C}) supera el límite permitido de 10 veces tus ingresos mensuales ({limite10X:C}).");
        }

        if (!ModelState.IsValid)
        {
            model.MensajeError = "No se pudo registrar la solicitud debido a inconsistencias con las reglas de negocio.";
            return View(model);
        }

        // Crear la entidad en estado Pendiente
        var nuevaSolicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = model.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };

        _context.SolicitudesCredito.Add(nuevaSolicitud);
        await _context.SaveChangesAsync();

        // --- INVALIDACIÓN DE CACHÉ EN REDIS ---
        // Al registrarse una nueva solicitud, se invalida la caché del listado del usuario
        await _cacheService.InvalidateSolicitudesUsuarioAsync(user.Id);

        // Feedback claro en la misma vista (éxito)
        model.MensajeExito = $"¡Solicitud #{nuevaSolicitud.Id} registrada exitosamente por un monto de {nuevaSolicitud.MontoSolicitado:C}! Su estado actual es: Pendiente de Evaluación.";
        model.MensajeError = null;
        model.SolicitudIdCreada = nuevaSolicitud.Id;
        model.TieneSolicitudPendiente = true;
        model.MontoSolicitado = 0;

        return View(model);
    }

    // GET: /Solicitudes/ObtenerEstadosVigentes (Para reconexión WebSocket)
    [HttpGet]
    public async Task<IActionResult> ObtenerEstadosVigentes()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == user.Id);

        if (cliente == null)
        {
            return Json(new List<object>());
        }

        var estados = await _context.SolicitudesCredito
            .Where(s => s.ClienteId == cliente.Id)
            .Select(s => new
            {
                id = s.Id,
                estado = s.Estado.ToString(),
                motivoRechazo = s.MotivoRechazo
            })
            .ToListAsync();

        return Json(estados);
    }
}
