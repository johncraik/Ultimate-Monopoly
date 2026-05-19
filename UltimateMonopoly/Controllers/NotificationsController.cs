using JC.Communication.Notifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UltimateMonopoly.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationManager _notifications;

    public NotificationsController(INotificationManager notifications)
    {
        _notifications = notifications;
    }

    [HttpPost("{id}/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(string id)
    {
        var ok = await _notifications.TryMarkAsReadAsync(id);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("{id}/dismiss")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(string id)
    {
        var ok = await _notifications.TryDismissAsync(id);
        return ok ? NoContent() : NotFound();
    }
}