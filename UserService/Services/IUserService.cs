using UserService.Models;

namespace UserService.Services;

public interface IUserService
{
    Task<User?> GetByUsernameAsync(string username);
    Task CreateAsync(User user);
    Task<IEnumerable<User>> GetAllAsync();
    Task UpdateUserAsync(User user);
    Task DeleteUserAsync(string username);
}
