using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Sample.Api.Data
{
    public class AppUser : IdentityUser<int>
    {
        [Required] 
        [StringLength(128)] 
        public string Address { get; set; }
    }
}