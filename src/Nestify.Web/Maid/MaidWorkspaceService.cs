using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Web.Maid;

// Front-end only workspace state for the helper interface. Sample data held in
// memory (Singleton) so the pages can be built and demoed before the booking
// tables exist. Only the profile page reads the real users row.
public sealed class MaidWorkspaceService
{
    public const int StartHour = 6;
    public const int EndHour = 24;
    public const int HoursPerDay = EndHour - StartHour;
    public const int Days = 7;

    // Weekly template: the same Saturday to Friday pattern repeats every week.
    private readonly Dictionary<(DayOfWeek Day, int Hour), MaidSlotState> _slots = new();
    private readonly List<MaidRequest> _requests = new();
    private readonly List<MaidEngagement> _engagements = new();
    private readonly List<MaidVisit> _visits = new();
    private readonly List<MaidReview> _reviews = new();

    private int _counter = 500;

    public MaidWorkspaceService()
    {
        var today = DateTime.Today;
        SeedEngagements(today);
        SeedVisits(today);
        SeedAvailability(today);
        SeedRequests(today);
        SeedReviews(today);
    }

    public event Action? Changed;

    // Sample profile bits that are not in the users table yet. Edits stay in
    // memory until a helper profile table exists.
    public MaidProfile Profile { get; } = new();

    public static string ServiceLabel(ServiceType type) => type switch
    {
        ServiceType.ElderCare => "Elder care",
        ServiceType.General => "General help",
        _ => type.ToString()
    };

    public void SaveProfile(MaidProfile draft)
    {
        Profile.FullName = draft.FullName;
        Profile.PhoneNumber = draft.PhoneNumber;
        Profile.Headline = draft.Headline;
        Profile.Bio = draft.Bio;
        Profile.Services = draft.Services.ToList();
        Profile.Languages = draft.Languages;
        Profile.Area = draft.Area;
        Profile.Address = draft.Address;
        Profile.WhatsappNumber = draft.WhatsappNumber;
        Profile.MonthlyRate = draft.MonthlyRate;
        Profile.ExperienceYears = draft.ExperienceYears;
        Profile.PreferredHours = draft.PreferredHours;
        Changed?.Invoke();
    }

    public void SetPhoto(string url)
    {
        Profile.PhotoUrl = url;
        Changed?.Invoke();
    }

    public void SubmitVerification(string documentType)
    {
        Profile.VerificationDocument = documentType;
        Profile.VerificationPending = true;
        Changed?.Invoke();
    }

    public void CancelVerification()
    {
        Profile.VerificationDocument = null;
        Profile.VerificationPending = false;
        Changed?.Invoke();
    }

    public bool IsAvailable { get; private set; } = true;

    public IReadOnlyList<MaidRequest> Requests => _requests;
    public IReadOnlyList<MaidEngagement> Engagements => _engagements;
    public IReadOnlyList<MaidVisit> Visits => _visits;
    public IReadOnlyList<MaidReview> Reviews => _reviews;

    public int PendingRequestCount => _requests.Count(r => r.Status == MaidRequestStatus.Pending);
    public int ActiveEngagementCount => _engagements.Count(e => e.Status == MaidEngagementStatus.Active);
    public int CompletedEngagementCount => _engagements.Count(e => e.Status == MaidEngagementStatus.Completed);

    public double RatingAverage => _reviews.Count == 0 ? 0 : Math.Round(_reviews.Average(r => r.Rating), 1);
    public int RatingCount => _reviews.Count;

    // ---------- availability ----------

    // Saturday first, the way the week is read here.
    public static readonly DayOfWeek[] Week =
    {
        DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
        DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
    };

    public MaidSlotState GetSlot(DayOfWeek day, int hour) =>
        _slots.TryGetValue((day, hour), out var state) ? state : MaidSlotState.Off;

    public MaidSlotState GetSlot(DateTime date, int hour) => GetSlot(date.DayOfWeek, hour);

    public Dictionary<(DayOfWeek Day, int Hour), MaidSlotState> SnapshotWeek()
    {
        var copy = new Dictionary<(DayOfWeek, int), MaidSlotState>();
        foreach (var day in Week)
        {
            for (var h = StartHour; h < EndHour; h++)
            {
                copy[(day, h)] = GetSlot(day, h);
            }
        }
        return copy;
    }

