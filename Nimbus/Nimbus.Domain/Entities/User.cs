using Nimbus.Domain.Entities.Base;
using Nimbus.Domain.Enums;
namespace Nimbus.Domain.Entities;

//extraction of the user entity from the identity package so that the identity stuff is not in the domain layer
public class User : BaseEntity
{
    public User(Guid id, string email, string name, string firstName, UserRole role)
    {
        Id = id;
        Email = email;
        Name = name;
        FirstName = firstName;
        Role = role;
    }
    public string Email { get; private set; }
    public string Name { get; private set; }
    public string FirstName { get; private set; }
    public UserRole Role { get; private set; }
}
