using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.ViewModels;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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

        // Si hay errores de validación en los filtros, mostramos los errores y la lista vacía para corrección
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        // Construcción de la consulta con filtros
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

        viewModel.Solicitudes = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

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

        return View(solicitud);
    }
}
