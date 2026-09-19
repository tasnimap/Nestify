using Dapper;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Api.Helpers;

public sealed class HelperWorkspaceService
{
    private const short Requested = 1;
    private const short Confirmed = 2;
    private const short Completed = 4;
    private const short Declined = 6;
    private readonly DbConnectionFactory _db;

    public HelperWorkspaceService(DbConnectionFactory db) => _db = db;

    public async Task<HelperWorkspaceDashboardDto?> GetDashboardAsync(long userId)
    {
        using var db = await _db.OpenAsync();
        var profile = await GetProfileAsync(db, userId);
        if (profile is null) return null;
        var today = DateTime.UtcNow.Date;
        var week = StartOfWeek(today);
        var summary = await db.QuerySingleAsync<DashboardRow>("""
            SELECT count(*) FILTER (WHERE e.status = @requested)::int AS PendingRequestCount,
                   count(*) FILTER (WHERE e.status = @confirmed)::int AS ActiveJobCount,
                   coalesce(sum(d.monthly_rate) FILTER (WHERE e.status = @confirmed), 0) AS MonthlyEarnings,
                   coalesce(p.average_rating, 0) AS RatingAverage, p.review_count AS ReviewCount
            FROM domestic_helper_profiles p LEFT JOIN service_engagements e ON e.helper_profile_id=p.id
            LEFT JOIN service_engagement_details d ON d.engagement_id=e.id WHERE p.id=@id
            GROUP BY p.average_rating,p.review_count
            """, new { id = profile.Value, requested = Requested, confirmed = Confirmed });
        var visits = await VisitsAsync(db, profile.Value, week, week.AddDays(7));
        return new HelperWorkspaceDashboardDto { PendingRequestCount=summary.PendingRequestCount, ActiveJobCount=summary.ActiveJobCount,
            MonthlyEarnings=summary.MonthlyEarnings, RatingAverage=(double)summary.RatingAverage, ReviewCount=summary.ReviewCount,
            HoursThisWeek=visits.Sum(x=>x.EndHour-x.StartHour), VisitsThisWeek=visits.Count,
            TodayVisits=visits.Where(x=>x.Date==today).ToList(), NewRequests=(await RequestsAsync(db, profile.Value)).Take(3).ToList(),
            OpenHours=await AvailabilityDaysAsync(db, profile.Value, week) };
    }

    public async Task<HelperAvailabilityDto?> GetAvailabilityAsync(long userId, DateTime weekStart)
    {
        using var db=await _db.OpenAsync(); var id=await GetProfileAsync(db,userId); if(id is null)return null;
        var open=(await db.QueryAsync<AvailabilityRow>("SELECT day_of_week AS DayOfWeek,hour AS Hour,is_open AS IsOpen FROM helper_weekly_availability WHERE helper_profile_id=@id",new{id})).ToDictionary(x=>(x.DayOfWeek,x.Hour),x=>x.IsOpen);
        var booked=(await db.QueryAsync<SlotRow>("SELECT extract(dow FROM s.visit_date)::int AS DayOfWeek,s.start_hour AS Hour FROM service_engagement_slots s JOIN service_engagements e ON e.id=s.engagement_id WHERE e.helper_profile_id=@id AND e.status=@confirmed AND s.visit_date>=@week AND s.visit_date<@end",new{id,confirmed=Confirmed,week=StartOfWeek(weekStart),end=StartOfWeek(weekStart).AddDays(7)})).Select(x=>(x.DayOfWeek,x.Hour)).ToHashSet();
        var result=new HelperAvailabilityDto { IsAvailable=await db.ExecuteScalarAsync<bool>("SELECT is_active FROM domestic_helper_profiles WHERE id=@id",new{id}) };
        for(var day=0;day<7;day++) for(var hour=6;hour<24;hour++) result.Slots.Add(new(){DayOfWeek=day,Hour=hour,IsOpen=open.GetValueOrDefault((day,hour)),IsBooked=booked.Contains((day,hour))});
        return result;
    }

    public async Task<string?> SaveAvailabilityAsync(long userId, HelperAvailabilityDto dto)
    {
        using var db=await _db.OpenAsync(); var id=await GetProfileAsync(db,userId); if(id is null)return "Create your helper profile first.";
        if(dto.Slots.Any(x=>x.DayOfWeek is <0 or >6 || x.Hour is <6 or >23)) return "Availability contains an invalid hour.";
        using var tx=db.BeginTransaction();
        await db.ExecuteAsync("UPDATE domestic_helper_profiles SET is_active=@active WHERE id=@id",new{id,active=dto.IsAvailable},tx);
        await db.ExecuteAsync("DELETE FROM helper_weekly_availability WHERE helper_profile_id=@id",new{id},tx);
        foreach(var slot in dto.Slots.Where(x=>x.IsOpen && !x.IsBooked).DistinctBy(x=>(x.DayOfWeek,x.Hour)))
            await db.ExecuteAsync("INSERT INTO helper_weekly_availability(helper_profile_id,day_of_week,hour,is_open) VALUES(@id,@day,@hour,true)",new{id,day=slot.DayOfWeek,hour=slot.Hour},tx);
        tx.Commit(); return null;
    }

