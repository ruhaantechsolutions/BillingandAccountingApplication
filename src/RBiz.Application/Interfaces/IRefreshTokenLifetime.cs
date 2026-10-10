namespace RBiz.Application.Interfaces;

/// <summary>
/// How long a refresh token stays valid.
///
/// Exposed as an interface rather than reading configuration directly, so the Application
/// layer stays unaware of where the value comes from. The practical constraint is that it
/// must comfortably exceed the longest period a device is expected to be offline, because
/// once it lapses the queued work can only be uploaded after a fresh sign-in.
/// </summary>
public interface IRefreshTokenLifetime
{
    TimeSpan RefreshTokenLifetime { get; }
}
