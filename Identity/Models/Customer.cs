using MongoRepository;

namespace Suffuz.Identity.Models
{
    using System.ComponentModel.DataAnnotations;

    public class Customer : Entity
    {
        [Required]
        [Display(Name = "Customer Name")]
        public string Name { get; set; }

        [Required]
        [Display(Name = "Prefix")]
        public string Prefix { get; set; }
    }
}