using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlataformaCreditos.Hubs;

[Authorize]
public class SolicitudesHub : Hub
{
    private readonly ILogger<SolicitudesHub> _logger;

    public SolicitudesHub(ILogger<SolicitudesHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation(">>> [WEBSOCKET CONNECTED] Usuario autenticado {UserId} conectado al Hub (ConnectionId: {ConnId}).",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation(">>> [WEBSOCKET DISCONNECTED] Usuario {UserId} desconectado (ConnectionId: {ConnId}).",
            userId, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
