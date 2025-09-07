using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Models;

namespace Reservation.Models;

[Table("Details")]
public class Detail : GenericModel
{
    [Required]
    [ForeignKey("Reservation")]
    public Guid ReservationId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public int NumberOfAdults { get; set; }
    public int NumberOfChildren { get; set; }
    public int NumberOfInfants { get; set; }
    public int NumberOfPets { get; set; }
    public int ResourceQuantity { get; set; }
    public string Note { get; set; }
    public double OriginalPrice { get; set; }
    public double Discount { get; set; }
    public string Currency { get; set; }

    public virtual Reservation Reservation { get; set; }
}
