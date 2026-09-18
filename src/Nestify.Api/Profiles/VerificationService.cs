using Dapper;
using System.Data;
using System.Security.Cryptography;
using Npgsql;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Helpers;
using Nestify.Shared.Dtos.Admin;

namespace Nestify.Api.Profiles;

public sealed class VerificationService
{
    private readonly DbConnectionFactory _db;
    private readonly CloudinaryUploader _uploader;

    private const short Pending = 1;
    private const short Cancelled = 4;
    private const short User = 1;
    private const short DomesticHelper = 2;

    public VerificationService(DbConnectionFactory db, CloudinaryUploader uploader)
    {
        _db = db;
        _uploader = uploader;
    }

    public async Task<(HelperVerificationStatusDto? Data, string? Error)> GetHelperPendingAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var row = await connection.QuerySingleOrDefaultAsync<HelperVerificationStatusRow>("""
            SELECT d.document_type AS DocumentType, r.submitted_at_utc AS SubmittedAtUtc
            FROM verification_requests r
            JOIN verification_documents d ON d.verification_request_id = r.id
            WHERE r.user_id = @userId AND r.subject_type = @subjectType AND r.status = @pending
            ORDER BY d.uploaded_at_utc DESC
            LIMIT 1
            """, new { userId, subjectType = DomesticHelper, pending = Pending });

