using Microsoft.AspNetCore.Identity;

namespace InternLink.Web.Models;

public class AppRole : IdentityRole<Guid>
{
    public AppRole() : base() { }
    public AppRole(string roleName) : base(roleName) { }
}