    public async Task<HelperWorkspaceScheduleDto?> GetScheduleAsync(long userId, DateTime weekStart)
    { using var db=await _db.OpenAsync();var id=await GetProfileAsync(db,userId);if(id is null)return null;var start=StartOfWeek(weekStart);var visits=await VisitsAsync(db,id.Value,start,start.AddDays(7));var all=await VisitsAsync(db,id.Value,DateTime.UtcNow.Date,DateTime.MaxValue.Date);return new(){WeekStart=start,Visits=visits,NextVisit=all.FirstOrDefault(x=>x.Date>DateTime.UtcNow.Date || (x.Date==DateTime.UtcNow.Date&&x.EndHour>DateTime.UtcNow.Hour))}; }

    public async Task<HelperWorkspaceEngagementsDto?> GetEngagementsAsync(long userId)
    { using var db=await _db.OpenAsync();var id=await GetProfileAsync(db,userId);if(id is null)return null;var rows=await EngagementsAsync(db,id.Value);return new(){Requests=await RequestsAsync(db,id.Value),Current=rows.Where(x=>x.Status==Confirmed).ToList(),Past=rows.Where(x=>x.Status==Completed).ToList()}; }

    public async Task<string?> DecideAsync(long userId,string engagementId,bool accept,string? reason)
    { if(!long.TryParse(engagementId,out var eid))return "Request not found.";using var db=await _db.OpenAsync();var changed=await db.ExecuteAsync("UPDATE service_engagements e SET status=@status,helper_confirmed_at_utc=CASE WHEN @accept THEN now() ELSE helper_confirmed_at_utc END,cancelled_at_utc=CASE WHEN @accept THEN NULL ELSE now() END FROM domestic_helper_profiles p WHERE e.id=@eid AND e.helper_profile_id=p.id AND p.user_id=@userId AND e.status=@requested",new{eid,userId,requested=Requested,status=accept?Confirmed:Declined,accept});if(changed==0)return "Request not found or already answered.";if(!accept)await db.ExecuteAsync("UPDATE service_engagement_details SET decline_reason=@reason WHERE engagement_id=@eid",new{eid,reason=string.IsNullOrWhiteSpace(reason)?null:reason.Trim()});return null; }