        return row is null
            ? (new HelperVerificationStatusDto { IsPending = false }, null)
            : (new HelperVerificationStatusDto
            {
                IsPending = true,
                DocumentType = DocumentTypeLabels.TryGetValue(row.DocumentType, out var label) ? label : null,
                SubmittedAtUtc = row.SubmittedAtUtc
            }, null);
    }

    public async Task<string?> SubmitHelperAsync(long userId, string documentType, Stream file, string fileName, string? contentType, long sizeBytes)
        => await SubmitDocumentAsync(userId, DomesticHelper, documentType, file, fileName, contentType, sizeBytes);

    public async Task<string?> SubmitUserAsync(long userId, string documentType, Stream file, string fileName, string? contentType, long sizeBytes)
        => await SubmitDocumentAsync(userId, User, documentType, file, fileName, contentType, sizeBytes);

    private async Task<string?> SubmitDocumentAsync(long userId, short subjectType, string documentType, Stream file, string fileName, string? contentType, long sizeBytes)
    {
        if (!DocumentTypeIds.TryGetValue(documentType.Trim(), out var documentTypeId))
        {
            return "Choose a valid verification document.";
        }

        if (sizeBytes <= 0 || sizeBytes > int.MaxValue)
        {
            return "The selected document is invalid.";
        }

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return "The selected document needs a file name.";
        }

        byte[] content;
        await using (var copy = new MemoryStream())
        {
            await file.CopyToAsync(copy);
            content = copy.ToArray();
        }

        if (content.LongLength != sizeBytes)
        {
            return "The document upload was incomplete. Please try again.";
        }

        using var connection = await _db.OpenAsync();
        var pending = await connection.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM verification_requests
                WHERE user_id = @userId AND subject_type = @subjectType AND status = @pending)
            """, new { userId, subjectType, pending = Pending });
        if (pending)
        {
            return "You already have a verification request under review.";
        }

        var storedFileName = Guid.NewGuid().ToString("N");
        await using var upload = new MemoryStream(content, writable: false);
        var (storageUrl, uploadError) = await _uploader.UploadAsync(upload, safeFileName, contentType);
        if (storageUrl is null)
        {
            return uploadError ?? "Could not store the verification document.";
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            var requestId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO verification_requests (user_id, subject_type)
                VALUES (@userId, @subjectType)
                RETURNING id
                """, new { userId, subjectType }, transaction);

            await connection.ExecuteAsync("""
                INSERT INTO verification_documents
                    (verification_request_id, document_type, stored_file_name,
                     original_file_name_sanitized, content_type, size_bytes,
                     sha256_hash, storage_url)
                VALUES
                    (@requestId, @documentTypeId, @storedFileName,
                     @safeFileName, @contentType, @sizeBytes,
                     @sha256Hash, @storageUrl)
                """, new
            {
                requestId,
                documentTypeId,
                storedFileName,
                safeFileName,
                contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                sizeBytes = (int)sizeBytes,
                sha256Hash = SHA256.HashData(content),
                storageUrl
            }, transaction);

            transaction.Commit();
            return null;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            transaction.Rollback();
            return "You already have a verification request under review.";
        }
    }

    public async Task<string?> CancelHelperAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync("""
            UPDATE verification_requests
            SET status = @cancelled, decided_at_utc = now()
            WHERE user_id = @userId AND subject_type = @subjectType AND status = @pending
            """, new { userId, subjectType = DomesticHelper, pending = Pending, cancelled = Cancelled });
        return changed == 0 ? "There is no pending verification application to cancel." : null;
    }

    public async Task<List<VerificationRequestDto>> GetQueueAsync()
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<VerificationRequestDto>("""
            SELECT v.id::text AS Id, u.full_name AS ApplicantName,
                   CASE v.subject_type WHEN 2 THEN 'Domestic Helper' ELSE 'User' END AS SubjectType,
                   CASE d.document_type
                       WHEN 1 THEN 'National ID (NID)'
                       WHEN 2 THEN 'Student ID'
                       WHEN 3 THEN 'Passport'
                       WHEN 4 THEN 'Birth certificate'
                   END AS DocumentType,
                   d.storage_url AS DocumentUrl, v.submitted_at_utc AS SubmittedUtc,
                   v.status - 1 AS Status
            FROM verification_requests v
            JOIN users u ON u.id = v.user_id
            LEFT JOIN LATERAL (
                SELECT document_type, storage_url FROM verification_documents
                WHERE verification_request_id = v.id
                ORDER BY uploaded_at_utc DESC LIMIT 1
            ) d ON true
            ORDER BY v.status, v.submitted_at_utc
            """);
        return rows.ToList();
    }

    public async Task<string?> CancelAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync(
            "UPDATE verification_requests SET status = 4, decided_at_utc = now() WHERE user_id = @userId AND subject_type = @subjectType AND status = 1",
            new { userId, subjectType = User });
        return changed == 0 ? "There is no pending verification application to cancel." : null;
    }

    public async Task<string?> DecideAsync(long requestId, long adminId, bool approve)
    {
        using var connection = await _db.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var request = await connection.QuerySingleOrDefaultAsync<VerificationDecisionRow>("SELECT user_id AS UserId, subject_type AS SubjectType FROM verification_requests WHERE id = @requestId AND status = 1 FOR UPDATE", new { requestId }, transaction);
        if (request is null) return "This verification request is no longer pending.";
        await connection.ExecuteAsync("UPDATE verification_requests SET status = @status, decided_at_utc = now(), decided_by_admin_id = @adminId WHERE id = @requestId", new { requestId, adminId, status = approve ? 2 : 3 }, transaction);
        if (approve && request.SubjectType == DomesticHelper)
            await connection.ExecuteAsync("UPDATE domestic_helper_profiles SET is_verified = true WHERE user_id = @userId", new { request.UserId }, transaction);
        else if (approve)
            await connection.ExecuteAsync("UPDATE user_additional_profile_info SET is_verified = true, updated_at_utc = now() WHERE user_id = @userId", new { request.UserId }, transaction);
        transaction.Commit();
        return null;
    }

    public static readonly HashSet<string> DocumentTypes = new(StringComparer.Ordinal)
    { "Student ID", "Birth Certificate", "National ID (NID)", "Employee ID / Workplace ID" };

    private static readonly IReadOnlyDictionary<string, short> DocumentTypeIds =
        new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
        {
            ["National ID (NID)"] = 1,
            ["Student ID"] = 2,
            ["Passport"] = 3,
            ["Birth certificate"] = 4,
            ["Birth Certificate"] = 4,
            ["Employee ID / Workplace ID"] = 2
        };

    private static readonly IReadOnlyDictionary<short, string> DocumentTypeLabels =
        new Dictionary<short, string>
        {
            [1] = "National ID (NID)",
            [2] = "Student ID",
            [3] = "Passport",
            [4] = "Birth certificate"
        };

    private sealed class HelperVerificationStatusRow
    {
        public short DocumentType { get; init; }
        public DateTime SubmittedAtUtc { get; init; }
    }

    private sealed class VerificationDecisionRow
    {
        public long UserId { get; init; }
        public short SubjectType { get; init; }
    }
}
