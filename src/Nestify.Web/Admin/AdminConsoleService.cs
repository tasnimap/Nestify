namespace Nestify.Web.Admin;

// Front-end only console state. Everything here is sample data held in memory so
// the admin pages can be built and demoed before the moderation API exists.
public sealed class AdminConsoleService
{
    private readonly List<HousingPost> _housing = new();
    private readonly List<MarketItem> _market = new();
    private readonly List<PostReport> _reports = new();
    private readonly List<VerificationApplication> _applications = new();
    private readonly List<FeeSetting> _fees = new();
    private readonly List<PostPlan> _plans = new();
    private readonly List<AuditEntry> _audit = new();
    private readonly List<AdminAccount> _admins = new();

    private int _counter = 1000;

    public AdminConsoleService()
    {
        SeedHousing();
        SeedMarket();
        SeedReports();
        SeedApplications();
        SeedPricing();
        SeedAdmins();
        SeedAudit();
    }

    // Set by the admin shell once the signed-in name is known, so audit rows
    // carry whoever is actually clicking.
    public string CurrentAdmin { get; set; } = "Admin";

    public event Action? Changed;

    public IReadOnlyList<HousingPost> Housing => _housing;
    public IReadOnlyList<MarketItem> Market => _market;
    public IReadOnlyList<PostReport> Reports => _reports;
    public IReadOnlyList<VerificationApplication> Applications => _applications;
    public IReadOnlyList<FeeSetting> Fees => _fees;
    public IReadOnlyList<PostPlan> Plans => _plans;
    public IReadOnlyList<AuditEntry> Audit => _audit;
    public IReadOnlyList<AdminAccount> Admins => _admins;

    public IReadOnlyList<RevenuePoint> Revenue { get; } = new List<RevenuePoint>
    {
        new() { Month = "Oct", Housing = 18400, Marketplace = 9200,  Verification = 6300 },
        new() { Month = "Nov", Housing = 21100, Marketplace = 10400, Verification = 7100 },
        new() { Month = "Dec", Housing = 19600, Marketplace = 12800, Verification = 6800 },
        new() { Month = "Jan", Housing = 26300, Marketplace = 14100, Verification = 9400 },
        new() { Month = "Feb", Housing = 24800, Marketplace = 13500, Verification = 8700 },
        new() { Month = "Mar", Housing = 29700, Marketplace = 15900, Verification = 10200 },
        new() { Month = "Apr", Housing = 33200, Marketplace = 17300, Verification = 11800 },
        new() { Month = "May", Housing = 31500, Marketplace = 16400, Verification = 11100 },
        new() { Month = "Jun", Housing = 37900, Marketplace = 19800, Verification = 13600 },
        new() { Month = "Jul", Housing = 41300, Marketplace = 21200, Verification = 14900 },
        new() { Month = "Aug", Housing = 39600, Marketplace = 22700, Verification = 14100 },
        new() { Month = "Sep", Housing = 46800, Marketplace = 25400, Verification = 16700 }
    };

    public IReadOnlyList<GrowthPoint> Growth { get; } = new List<GrowthPoint>
    {
        new() { Month = "Apr", Users = 1180, Helpers = 190 },
        new() { Month = "May", Users = 1420, Helpers = 232 },
        new() { Month = "Jun", Users = 1755, Helpers = 281 },
        new() { Month = "Jul", Users = 2090, Helpers = 336 },
        new() { Month = "Aug", Users = 2468, Helpers = 392 },
        new() { Month = "Sep", Users = 2914, Helpers = 451 }
    };

    public PlatformStats Stats => new()
    {
        TotalUsers = 2914,
        TotalHelpers = 451,
        TotalAdmins = _admins.Count(a => a.IsActive),
        ActiveThisMonth = 1863,
        HomesFound = 612,
        HelpersHired = 287,
        ItemsSold = 1044,
        ItemsListed = 1732,
        FakePostsRemoved = 96 + _audit.Count(a => a.Action == "Removed post"),
        VerifiedAccounts = 1208,
        LiveHousingPosts = _housing.Count(h => h.State == PostState.Live),
        LiveMarketPosts = _market.Count(m => m.State == PostState.Live)
    };

