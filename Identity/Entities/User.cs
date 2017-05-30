namespace Suffuz.Identity.Entities
{
    using AspNet.Identity.MongoDB;

    public class User : IdentityUser
    {
        public string CustomerId { get; set; }
    }
}