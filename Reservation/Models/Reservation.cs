using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Models;

namespace Reservation.Models;

[Table("Reservations")]
public class Reservation : GenericModel
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    public Guid PaymentTypeId { get; set; }

    [Required]
    [ForeignKey("ReservationStatus")]
    public Guid StatusId { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    public double TotalAmount { get; set; }
    public string Code { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public string Source { get; set; }
    public virtual Status Status { get; set; }
    public virtual ICollection<ReservationResource> ReservationResources { get; set; }
}
