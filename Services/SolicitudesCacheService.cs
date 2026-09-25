using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.DTOs;

namespace PlataformaCreditos.Services;

public class SolicitudesCacheService : ISolicitudesCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SolicitudesCacheService> _logger;

    public SolicitudesCacheService(
        IDistributedCache cache,
        ApplicationDbContext context,
        ILogger<SolicitudesCacheService> logger)
    {
        _cache = cache;
        _context = context;
        _logger = logger;
    }

    private static string GetCacheKey(string usuarioId) => $"solicitudes_user_{usuarioId}";

    public async Task<List<SolicitudCredito>?> GetSolicitudesUsuarioAsync(string usuarioId, Cliente? cliente = null)
    {
        try
        {
            var cacheKey = GetCacheKey(usuarioId);
            var cachedJson = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedJson))
            {
                _logger.LogInformation(">>> [REDIS CACHE HIT] Recuperadas solicitudes del usuario {UserId} desde Redis Cache.", usuarioId);
                var dtos = JsonSerializer.Deserialize<List<SolicitudCacheDto>>(cachedJson);
                if (dtos != null)
                {
                    return dtos.Select(d => d.ToEntity(cliente)).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar la caché de Redis. Se continuará con la base de datos.");
        }

        _logger.LogInformation(">>> [REDIS CACHE MISS] No se encontraron solicitudes cacheadas para el usuario {UserId}.", usuarioId);
        return null;
    }

    public async Task SetSolicitudesUsuarioAsync(string usuarioId, List<SolicitudCredito> solicitudes, TimeSpan? duracion = null)
    {
        try
        {
            var cacheKey = GetCacheKey(usuarioId);
            var dtos = solicitudes.Select(SolicitudCacheDto.FromEntity).ToList();
            var json = JsonSerializer.Serialize(dtos);

            var options = new DistributedCacheEntryOptions
            {
                // Regla obligatoria: Cachear por 60s
                AbsoluteExpirationRelativeToNow = duracion ?? TimeSpan.FromSeconds(60)
            };

            await _cache.SetStringAsync(cacheKey, json, options);
            _logger.LogInformation(">>> [REDIS CACHE SET] Cacheadas {Count} solicitudes del usuario {UserId} por 60s.", dtos.Count, usuarioId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al escribir en la caché de Redis.");
        }
    }

    public async Task InvalidateSolicitudesUsuarioAsync(string usuarioId)
    {
        try
        {
            var cacheKey = GetCacheKey(usuarioId);
            await _cache.RemoveAsync(cacheKey);
            _logger.LogInformation(">>> [REDIS CACHE INVALIDATED] Invalidada caché de solicitudes del usuario {UserId}.", usuarioId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invalidar la caché de Redis para el usuario {UserId}.", usuarioId);
        }
    }

    public async Task InvalidateSolicitudesPorClienteIdAsync(int clienteId)
    {
        try
        {
            var cliente = await _context.Clientes.FindAsync(clienteId);
            if (cliente != null && !string.IsNullOrEmpty(cliente.UsuarioId))
            {
                await InvalidateSolicitudesUsuarioAsync(cliente.UsuarioId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invalidar la caché por ClienteId {ClienteId}.", clienteId);
        }
    }
}