    public int MonthRevenue => Revenue[^1].Total;

    public int LastMonthRevenue => Revenue[^2].Total;

    public int YearRevenue => Revenue.Sum(r => r.Total);

    public int PendingApplications => _applications.Count(a => a.State == ApplicationState.Pending);

    public int OpenReports => _reports.Count(r => r.State == ReportState.Open);

    // ---------- moderation ----------

    public void RemoveHousingPost(string id, string reason)
    {
        var post = _housing.FirstOrDefault(p => p.Id == id);
        if (post is null || post.State == PostState.Removed) return;

        post.State = PostState.Removed;
        post.RemovalReason = reason;
        Log("Removed post", $"Housing · {post.Title}", reason, "danger");
    }

    public void RestoreHousingPost(string id)
    {
        var post = _housing.FirstOrDefault(p => p.Id == id);
        if (post is null || post.State == PostState.Live) return;

        post.State = PostState.Live;
        post.RemovalReason = null;
        Log("Restored post", $"Housing · {post.Title}", null, "ok");
    }

    public void RemoveMarketItem(string id, string reason)
    {
        var item = _market.FirstOrDefault(p => p.Id == id);
        if (item is null || item.State == PostState.Removed) return;

        item.State = PostState.Removed;
        item.RemovalReason = reason;
        Log("Removed post", $"Marketplace · {item.Title}", reason, "danger");
    }

    public void RestoreMarketItem(string id)
    {
        var item = _market.FirstOrDefault(p => p.Id == id);
        if (item is null || item.State == PostState.Live) return;

        item.State = PostState.Live;
        item.RemovalReason = null;
        Log("Restored post", $"Marketplace · {item.Title}", null, "ok");
    }

    public void ResolveReport(string id, string note)
    {
        var report = _reports.FirstOrDefault(r => r.Id == id);
        if (report is null) return;

        report.State = ReportState.Resolved;
        Log("Resolved report", report.PostTitle, note, "ok");
    }

    public void DismissReport(string id)
    {
        var report = _reports.FirstOrDefault(r => r.Id == id);
        if (report is null) return;

        report.State = ReportState.Dismissed;
        Log("Dismissed report", report.PostTitle, "No violation found", "neutral");
    }

    // ---------- verification ----------

    public void DecideApplication(string id, bool approve, string note)
    {
        var application = _applications.FirstOrDefault(a => a.Id == id);
        if (application is null) return;

        application.State = approve ? ApplicationState.Approved : ApplicationState.Declined;
        application.Decision = note;
        Log(approve ? "Approved verification" : "Declined verification",
            $"{application.Applicant} · {KindLabel(application.Kind)}", note, approve ? "ok" : "danger");
    }

    // ---------- pricing ----------

    public void SaveFee(string id, int amount)
    {
        var fee = _fees.FirstOrDefault(f => f.Id == id);
        if (fee is null || fee.Amount == amount) return;

        var old = fee.Amount;
        fee.Amount = amount;
        Log("Updated fee", fee.Label, $"BDT {old} to BDT {amount}", "neutral");
    }

    public void SavePlan(PostPlan plan)
    {
        var existing = _plans.FirstOrDefault(p => p.Id == plan.Id);
        if (existing is null)
        {
            plan.Id = NextId("PLN");
            _plans.Add(plan);
            Log("Created plan", $"{ScopeLabel(plan.Scope)} · {plan.Name}",
                $"{plan.Posts} posts for BDT {plan.Price}", "ok");
            return;
        }

        existing.Name = plan.Name;
        existing.Scope = plan.Scope;
        existing.Posts = plan.Posts;
        existing.Price = plan.Price;
        existing.ValidDays = plan.ValidDays;
        Log("Updated plan", $"{ScopeLabel(existing.Scope)} · {existing.Name}",
            $"{existing.Posts} posts for BDT {existing.Price}", "neutral");
    }

