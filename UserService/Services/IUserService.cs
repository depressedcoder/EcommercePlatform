using UserService.Models;

namespace UserService.Services;

public interface IUserService
{
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> ValidateCredentialsAsync(string username, string password);
    Task CreateAsync(User user);
    Task<IEnumerable<User>> GetAllAsync();
}
