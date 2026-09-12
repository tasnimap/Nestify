using Dapper;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Admin;

namespace Nestify.Api.Profiles;

public sealed class VerificationService
{
    private readonly DbConnectionFactory _db;
    public VerificationService(DbConnectionFactory db) => _db = db;

    public async Task<string?> SubmitAsync(long userId, string documentType, string documentUrl, string fileName)
    {
        if (!DocumentTypes.Contains(documentType)) return "Choose a valid verification document.";
        using var connection = await _db.OpenAsync();
        var pending = await connection.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM verification_requests WHERE user_id = @userId AND status = 1)", new { userId });
        if (pending) return "You already have a verification request under review.";
        await connection.ExecuteAsync("INSERT INTO verification_requests (user_id, document_type, document_url, original_file_name) VALUES (@userId, @documentType, @documentUrl, @fileName)", new { userId, documentType, documentUrl, fileName = Path.GetFileName(fileName) });
        return null;
    }

    public async Task<List<VerificationRequestDto>> GetQueueAsync()
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<VerificationRequestDto>("""
            SELECT v.id::text AS Id, u.full_name AS ApplicantName, 'User' AS SubjectType,
                   v.document_type AS DocumentType, v.document_url AS DocumentUrl,
                   v.submitted_at_utc AS SubmittedUtc, v.status - 1 AS Status
            FROM verification_requests v JOIN users u ON u.id = v.user_id
            ORDER BY v.status, v.submitted_at_utc
            """);
        return rows.ToList();
    }

    public async Task<string?> CancelAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync(
            "UPDATE verification_requests SET status = 4, decided_at_utc = now() WHERE user_id = @userId AND status = 1",
            new { userId });
        return changed == 0 ? "There is no pending verification application to cancel." : null;
    }

    public async Task<string?> DecideAsync(long requestId, long adminId, bool approve)
    {
        using var connection = await _db.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var userId = await connection.ExecuteScalarAsync<long?>("SELECT user_id FROM verification_requests WHERE id = @requestId AND status = 1 FOR UPDATE", new { requestId }, transaction);
        if (userId is null) return "This verification request is no longer pending.";
        await connection.ExecuteAsync("UPDATE verification_requests SET status = @status, decided_at_utc = now(), decided_by_admin_id = @adminId WHERE id = @requestId", new { requestId, adminId, status = approve ? 2 : 3 }, transaction);
        if (approve) await connection.ExecuteAsync("UPDATE user_additional_profile_info SET is_verified = true, updated_at_utc = now() WHERE user_id = @userId", new { userId }, transaction);
        transaction.Commit();
        return null;
    }

    public static readonly HashSet<string> DocumentTypes = new(StringComparer.Ordinal)
    { "Student ID", "Birth Certificate", "National ID (NID)", "Employee ID / Workplace ID" };
}