    public void SaveWeek(Dictionary<(DayOfWeek Day, int Hour), MaidSlotState> week)
    {
        foreach (var (key, state) in week)
        {
            // Booked hours are owned by the engagement, not the board.
            if (GetSlot(key.Day, key.Hour) == MaidSlotState.Booked) continue;
            _slots[key] = state;
        }
        Changed?.Invoke();
    }

    public void SetAvailable(bool value)
    {
        IsAvailable = value;
        Changed?.Invoke();
    }

    public int OpenHoursPerWeek() => _slots.Values.Count(s => s == MaidSlotState.Open);

    // ---------- requests ----------

    public void AcceptRequest(string id)
    {
        var request = _requests.FirstOrDefault(r => r.Id == id);
        if (request is null || request.Status != MaidRequestStatus.Pending) return;

        request.Status = MaidRequestStatus.Accepted;

        var engagement = new MaidEngagement
        {
            Id = $"eng-{_counter++}",
            ClientName = request.ClientName,
            Area = request.Area,
            Address = $"{request.HomeType}, {request.Area}",
            Services = request.Services.ToList(),
            MonthlyRate = request.OfferedRate,
            StartedOn = DateTime.Today,
            WeeklyHours = Math.Max(request.Slots.Count, 6),
            VisitsDone = 0,
            VisitsPlanned = Math.Max(request.Slots.Count, 6) * 4,
            Status = MaidEngagementStatus.Active,
            Phone = "01" + Random.Shared.Next(300000000, 999999999)
        };
        _engagements.Insert(0, engagement);

        foreach (var slot in request.Slots)
        {
            _slots[(slot.Date.DayOfWeek, slot.Hour)] = MaidSlotState.Booked;
            _visits.Add(new MaidVisit
            {
                Id = $"visit-{_counter++}",
                EngagementId = engagement.Id,
                ClientName = request.ClientName,
                Area = request.Area,
                Service = request.Services.FirstOrDefault() ?? "Visit",
                Date = slot.Date.Date,
                StartHour = slot.Hour,
                EndHour = slot.Hour + 1,
                Status = MaidVisitStatus.Upcoming
            });
        }

        Changed?.Invoke();
    }

    public void DeclineRequest(string id, string? reason)
    {
        var request = _requests.FirstOrDefault(r => r.Id == id);
        if (request is null || request.Status != MaidRequestStatus.Pending) return;
        request.Status = MaidRequestStatus.Declined;
        request.DeclineReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Changed?.Invoke();
    }

    // ---------- engagements ----------

    public void MarkComplete(string id)
    {
        var engagement = _engagements.FirstOrDefault(e => e.Id == id);
        if (engagement is null) return;

        engagement.HelperMarkedComplete = true;
        if (engagement.ClientMarkedComplete)
        {
            engagement.Status = MaidEngagementStatus.Completed;
        }
        Changed?.Invoke();
    }

    public void TogglePause(string id)
    {
        var engagement = _engagements.FirstOrDefault(e => e.Id == id);
        if (engagement is null || engagement.Status == MaidEngagementStatus.Completed) return;

        engagement.Status = engagement.Status == MaidEngagementStatus.Active
            ? MaidEngagementStatus.Paused
            : MaidEngagementStatus.Active;
        Changed?.Invoke();
    }

    // ---------- reviews ----------

    public void ReplyToReview(string id, string reply)
    {
        var review = _reviews.FirstOrDefault(r => r.Id == id);
        if (review is null) return;
        review.Reply = string.IsNullOrWhiteSpace(reply) ? null : reply.Trim();
        Changed?.Invoke();
    }

    // ---------- helpers ----------

    public static string HourLabel(int hour) => hour switch
    {
        0 or 24 => "12 AM",
        12 => "12 PM",
        < 12 => $"{hour} AM",
        _ => $"{hour - 12} PM"
    };

    public static string RangeLabel(int from, int to) => $"{HourLabel(from)} – {HourLabel(to)}";

    // ---------- seed ----------

