using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;

namespace InventarioApi.Models
{
    public class AppUser : IdentityUser
    {
        public int? TenantId { get; set; }

        [JsonIgnore]
        public Tenant? Tenant { get; set; }
    }
}