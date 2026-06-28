using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace UltimateMonopoly.Hubs;

/// <summary>
/// Friend-messaging hub (E1). Push-only: it carries no client→server methods — all pushes originate
/// server-side from <c>FriendMessagingService</c> via <see cref="IHubContext{MessagingHub}"/>, routed to the
/// recipient with <c>Clients.User(userId)</c> (SignalR's built-in <c>Context.UserIdentifier</c>, so no manual
/// groups are needed). <c>[Authorize]</c> ensures the user identifier is populated.
/// </summary>
[Authorize]
public class MessagingHub : Hub;