    private void SeedEngagements(DateTime today)
    {
        _engagements.AddRange(new[]
        {
            new MaidEngagement
            {
                Id = "eng-101", ClientName = "Farhana Rahman", Area = "Dhanmondi 27", Address = "House 14, Road 27, Dhanmondi",
                Services = new() { "Cooking", "Cleaning" }, MonthlyRate = 9500m, StartedOn = today.AddDays(-38),
                WeeklyHours = 12, VisitsDone = 21, VisitsPlanned = 24, Status = MaidEngagementStatus.Active, Phone = "01711223344"
            },
            new MaidEngagement
            {
                Id = "eng-102", ClientName = "Tanvir Ahmed", Area = "Mohammadpur", Address = "Flat B4, Tajmahal Road, Mohammadpur",
                Services = new() { "Cooking" }, MonthlyRate = 7000m, StartedOn = today.AddDays(-12),
                WeeklyHours = 9, VisitsDone = 5, VisitsPlanned = 18, Status = MaidEngagementStatus.Active, Phone = "01822334455"
            },
            new MaidEngagement
            {
                Id = "eng-103", ClientName = "Nusrat Jahan", Area = "Lalmatia", Address = "Block D, Lalmatia",
                Services = new() { "Cleaning", "Laundry" }, MonthlyRate = 6000m, StartedOn = today.AddDays(-20),
                WeeklyHours = 6, VisitsDone = 8, VisitsPlanned = 12, Status = MaidEngagementStatus.Paused, Phone = "01933445566"
            },
            new MaidEngagement
            {
                Id = "eng-104", ClientName = "Rafiq Chowdhury", Area = "Shyamoli", Address = "Road 2, Shyamoli",
                Services = new() { "Cooking" }, MonthlyRate = 8000m, StartedOn = today.AddDays(-95),
                WeeklyHours = 10, VisitsDone = 40, VisitsPlanned = 40, Status = MaidEngagementStatus.Completed,
                HelperMarkedComplete = true, ClientMarkedComplete = true, Phone = "01644556677"
            },
            new MaidEngagement
            {
                Id = "eng-106", ClientName = "Jubayer Hossain", Area = "Kalabagan", Address = "Flat 5C, Kalabagan",
                Services = new() { "Cleaning" }, MonthlyRate = 5500m, StartedOn = today.AddDays(-170),
                WeeklyHours = 6, VisitsDone = 24, VisitsPlanned = 24, Status = MaidEngagementStatus.Completed,
                HelperMarkedComplete = true, ClientMarkedComplete = true, Phone = "01777889900"
            },
            new MaidEngagement
            {
                Id = "eng-107", ClientName = "Arif Mahmud", Area = "Mohammadpur", Address = "Block C, Mohammadpur",
                Services = new() { "Cooking", "Laundry" }, MonthlyRate = 9000m, StartedOn = today.AddDays(-210),
                WeeklyHours = 12, VisitsDone = 48, VisitsPlanned = 48, Status = MaidEngagementStatus.Completed,
                HelperMarkedComplete = true, ClientMarkedComplete = true, Phone = "01888990011"
            },
            new MaidEngagement
            {
                Id = "eng-108", ClientName = "Sabbir Rahman", Area = "Dhanmondi 15", Address = "Road 15, Dhanmondi",
                Services = new() { "Cooking" }, MonthlyRate = 7500m, StartedOn = today.AddDays(-250),
                WeeklyHours = 10, VisitsDone = 40, VisitsPlanned = 40, Status = MaidEngagementStatus.Completed,
                HelperMarkedComplete = true, ClientMarkedComplete = true, Phone = "01999001122"
            },
            new MaidEngagement
            {
                Id = "eng-109", ClientName = "Tamim Iqbal", Area = "Shukrabad", Address = "House 9, Shukrabad",
                Services = new() { "Cleaning", "Grocery runs" }, MonthlyRate = 6500m, StartedOn = today.AddDays(-300),
                WeeklyHours = 8, VisitsDone = 32, VisitsPlanned = 32, Status = MaidEngagementStatus.Completed,
                HelperMarkedComplete = true, ClientMarkedComplete = true, Phone = "01611223344"
            },
            new MaidEngagement
            {
                Id = "eng-110", ClientName = "Nayeem Khan", Area = "Lalmatia", Address = "Block B, Lalmatia",
                Services = new() { "Cooking", "Cleaning" }, MonthlyRate = 10000m, StartedOn = today.AddDays(-360),
                WeeklyHours = 14, VisitsDone = 56, VisitsPlanned = 56, Status = MaidEngagementStatus.Completed,
                HelperMarkedComplete = true, ClientMarkedComplete = true, Phone = "01522334455"
            },
            new MaidEngagement
            {
                Id = "eng-105", ClientName = "Sadia Islam", Area = "Jigatola", Address = "House 3, Jigatola",
                Services = new() { "Cooking", "Cleaning" }, MonthlyRate = 11000m, StartedOn = today.AddDays(-140),
                WeeklyHours = 15, VisitsDone = 60, VisitsPlanned = 60, Status = MaidEngagementStatus.Completed,
                HelperMarkedComplete = true, ClientMarkedComplete = true, Phone = "01555667788"
            }
        });
    }

