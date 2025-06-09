using UserService.Models;
using UserService.Repositories;

namespace UserService.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _repository.GetByUsernameAsync(username);
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _repository.GetByUsernameAsync(username);
        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            return false;

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    public async Task CreateAsync(User user)
    {
        await _repository.AddAsync(user);
    }
    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

}
