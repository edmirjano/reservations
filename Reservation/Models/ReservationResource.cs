using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Models;

namespace Reservation.Models;

[Table("ReservationResources")]
public class ReservationResource : GenericModel
{
    [Required]
    [ForeignKey("Reservation")]
    public Guid ReservationId { get; set; }
    public virtual Reservation Reservation { get; set; }

    [Required]
    public Guid ResourceId { get; set; }
}