    private static async Task<long?> GetProfileAsync(System.Data.IDbConnection db,long userId)=>await db.ExecuteScalarAsync<long?>("SELECT id FROM domestic_helper_profiles WHERE user_id=@userId",new{userId});
    private static DateTime StartOfWeek(DateTime day)=>day.Date.AddDays(-(((int)day.DayOfWeek-(int)DayOfWeek.Saturday+7)%7));
    private static async Task<List<HelperWorkspaceRequestDto>> RequestsAsync(System.Data.IDbConnection db,long id)
    { var rows=await db.QueryAsync<RequestRow>("SELECT e.id,u.full_name AS ClientName,coalesce(up.is_verified,false) AS ClientVerified,e.requested_at_utc AS RequestedAtUtc,d.area AS Area,d.home_type AS HomeType,d.services AS Services,d.monthly_rate AS OfferedRate,d.message AS Message,d.decline_reason AS DeclineReason FROM service_engagements e JOIN users u ON u.id=e.client_user_id LEFT JOIN user_additional_profile_info up ON up.user_id=u.id LEFT JOIN service_engagement_details d ON d.engagement_id=e.id WHERE e.helper_profile_id=@id AND e.status=@requested ORDER BY e.requested_at_utc DESC",new{id,requested=Requested});var slots=await db.QueryAsync<SlotWithEngagementRow>("SELECT engagement_id AS EngagementId,visit_date AS Date,start_hour AS Hour FROM service_engagement_slots WHERE engagement_id IN (SELECT id FROM service_engagements WHERE helper_profile_id=@id AND status=@requested)",new{id,requested=Requested});return rows.Select(r=>new HelperWorkspaceRequestDto{Id=r.Id.ToString(),ClientName=r.ClientName,ClientVerified=r.ClientVerified,RequestedAtUtc=r.RequestedAtUtc,Area=r.Area??"",HomeType=r.HomeType??"",Services=r.Services?.ToList()??new(),OfferedRate=r.OfferedRate,Message=r.Message??"",DeclineReason=r.DeclineReason,Slots=slots.Where(s=>s.EngagementId==r.Id).Select(s=>new EngagementSlotDto{Date=s.Date,Hour=s.Hour}).ToList()}).ToList(); }
    private static async Task<List<HelperWorkspaceVisitDto>> VisitsAsync(System.Data.IDbConnection db,long id,DateTime from,DateTime to)
    {var rows=await db.QueryAsync<VisitRow>("SELECT s.id,e.id AS EngagementId,u.full_name AS ClientName,coalesce(d.area,'') AS Area,coalesce(d.services[1],'General help') AS Service,s.visit_date AS Date,s.start_hour AS StartHour FROM service_engagement_slots s JOIN service_engagements e ON e.id=s.engagement_id JOIN users u ON u.id=e.client_user_id LEFT JOIN service_engagement_details d ON d.engagement_id=e.id WHERE e.helper_profile_id=@id AND e.status=@confirmed AND s.visit_date>=@from AND s.visit_date<@to ORDER BY s.visit_date,s.start_hour",new{id,confirmed=Confirmed,from,to});return rows.Select(x=>new HelperWorkspaceVisitDto{Id=x.Id.ToString(),EngagementId=x.EngagementId.ToString(),ClientName=x.ClientName,Area=x.Area,Service=x.Service,Date=x.Date,StartHour=x.StartHour,EndHour=x.StartHour+1,IsDone=x.Date<DateTime.UtcNow.Date||(x.Date==DateTime.UtcNow.Date&&x.StartHour<DateTime.UtcNow.Hour)}).ToList();}
    private static async Task<List<HelperWorkspaceEngagementDto>> EngagementsAsync(System.Data.IDbConnection db,long id)
    {var rows=await db.QueryAsync<EngagementWorkspaceRow>("SELECT e.id,u.full_name AS ClientName,coalesce(d.area,'') AS Area,coalesce(d.address,'') AS Address,coalesce(d.client_phone,'') AS Phone,coalesce(d.services,'{}') AS Services,coalesce(d.monthly_rate,0) AS MonthlyRate,e.start_date AS StartedOn,e.status AS Status,e.helper_completed_at_utc IS NOT NULL AS HelperMarkedComplete,e.client_completed_at_utc IS NOT NULL AS ClientMarkedComplete FROM service_engagements e JOIN users u ON u.id=e.client_user_id LEFT JOIN service_engagement_details d ON d.engagement_id=e.id WHERE e.helper_profile_id=@id AND e.status IN (@confirmed,@completed)",new{id,confirmed=Confirmed,completed=Completed});var slots=await db.QueryAsync<SlotWithEngagementRow>("SELECT s.engagement_id AS EngagementId,s.visit_date AS Date,s.start_hour AS Hour FROM service_engagement_slots s JOIN service_engagements e ON e.id=s.engagement_id WHERE e.helper_profile_id=@id",new{id});return rows.Select(r=>new HelperWorkspaceEngagementDto{Id=r.Id.ToString(),ClientName=r.ClientName,Area=r.Area,Address=r.Address,Phone=r.Phone,Services=r.Services?.ToList()??new(),MonthlyRate=r.MonthlyRate,StartedOn=r.StartedOn,Status=r.Status,WeeklyHours=slots.Count(s=>s.EngagementId==r.Id),VisitsPlanned=slots.Count(s=>s.EngagementId==r.Id),VisitsDone=slots.Count(s=>s.EngagementId==r.Id&&s.Date<DateTime.UtcNow.Date),HelperMarkedComplete=r.HelperMarkedComplete,ClientMarkedComplete=r.ClientMarkedComplete}).ToList();}
    private static async Task<List<HelperAvailabilityDayDto>> AvailabilityDaysAsync(System.Data.IDbConnection db,long id,DateTime week){var slots=await db.QueryAsync<AvailabilityRow>("SELECT day_of_week AS DayOfWeek,hour AS Hour,is_open AS IsOpen FROM helper_weekly_availability WHERE helper_profile_id=@id",new{id});return Enumerable.Range(0,7).Select(d=>new HelperAvailabilityDayDto{DayOfWeek=d,OpenHours=slots.Count(x=>x.DayOfWeek==d&&x.IsOpen)}).ToList();}
    private sealed class DashboardRow {public int PendingRequestCount{get;set;}public int ActiveJobCount{get;set;}public decimal MonthlyEarnings{get;set;}public decimal RatingAverage{get;set;}public int ReviewCount{get;set;}}
    private sealed class AvailabilityRow {public int DayOfWeek{get;set;}public int Hour{get;set;}public bool IsOpen{get;set;}}
    private class SlotRow {public int DayOfWeek{get;set;}public int Hour{get;set;}}
    private sealed class SlotWithEngagementRow:SlotRow {public long EngagementId{get;set;}public DateTime Date{get;set;}}
    private sealed class RequestRow {public long Id{get;set;}public string ClientName{get;set;}="";public bool ClientVerified{get;set;}public DateTime RequestedAtUtc{get;set;}public string? Area{get;set;}public string? HomeType{get;set;}public string[]? Services{get;set;}public decimal OfferedRate{get;set;}public string? Message{get;set;}public string? DeclineReason{get;set;}}
    private sealed class VisitRow {public long Id{get;set;}public long EngagementId{get;set;}public string ClientName{get;set;}="";public string Area{get;set;}="";public string Service{get;set;}="";public DateTime Date{get;set;}public int StartHour{get;set;}}
    private sealed class EngagementWorkspaceRow {public long Id{get;set;}public string ClientName{get;set;}="";public string Area{get;set;}="";public string Address{get;set;}="";public string Phone{get;set;}="";public string[]? Services{get;set;}public decimal MonthlyRate{get;set;}public DateTime StartedOn{get;set;}public short Status{get;set;}public bool HelperMarkedComplete{get;set;}public bool ClientMarkedComplete{get;set;}}
}