    private void SeedVisits(DateTime today)
    {
        // A repeating pattern over three weeks so the schedule has past and future days.
        for (var offset = -7; offset < 15; offset++)
        {
            var date = today.AddDays(offset);
            var dow = date.DayOfWeek;
            var past = date < today;

            if (dow != DayOfWeek.Friday)
            {
                AddVisit("eng-101", "Farhana Rahman", "Dhanmondi 27", "Cooking", date, 8, 10, past);
            }

            if (dow is DayOfWeek.Saturday or DayOfWeek.Monday or DayOfWeek.Wednesday)
            {
                AddVisit("eng-102", "Tanvir Ahmed", "Mohammadpur", "Cooking", date, 15, 18, past);
            }

            if (offset < 0 && dow is DayOfWeek.Sunday or DayOfWeek.Tuesday)
            {
                AddVisit("eng-103", "Nusrat Jahan", "Lalmatia", "Cleaning", date, 11, 13, past);
            }

            if (dow == DayOfWeek.Thursday)
            {
                AddVisit("eng-101", "Farhana Rahman", "Dhanmondi 27", "Cleaning", date, 10, 12, past);
            }
        }

        var cancelled = _visits.FirstOrDefault(v => v.Date == today.AddDays(-3));
        if (cancelled is not null)
        {
            cancelled.Status = MaidVisitStatus.Cancelled;
            cancelled.Note = "Client was travelling";
        }
    }

    private void AddVisit(string engagementId, string client, string area, string service, DateTime date, int from, int to, bool past)
    {
        _visits.Add(new MaidVisit
        {
            Id = $"visit-{_counter++}",
            EngagementId = engagementId,
            ClientName = client,
            Area = area,
            Service = service,
            Date = date,
            StartHour = from,
            EndHour = to,
            Status = past ? MaidVisitStatus.Done : MaidVisitStatus.Upcoming
        });
    }

    private void SeedAvailability(DateTime today)
    {
        foreach (var day in Week)
        {
            if (day == DayOfWeek.Friday) continue;

            for (var h = 7; h < 19; h++)
            {
                _slots[(day, h)] = MaidSlotState.Open;
            }
        }

        foreach (var visit in _visits.Where(v => v.Status == MaidVisitStatus.Upcoming))
        {
            for (var h = visit.StartHour; h < visit.EndHour; h++)
            {
                _slots[(visit.Date.DayOfWeek, h)] = MaidSlotState.Booked;
            }
        }
    }