    public void TogglePlan(string id)
    {
        var plan = _plans.FirstOrDefault(p => p.Id == id);
        if (plan is null) return;

        plan.IsActive = !plan.IsActive;
        Log(plan.IsActive ? "Enabled plan" : "Disabled plan", plan.Name, null, plan.IsActive ? "ok" : "danger");
    }

    // ---------- admin accounts ----------

    public void AddAdmin(AdminAccount account)
    {
        account.Id = NextId("ADM");
        account.CreatedOn = DateTime.Now;
        account.LastActive = DateTime.Now;
        account.IsActive = true;
        _admins.Add(account);
        Log("Created admin account", account.FullName, account.Scope, "ok");
    }

    public void ToggleAdmin(string id)
    {
        var account = _admins.FirstOrDefault(a => a.Id == id);
        if (account is null) return;

        account.IsActive = !account.IsActive;
        Log(account.IsActive ? "Enabled admin" : "Suspended admin", account.FullName, null,
            account.IsActive ? "ok" : "danger");
    }

    // ---------- helpers ----------

    public static string KindLabel(ApplicantKind kind) =>
        kind == ApplicantKind.DomesticHelper ? "Domestic helper" : "User";

    public static string ScopeLabel(PlanScope scope) =>
        scope == PlanScope.Housing ? "Housing" : "Marketplace";

    public static string Taka(int amount) => "৳" + amount.ToString("N0");

