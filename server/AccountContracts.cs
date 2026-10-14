// Architecture contract. This file is not a deployed account server.
namespace Hydra.Server.Contracts;

public record Account(string Id, string Username, string Email);
public record DeviceSession(string Id, string DeviceName, System.DateTimeOffset ExpiresAt);
public interface IAccountService
{
    System.Threading.Tasks.Task<Account> CreateAsync(string username, string email, string password, System.Threading.CancellationToken ct);
    System.Threading.Tasks.Task<DeviceSession> SignInAsync(string email, string password, System.Threading.CancellationToken ct);
    System.Threading.Tasks.Task RevokeAsync(string sessionId, System.Threading.CancellationToken ct);
    System.Threading.Tasks.Task RequestPasswordResetAsync(string email, System.Threading.CancellationToken ct);
}