    private void SeedRequests(DateTime today)
    {
        _requests.AddRange(new[]
        {
            new MaidRequest
            {
                Id = "req-201", ClientName = "Mahmudul Hasan", Area = "Dhanmondi 8", HomeType = "3 bed flat",
                Services = new() { "Cooking", "Cleaning" }, OfferedRate = 10000m, ClientVerified = true,
                Message = "We need help on weekday mornings, mostly breakfast and lunch prep for four bachelors.",
                RequestedAtUtc = DateTime.UtcNow.AddHours(-3), Status = MaidRequestStatus.Pending,
                Slots = new()
                {
                    new MaidSlot { Date = today.AddDays(1), Hour = 7 }, new MaidSlot { Date = today.AddDays(1), Hour = 8 },
                    new MaidSlot { Date = today.AddDays(3), Hour = 7 }, new MaidSlot { Date = today.AddDays(3), Hour = 8 },
                    new MaidSlot { Date = today.AddDays(5), Hour = 7 }
                }
            },
            new MaidRequest
            {
                Id = "req-202", ClientName = "Ayesha Siddiqua", Area = "Mohammadpur", HomeType = "2 bed flat",
                Services = new() { "Cleaning" }, OfferedRate = 6500m, ClientVerified = false,
                Message = "Three of us share the flat and nobody has time to clean. Afternoons twice a week would be perfect.",
                RequestedAtUtc = DateTime.UtcNow.AddHours(-26), Status = MaidRequestStatus.Pending,
                Slots = new()
                {
                    new MaidSlot { Date = today.AddDays(2), Hour = 14 }, new MaidSlot { Date = today.AddDays(2), Hour = 15 },
                    new MaidSlot { Date = today.AddDays(2), Hour = 16 }, new MaidSlot { Date = today.AddDays(4), Hour = 14 },
                    new MaidSlot { Date = today.AddDays(4), Hour = 15 }, new MaidSlot { Date = today.AddDays(4), Hour = 16 }
                }
            },
            new MaidRequest
            {
                Id = "req-203", ClientName = "Imran Kabir", Area = "Lalmatia", HomeType = "Duplex",
                Services = new() { "Cleaning", "Laundry" }, OfferedRate = 7500m, ClientVerified = true,
                Message = "Deep cleaning twice a week, laundry on the same days if possible.",
                RequestedAtUtc = DateTime.UtcNow.AddDays(-2), Status = MaidRequestStatus.Pending,
                Slots = new()
                {
                    new MaidSlot { Date = today.AddDays(6), Hour = 11 }, new MaidSlot { Date = today.AddDays(6), Hour = 12 }
                }
            },
            new MaidRequest
            {
                Id = "req-205", ClientName = "Fahim Chowdhury", Area = "Shukrabad", HomeType = "4 bed mess",
                Services = new() { "Cooking", "Cleaning" }, OfferedRate = 12000m, ClientVerified = true,
                Message = "Eight of us in a mess. Lunch and dinner on weekdays, cleaning on Fridays.",
                RequestedAtUtc = DateTime.UtcNow.AddDays(-3), Status = MaidRequestStatus.Pending,
                Slots = new() { new MaidSlot { Date = today.AddDays(1), Hour = 12 }, new MaidSlot { Date = today.AddDays(1), Hour = 13 } }
            },
            new MaidRequest
            {
                Id = "req-206", ClientName = "Rashed Karim", Area = "Dhanmondi 4", HomeType = "2 bed flat",
                Services = new() { "Laundry" }, OfferedRate = 4000m, ClientVerified = false,
                Message = "Laundry once a week for two people.",
                RequestedAtUtc = DateTime.UtcNow.AddDays(-4), Status = MaidRequestStatus.Pending,
                Slots = new() { new MaidSlot { Date = today.AddDays(5), Hour = 10 } }
            },
            new MaidRequest
            {
                Id = "req-207", ClientName = "Sajid Alam", Area = "Lalmatia", HomeType = "3 bed flat",
                Services = new() { "Grocery runs", "Cooking" }, OfferedRate = 8500m, ClientVerified = true,
                Message = "Weekly bazar plus dinner four nights a week.",
                RequestedAtUtc = DateTime.UtcNow.AddDays(-5), Status = MaidRequestStatus.Pending,
                Slots = new() { new MaidSlot { Date = today.AddDays(2), Hour = 18 }, new MaidSlot { Date = today.AddDays(2), Hour = 19 } }
            },
            new MaidRequest
            {
                Id = "req-208", ClientName = "Hasib Noor", Area = "Mohammadpur", HomeType = "Mess",
                Services = new() { "Cleaning" }, OfferedRate = 5000m, ClientVerified = false,
                Message = "Just the kitchen and bathrooms, twice a week.",
                RequestedAtUtc = DateTime.UtcNow.AddDays(-5).AddHours(-6), Status = MaidRequestStatus.Pending,
                Slots = new() { new MaidSlot { Date = today.AddDays(3), Hour = 9 } }
            },
            new MaidRequest
            {
                Id = "req-204", ClientName = "Shirin Akter", Area = "Kalabagan", HomeType = "1 bed flat",
                Services = new() { "Cooking" }, OfferedRate = 5000m, ClientVerified = false,
                Message = "Just dinner on weekdays.",
                RequestedAtUtc = DateTime.UtcNow.AddDays(-6), Status = MaidRequestStatus.Declined, Slots = new(),
                DeclineReason = "Evenings are already taken by another home this month."
            }
        });
    }

