using Microsoft.AspNetCore.Identity;

namespace Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    
    public ICollection<UserTicker> UserTickers { get; set; } = new List<UserTicker>();
}
