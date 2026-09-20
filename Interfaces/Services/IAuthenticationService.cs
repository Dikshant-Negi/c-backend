using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.DTOs;

namespace EquipmentManagementBackend.Application;

public interface IAuthenticationService
{
    Task<bool> NeedsBootstrapAsync();
    Task BootstrapAsync(string name, string email, string password);
    Task<User> RegisterAsync(string name, string email, string password, string role);
    Task<Session> LoginAsync(string email, string password);
    Task<User> RequireAsync(IUnitOfWork db, Session session, params string[] roles);
    Task<User> CurrentAsync(Session session);
    Task ChangePasswordAsync(Session session, string currentPassword, string newPassword);
}
