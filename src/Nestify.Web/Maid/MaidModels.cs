using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Web.Maid;

// Sample data models for the helper workspace. Everything here lives in memory
// until the booking tables exist.

public enum MaidSlotState { Off, Open, Booked }

public enum MaidRequestStatus { Pending, Accepted, Declined }

public enum MaidEngagementStatus { Active, Paused, Completed }

public enum MaidVisitStatus { Upcoming, Done, Cancelled }

public sealed class MaidSlot
{
    public DateTime Date { get; set; }
    public int Hour { get; set; }
}

public sealed class MaidRequest
{
    public string Id { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string HomeType { get; set; } = string.Empty;
    public List<string> Services { get; set; } = new();
    public List<MaidSlot> Slots { get; set; } = new();
    public decimal OfferedRate { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public MaidRequestStatus Status { get; set; }
    public bool ClientVerified { get; set; }
    public string? DeclineReason { get; set; }
}

public sealed class MaidEngagement
{
    public string Id { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public List<string> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public DateTime StartedOn { get; set; }
    public int WeeklyHours { get; set; }
    public int VisitsDone { get; set; }
    public int VisitsPlanned { get; set; }
    public MaidEngagementStatus Status { get; set; }
    public bool HelperMarkedComplete { get; set; }
    public bool ClientMarkedComplete { get; set; }
    public string Phone { get; set; } = string.Empty;
}

public sealed class MaidVisit
{
    public string Id { get; set; } = string.Empty;
    public string EngagementId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int StartHour { get; set; }
    public int EndHour { get; set; }
    public MaidVisitStatus Status { get; set; }
    public string? Note { get; set; }
}

public sealed class MaidReview
{
    public string Id { get; set; } = string.Empty;
    public string ReviewerName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? Reply { get; set; }
    public List<string> Tags { get; set; } = new();
}

// Everything about the helper that is not in the users table yet.
public sealed class MaidProfile
{
    public const string DefaultPhotoUrl =
        "https://res.cloudinary.com/dait0sacc/image/upload/v1774704629/k7ygnoel72ychr8ico6n.png";

    // Overrides for the users row; null means show what the database has.
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }

    public string PhotoUrl { get; set; } = DefaultPhotoUrl;
    public string Headline { get; set; } = "Cooking, cleaning and laundry for bachelor homes, 6 years of experience";
    public string Bio { get; set; } = "I have worked in bachelor homes in Dhanmondi and Mohammadpur since 2019. I keep a tidy kitchen, cook Bangla and simple continental food, and I am used to shared flats with four or five people and their different meal times.";
    public List<ServiceType> Services { get; set; } = new() { ServiceType.Cooking, ServiceType.Cleaning, ServiceType.Laundry };
    public string Languages { get; set; } = "Bangla, Basic English";
    public string Area { get; set; } = "Dhanmondi, Dhaka";
    public string Address { get; set; } = "Road 8, Dhanmondi";
    public string WhatsappNumber { get; set; } = "01711000000";
    public decimal MonthlyRate { get; set; } = 9500m;
    public int ExperienceYears { get; set; } = 6;
    public string PreferredHours { get; set; } = "Sat-Thu, 8 AM - 2 PM";

    public bool VerificationPending { get; set; }
    public string? VerificationDocument { get; set; }

    public MaidProfile Clone() => (MaidProfile)MemberwiseClone();
}
