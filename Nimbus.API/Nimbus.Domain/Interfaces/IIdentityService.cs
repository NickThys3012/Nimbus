namespace Nimbus.Domain.Interfaces;

public interface IIdentityService
{
    Task<string> RegisterAsync(string email, string password, string firstName, string lastName);
}