    public static string Ago(DateTime when)
    {
        var span = DateTime.Now - when;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays} d ago";
        return when.ToString("d MMM yyyy");
    }

    private void Log(string action, string target, string? note, string kind)
    {
        _audit.Insert(0, new AuditEntry
        {
            Id = NextId("LOG"),
            Admin = CurrentAdmin,
            Action = action,
            Target = target,
            Note = string.IsNullOrWhiteSpace(note) ? null : note,
            Kind = kind,
            At = DateTime.Now
        });

        Changed?.Invoke();
    }

    private string NextId(string prefix) => $"{prefix}-{++_counter}";

    // ---------- sample data ----------

    private void SeedHousing()
    {
        // The same listings the browse page shows, plus the ones users reported.
        Add("One seat in a 3-bed flat, Dhanmondi",
            "Quiet mess near Dhanmondi 27. Two current tenants are AUST/DU students. Wifi, gas and a part-time cleaner included in rent.",
            "Single seat", 1, "Dhanmondi", "Dhaka", "Dhaka", "Arif Mahmud", true,
            "Male · students only · up to 27", 6500,
            Rooms("1522708323590-d24dbb6b0267", "1524758631624-e2822e304c36"), 0.2, 1);

        Add("Two seats, newly furnished flat in Mirpur-10",
            "Third floor, lift access, two seats open in a 4-seat flat. Owner lives in the same building.",
            "Multiple seats", 2, "Mirpur Model", "Dhaka", "Dhaka", "Nabila Haque", true,
            "Verified accounts only", 5200,
            Rooms("1501183638710-841dd1904471", "1493809842364-78817add7ffb"), 1.1, 0);

        Add("Entire 2-bed house available, Mohammadpur",
            "Full house handover, current tenants are relocating end of month. Two bedrooms, one common room, small rooftop access.",
            "Entire house", 4, "Mohammadpur", "Dhaka", "Dhaka", "Tuhin Rahman", true,
            "Anyone", 18000,
            Rooms("1560448204-e02f11c3d0e2", "1560185007-cde436f6a4d0", "1484154218962-a197022b5858"), 2, 1);

        Add("Seat near Tongi Bus Stand",
            "Single seat in a four-seat mess, five minutes from the bus stand. Kitchen and bathroom shared.",
            "Single seat", 1, "Tongi East", "Gazipur", "Dhaka", "Shafiqul Islam", false,
            "Students only", 4000,
            Rooms("1505873242700-f289a29e1e0f", "1522708323590-d24dbb6b0267"), 3, 0);

        Add("Single seat, walking distance to Shahjalal University",
            "Small quiet flat with two students already in. Good for someone who wants a calm study environment.",
            "Single seat", 1, "Sylhet Sadar", "Sylhet", "Sylhet", "Mizanur Rahman", true,
            "Male · single · 18 to 30", 3800,
            Rooms("1502005229762-cf1b2da7c5d6", "1598928506311-c55ded91a20c", "1616486338812-3dadae4b4ace"), 4, 0);

        Add("Seat in Panchlaish, Chattogram",
            "One seat free in a three-seat flat near Panchlaish thana. Attached bath, gas line, standby generator.",
            "Single seat", 1, "Panchlaish", "Chattogram", "Chattogram", "Sabbir Ahmed", false,
            "Female only", 5000,
            Rooms("1522771739844-6a9f6d5f14af", "1524758631624-e2822e304c36"), 6, 0);

        Add("Furnished flat at half the market rent, Bashundhara",
            "Fully furnished 3-bed at 6,000 taka only. Send the booking money on bKash today, flat will be handed over tomorrow.",
            "Entire house", 3, "Bhatara", "Dhaka", "Dhaka", "Shamim Hossain", false,
            "Anyone", 6000,
            Rooms("1560185007-cde436f6a4d0", "1484154218962-a197022b5858", "1501183638710-841dd1904471"), 0.1, 3);

        Add("Single seat, Mirpur 10 — urgent urgent urgent",
            "Seat available. Call now. Seat available. Call now. Seat available. Call now.",
            "Single seat", 1, "Mirpur Model", "Dhaka", "Dhaka", "Jamil Uddin", false,
            "Anyone", 3500,
            Rooms("1493809842364-78817add7ffb", "1505873242700-f289a29e1e0f"), 0.5, 2);

        Add("Boys mess seat near NSU, Bashundhara R/A",
            "Two seats open in block D. Meals arranged by the mess, 3,500 extra per month if you want them.",
            "Multiple seats", 2, "Bhatara", "Dhaka", "Dhaka", "Imran Sheikh", true,
            "Male · students only", 7600,
            Rooms("1484154218962-a197022b5858", "1522708323590-d24dbb6b0267", "1560448204-e02f11c3d0e2"), 5, 0);

        Add("Sublet for six months, Uttara Sector 7",
            "Going abroad for a semester, subletting my one-bed flat furnished. Rent fixed for the whole period.",
            "Entire house", 2, "Uttara Paschim", "Dhaka", "Dhaka", "Farhana Akter", true,
            "Anyone", 16000,
            Rooms("1598928506311-c55ded91a20c", "1616486338812-3dadae4b4ace", "1502005229762-cf1b2da7c5d6"), 8, 0);

        Add("Two seats in a girls mess, Rajshahi Court",
            "Clean mess run by the owner's family. Study table and almirah for each seat, meals included.",
            "Multiple seats", 2, "Rajshahi Court", "Rajshahi", "Rajshahi", "Rehana Parvin", true,
            "Female · students only", 4600,
            Rooms("1524758631624-e2822e304c36", "1598928506311-c55ded91a20c"), 12, 0);

        Add("Family flat, ground floor, Agrabad",
            "Two bedrooms with attached veranda, close to the commercial area. Water and gas bill included in rent.",
            "Entire house", 4, "Double Mooring", "Chattogram", "Chattogram", "Nurul Absar", true,
            "Anyone", 13500,
            Rooms("1560448204-e02f11c3d0e2", "1502005229762-cf1b2da7c5d6"), 14, 0);

        Add("Seat in a quiet flat, Khulna Sadar",
            "One seat in a two-seat room. Roommate is a final year student, keeps the place tidy.",
            "Single seat", 1, "Khulna Sadar", "Khulna", "Khulna", "Asif Iqbal", false,
            "Male · students only", 3200,
            Rooms("1505873242700-f289a29e1e0f", "1522771739844-6a9f6d5f14af"), 16, 0);

        void Add(string title, string description, string listingType, int seats, string area, string district,
                 string division, string owner, bool verified, string eligibility, int rent, List<string> photos,
                 double daysAgo, int reports) =>
            _housing.Add(new HousingPost
            {
                Id = NextId("HSE"),
                Title = title,
                Description = description,
                ListingType = listingType,
                Seats = seats,
                Area = area,
                District = district,
                Division = division,
                Owner = owner,
                OwnerVerified = verified,
                Eligibility = eligibility,
                Rent = rent,
                Photos = photos,
                PostedOn = DateTime.Now.AddDays(-daysAgo),
                ReportCount = reports
            });
    }

    private void SeedMarket()
    {
        // Mirrors the marketplace browse grid, with two reported listings added.
        Add("Solid wood study table with drawer",
            "Bought two years ago from Hatil. One deep drawer, cable slot at the back, no wobble. Minor scuff on the left leg.",
            "Furniture", "Good", "Arif Mahmud", true, "New Market, Dhaka", 3200,
            Things("desk", 101, 102, 103), 0.25, 1);

        Add("Duranta single-speed cycle, 26 inch",
            "Daily commuter for one year. New tube and brake pads last month. Lock and light included.",
            "Other", "Good", "Tuhin Rahman", true, "Shahbag, Dhaka", 4500,
            Things("bicycle", 104, 105), 1.1, 0);

        Add("Miyako rice cooker 1.8L",
            "Cooks for four. Non-stick pot intact, spatula and measuring cup included. Just upgraded to a bigger one.",
            "Appliances", "Like new", "Nabila Haque", true, "Chackbazar, Dhaka", 1400,
            Things("ricecooker", 106, 107), 2, 0);

        Add("CSE first-year bundle: Deitel, Rosen, Thomas",
            "Three hardcovers. Rosen has some highlighting in the first four chapters, the rest are clean.",
            "Books", "Fair", "Arif Mahmud", true, "Shahbag, Dhaka", 1800,
            Things("books", 108, 109), 3.2, 0);

        Add("Dell 22 inch IPS monitor",
            "1080p, HDMI and VGA. No dead pixels. Stand and power cable included, HDMI cable not included.",
            "Electronics", "Good", "Farhan Kabir", false, "Chawkbazar, Chattogram", 7200,
            Things("monitor", 110, 111, 112), 4, 2);

        Add("Single foam mattress 3ft, six months used",
            "Clean, no stains, kept with a cover from day one. Firm side still firm. Selling with the cover.",
            "Bedding", "Like new", "Tuhin Rahman", true, "Lalbagh, Dhaka", 2600,
            Things("mattress", 113, 114), 5, 0);

        Add("Electric kettle 1.5L, stainless",
            "Auto cut-off works. A bit of scale at the bottom, comes off with vinegar. Two months old.",
            "Kitchen", "Good", "Nabila Haque", true, "Lalbagh, Dhaka", 650,
            Things("kettle", 115, 116), 6.1, 0);

        Add("Steel almirah, three shelves and locker",
            "Heavy, solid, lock and key present. Some paint chipping on top. You arrange the pickup van.",
            "Furniture", "Fair", "Farhan Kabir", false, "Kamrangirchar, Dhaka", 5500,
            Things("wardrobe", 117, 118), 8, 0);

        Add("Vision ceiling fan 56 inch",
            "Runs quiet, no wobble, full speed. Regulator included. Uninstalled and ready.",
            "Appliances", "Good", "Arif Mahmud", true, "Chackbazar, Dhaka", 1900,
            Things("ceilingfan", 119, 120), 9, 0);

        Add("Acoustic guitar with soft case",
            "Beginner guitar, stays in tune, no fret buzz. Soft case has a broken zipper on the front pocket only.",
            "Other", "Good", "Farhan Kabir", false, "Kotwali Model, Sylhet", 3800,
            Things("guitar", 121, 122, 123), 11, 0);

        Add("iPhone 13 sealed box, unbelievable price",
            "Brand new sealed iPhone 13 at 21,000 only. Full payment by bKash first, courier delivery same day, no meeting.",
            "Electronics", "New", "Rony Islam", false, "Mohammadpur, Dhaka", 21000,
            Things("smartphone", 131, 132, 133), 0.15, 4);

        Add("Gaming laptop RTX 3060 — urgent sale",
            "Selling fast, no questions. No box, no charger, no bill. Cash only, hand over tonight.",
            "Electronics", "Good", "Shakib Al Mamun", false, "Uttara, Dhaka", 42000,
            Things("laptop", 134, 135), 1.5, 3);

        Add("Study chair with cushion, adjustable",
            "Height adjustable, wheels roll smooth on tiles. Cushion has no tear. Two years old.",
            "Furniture", "Good", "Nabila Haque", true, "Mirpur 10, Dhaka", 2100,
            Things("officechair", 136, 137), 2.4, 0);

        Add("Casio fx-991EX scientific calculator",
            "Used for two semesters, all keys work, cover included. Battery and solar both fine.",
            "Other", "Like new", "Tuhin Rahman", true, "Shahbag, Dhaka", 1250,
            Things("calculator", 138, 139), 3.6, 0);

        Add("Table lamp with warm LED bulb",
            "Bendable neck, no flicker. Good for late night study. Bulb included.",
            "Appliances", "Good", "Arif Mahmud", true, "New Market, Dhaka", 480,
            Things("desklamp", 140, 141), 7, 0);

        Add("Two-burner gas stove, Walton",
            "Both burners light on the first click. Cleaned before listing. No gas pipe included.",
            "Kitchen", "Good", "Farhan Kabir", false, "Kamrangirchar, Dhaka", 1650,
            Things("gasstove", 142, 143), 10, 0);

        void Add(string title, string description, string category, string condition, string seller, bool verified,
                 string area, int price, List<string> photos, double daysAgo, int reports) =>
            _market.Add(new MarketItem
            {
                Id = NextId("MKT"),
                Title = title,
                Description = description,
                Category = category,
                Condition = condition,
                Seller = seller,
                SellerVerified = verified,
                Area = area,
                Price = price,
                Photos = photos,
                PostedOn = DateTime.Now.AddDays(-daysAgo),
                ReportCount = reports
            });
    }

    // Stock photos from the same sources the user-facing browse pages use.
    private static List<string> Rooms(params string[] ids) =>
        ids.Select(id => $"https://images.unsplash.com/photo-{id}?auto=format&fit=crop&q=80&w=900").ToList();

    private static List<string> Things(string keyword, params int[] photos) =>
        photos.Select(n => $"https://loremflickr.com/900/675/{keyword}?lock={n}").ToList();

    private void SeedReports()
    {
        ForHousing("Furnished flat at half the market rent, Bashundhara", "Fake listing",
            "The flat in the photos does not exist. He asks for 3,000 taka booking money on bKash before any visit.", "Mahin R.", 4);
        ForHousing("Single seat, Mirpur 10 — urgent urgent urgent", "Duplicate post",
            "The same seat has been posted four times this week with a different title each time.", "Sabbir A.", 9);
        ForHousing("Furnished flat at half the market rent, Bashundhara", "Scam or advance payment demand",
            "He took my advance and blocked my number. Two friends had the same thing happen.", "Ishrat J.", 22);
        ForHousing("One seat in a 3-bed flat, Dhanmondi", "Wrong rent",
            "Listed at 6,500 but the owner asked for 9,000 plus service charge when I visited.", "Nadia K.", 31);
        ForHousing("Entire 2-bed house available, Mohammadpur", "Misleading photos or rent",
            "The photos are of a different flat, the actual rooms are much smaller and there is no rooftop access.", "Tanim S.", 48);
        ForHousing("Single seat, Mirpur 10 — urgent urgent urgent", "Abusive or improper content",
            "The description has offensive wording about students from outside Dhaka.", "Rafi H.", 56);

        ForMarket("iPhone 13 sealed box, unbelievable price", "Scam",
            "Wants full payment before showing the phone and refuses to meet. The account was made two days ago.", "Rafi H.", 3);
        ForMarket("Gaming laptop RTX 3060 — urgent sale", "Counterfeit or stolen item",
            "Serial number matches a laptop reported stolen from the CSE lab last week.", "Tanim S.", 12);
        ForMarket("Dell 22 inch IPS monitor", "Misleading photos",
            "The photos are taken from an online shop listing, not the actual monitor.", "Jarin T.", 27);
        ForMarket("iPhone 13 sealed box, unbelievable price", "Prohibited item",
            "The same seller is posting sealed phones at impossible prices in three different areas.", "Munim F.", 33);
        ForMarket("Solid wood study table with drawer", "Already sold",
            "I bought this table a week ago but the post is still live and people keep messaging the seller.", "Nusrat H.", 40);
        ForMarket("Gaming laptop RTX 3060 — urgent sale", "Scam",
            "No box, no bill, no charger and he only wants cash at night. This looks stolen.", "Shuvo D.", 51);

        void ForHousing(string title, string reason, string details, string by, int hoursAgo)
        {
            var post = _housing.First(p => p.Title == title);
            _reports.Add(new PostReport
            {
                Id = NextId("RPT"),
                Scope = PlanScope.Housing,
                PostId = post.Id,
                PostTitle = post.Title,
                Reason = reason,
                Details = details,
                ReportedBy = by,
                RaisedOn = DateTime.Now.AddHours(-hoursAgo)
            });
        }

        void ForMarket(string title, string reason, string details, string by, int hoursAgo)
        {
            var item = _market.First(p => p.Title == title);
            _reports.Add(new PostReport
            {
                Id = NextId("RPT"),
                Scope = PlanScope.Marketplace,
                PostId = item.Id,
                PostTitle = item.Title,
                Reason = reason,
                Details = details,
                ReportedBy = by,
                RaisedOn = DateTime.Now.AddHours(-hoursAgo)
            });
        }
    }

    private void SeedApplications()
    {
        _applications.AddRange(new[]
        {
            New("Ruhul Amin", ApplicantKind.User, "NID", "1994738201", "01711-224466", "Dhanmondi, Dhaka", 100, 2),
            New("Shefali Begum", ApplicantKind.DomesticHelper, "NID", "1988420113", "01822-119933", "Mirpur 12, Dhaka", 150, 5),
            New("Tahmid Rahman", ApplicantKind.User, "Student ID", "CSE-19-0418", "01933-887711", "Palashi, Dhaka", 100, 9),
            New("Morjina Khatun", ApplicantKind.DomesticHelper, "Birth certificate", "20019134772", "01644-556677", "Badda, Dhaka", 150, 14),
            New("Sazzad Hossain", ApplicantKind.User, "Passport", "BX0442719", "01577-334455", "Agrabad, Chattogram", 100, 20),
            New("Rahima Akter", ApplicantKind.DomesticHelper, "NID", "1979330085", "01399-778822", "Uttara, Dhaka", 150, 27)
        });

        VerificationApplication New(string name, ApplicantKind kind, string docType, string docNo, string phone, string area, int fee, int hoursAgo) => new()
        {
            Id = NextId("VER"),
            Applicant = name,
            Kind = kind,
            DocumentType = docType,
            DocumentNumber = docNo,
            Phone = phone,
            Area = area,
            FeePaid = fee,
            SubmittedOn = DateTime.Now.AddHours(-hoursAgo)
        };
    }

    private void SeedPricing()
    {
        _fees.AddRange(new[]
        {
            new FeeSetting { Id = "fee-user", Label = "User verification", Description = "Charged once when a user submits an NID or student ID for verification.", Amount = 100 },
            new FeeSetting { Id = "fee-helper", Label = "Domestic helper verification", Description = "Charged when a helper applies, covers the manual document check.", Amount = 150 },
            new FeeSetting { Id = "fee-rehelper", Label = "Helper re-verification", Description = "Charged when a declined helper applies again with new documents.", Amount = 80 },
            new FeeSetting { Id = "fee-feature", Label = "Featured listing (per week)", Description = "Pins a housing or marketplace post to the top of search results.", Amount = 250 }
        });

        _plans.AddRange(new[]
        {
            new PostPlan { Id = NextId("PLN"), Name = "Starter", Scope = PlanScope.Housing, Posts = 10, Price = 20, ValidDays = 30, Subscribers = 418 },
            new PostPlan { Id = NextId("PLN"), Name = "Landlord", Scope = PlanScope.Housing, Posts = 30, Price = 50, ValidDays = 60, Subscribers = 173 },
            new PostPlan { Id = NextId("PLN"), Name = "Agency", Scope = PlanScope.Housing, Posts = 100, Price = 150, ValidDays = 90, Subscribers = 46 },
            new PostPlan { Id = NextId("PLN"), Name = "Casual seller", Scope = PlanScope.Marketplace, Posts = 20, Price = 10, ValidDays = 30, Subscribers = 692 },
            new PostPlan { Id = NextId("PLN"), Name = "Regular seller", Scope = PlanScope.Marketplace, Posts = 60, Price = 25, ValidDays = 60, Subscribers = 214 },
            new PostPlan { Id = NextId("PLN"), Name = "Shop", Scope = PlanScope.Marketplace, Posts = 200, Price = 70, ValidDays = 90, IsActive = false, Subscribers = 31 }
        });
    }

    private void SeedAdmins()
    {
        _admins.AddRange(new[]
        {
            new AdminAccount { Id = NextId("ADM"), FullName = "Kazi Ishmamul Haque", Email = "ishmam@nestify.com", Phone = "01710-000001", Scope = "Full access", CreatedOn = DateTime.Now.AddMonths(-11), LastActive = DateTime.Now.AddMinutes(-4) },
            new AdminAccount { Id = NextId("ADM"), FullName = "Tasnim Ahmed", Email = "tasnim@nestify.com", Phone = "01710-000002", Scope = "Verification", CreatedOn = DateTime.Now.AddMonths(-7), LastActive = DateTime.Now.AddHours(-3) },
            new AdminAccount { Id = NextId("ADM"), FullName = "Nafis Rahman", Email = "nafis@nestify.com", Phone = "01710-000003", Scope = "Moderation", CreatedOn = DateTime.Now.AddMonths(-4), LastActive = DateTime.Now.AddDays(-1) },
            new AdminAccount { Id = NextId("ADM"), FullName = "Ayesha Siddika", Email = "ayesha@nestify.com", Phone = "01710-000004", Scope = "Finance", CreatedOn = DateTime.Now.AddMonths(-2), LastActive = DateTime.Now.AddDays(-9), IsActive = false }
        });
    }

    private void SeedAudit()
    {
        Add("Nafis Rahman", "Removed post", "Marketplace · PS4 console clone, sealed", "Counterfeit product", "danger", 2);
        Add("Tasnim Ahmed", "Approved verification", "Jubayer Islam · User", null, "ok", 5);
        Add("Kazi Ishmamul Haque", "Updated fee", "Featured listing (per week)", "BDT 200 to BDT 250", "neutral", 9);
        Add("Nafis Rahman", "Resolved report", "Room heater 2000W, already sold", "Seller asked to repost when relisting", "ok", 20);
        Add("Tasnim Ahmed", "Declined verification", "Selim Mia · Domestic helper", "Document photo unreadable", "danger", 27);
        Add("Kazi Ishmamul Haque", "Created admin account", "Ayesha Siddika", "Finance", "ok", 49);
        Add("Nafis Rahman", "Removed post", "Housing · Whole flat 4,000 taka only, Badda", "Fake listing", "danger", 58);
        Add("Kazi Ishmamul Haque", "Disabled plan", "Shop", null, "danger", 74);

        void Add(string admin, string action, string target, string? note, string kind, int hoursAgo) =>
            _audit.Add(new AuditEntry
            {
                Id = NextId("LOG"),
                Admin = admin,
                Action = action,
                Target = target,
                Note = note,
                Kind = kind,
                At = DateTime.Now.AddHours(-hoursAgo)
            });
    }
}