    private void SeedReviews(DateTime today)
    {
        _reviews.AddRange(new[]
        {
            new MaidReview
            {
                Id = "rev-301", ReviewerName = "Rafiq Chowdhury", Area = "Shyamoli", Service = "Cooking", Rating = 5,
                Comment = "Her cooking is wonderful, all four of us in the flat looked forward to lunch. Always on time, never missed a day in three months.",
                CreatedAtUtc = today.AddDays(-4), Tags = new() { "Punctual", "Great cook" }
            },
            new MaidReview
            {
                Id = "rev-302", ReviewerName = "Sadia Islam", Area = "Jigatola", Service = "Cooking", Rating = 5,
                Comment = "Kept the whole mess clean and cooked for six of us without complaint. We felt safe giving her a key to the flat.",
                CreatedAtUtc = today.AddDays(-19), Tags = new() { "Trustworthy", "Hard working" },
                Reply = "Thank you bhai, you were all very easy to work for."
            },
            new MaidReview
            {
                Id = "rev-303", ReviewerName = "Farhana Rahman", Area = "Dhanmondi 27", Service = "Cleaning", Rating = 4,
                Comment = "Very thorough with the kitchen and bathrooms. Sometimes a few minutes late on Sundays but always informs ahead.",
                CreatedAtUtc = today.AddDays(-33), Tags = new() { "Thorough" }
            },
            new MaidReview
            {
                Id = "rev-304", ReviewerName = "Tanvir Ahmed", Area = "Mohammadpur", Service = "Cooking", Rating = 5,
                Comment = "We are three students with odd hours and she worked around all of our schedules without fuss.",
                CreatedAtUtc = today.AddDays(-41), Tags = new() { "Flexible" }
            },
            new MaidReview
            {
                Id = "rev-305", ReviewerName = "Nusrat Jahan", Area = "Lalmatia", Service = "Laundry", Rating = 3,
                Comment = "Cleaning was fine. One shirt shrank in the wash, she apologised and was more careful after.",
                CreatedAtUtc = today.AddDays(-58), Tags = new() { "Honest" }
            },
            new MaidReview
            {
                Id = "rev-306", ReviewerName = "Kamrul Islam", Area = "Dhanmondi 15", Service = "Cooking", Rating = 4,
                Comment = "Reliable and polite. Learned what all of us in the flat like to eat within a week.",
                CreatedAtUtc = today.AddDays(-90), Tags = new() { "Reliable" }
            },
            new MaidReview
            {
                Id = "rev-307", ReviewerName = "Laila Begum", Area = "Mohammadpur", Service = "Cleaning", Rating = 5,
                Comment = "Spotless every time. Highly recommend to anyone in the area.",
                CreatedAtUtc = today.AddDays(-120), Tags = new() { "Spotless", "Recommended" }
            },
            new MaidReview
            {
                Id = "rev-308", ReviewerName = "Jubayer Hossain", Area = "Kalabagan", Service = "Cleaning", Rating = 4,
                Comment = "Good work on the common areas. Would have liked the balcony done more often.",
                CreatedAtUtc = today.AddDays(-150), Tags = new() { "Thorough" }
            },
            new MaidReview
            {
                Id = "rev-309", ReviewerName = "Arif Mahmud", Area = "Mohammadpur", Service = "Laundry", Rating = 5,
                Comment = "Clothes came back folded and sorted by roommate, which honestly nobody asked for but everyone loved.",
                CreatedAtUtc = today.AddDays(-185), Tags = new() { "Organised", "Recommended" }
            },
            new MaidReview
            {
                Id = "rev-310", ReviewerName = "Sabbir Rahman", Area = "Dhanmondi 15", Service = "Cooking", Rating = 5,
                Comment = "Best khichuri in Dhanmondi. Kept the kitchen clean after cooking too.",
                CreatedAtUtc = today.AddDays(-230), Tags = new() { "Great cook" }
            },
            new MaidReview
            {
                Id = "rev-311", ReviewerName = "Tamim Iqbal", Area = "Shukrabad", Service = "Grocery runs", Rating = 4,
                Comment = "Always brought receipts and never overcharged. Once bought the wrong rice, fixed it the next day.",
                CreatedAtUtc = today.AddDays(-280), Tags = new() { "Honest" }
            },
            new MaidReview
            {
                Id = "rev-312", ReviewerName = "Nayeem Khan", Area = "Lalmatia", Service = "Cooking", Rating = 5,
                Comment = "Worked with us for a full year. Never a single complaint from anyone in the flat.",
                CreatedAtUtc = today.AddDays(-330), Tags = new() { "Reliable", "Punctual" }
            },
            new MaidReview
            {
                Id = "rev-313", ReviewerName = "Mehedi Hasan", Area = "Jigatola", Service = "Cleaning", Rating = 3,
                Comment = "Fine overall. Missed two days in a month without telling us in advance.",
                CreatedAtUtc = today.AddDays(-345), Tags = new() { "Thorough" }
            },
            new MaidReview
            {
                Id = "rev-314", ReviewerName = "Rakib Hasan", Area = "Shyamoli", Service = "Cooking", Rating = 5,
                Comment = "She figured out five different spice tolerances in the flat and cooked for all of us. Magic.",
                CreatedAtUtc = today.AddDays(-380), Tags = new() { "Great cook", "Flexible" }
            }
        });
    }
}
