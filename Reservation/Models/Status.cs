using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Models;

namespace Reservation.Models;

[Table("Statuses")]
public class Status : GenericModel
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; }
    public string Description { get; set; }
}
