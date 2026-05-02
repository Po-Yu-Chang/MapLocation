using MapLocationApp.Models;

namespace MapLocationApp.Services;

public interface IRoleService
{
    /// <summary>True if there is a logged-in user AND that user is an admin.</summary>
    bool IsCurrentUserAdmin();

    /// <summary>Returns Employee when there's no session, so checks fail closed.</summary>
    UserRole CurrentRole();
}

public class RoleService : IRoleService
{
    private readonly IUserSessionService _session;

    public RoleService(IUserSessionService session)
    {
        _session = session;
    }

    public bool IsCurrentUserAdmin() => _session.CurrentUser?.IsAdmin == true;

    public UserRole CurrentRole() => _session.CurrentUser?.Role ?? UserRole.Employee;
}
