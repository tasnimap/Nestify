using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Admin;
using Nestify.Shared.Dtos.Helpers;
using Nestify.Shared.Dtos.Profile;

namespace Nestify.Api.Profiles;

// Verification requests for users and domestic helpers (User_Verification.sql).
public sealed class VerificationService
{
    private readonly DbConnectionFactory _db;
    private readonly CloudinaryUploader _uploader;

    // verification_requests.status
    private const short Pending = 1;
    private const short Approved = 2;
    private const short Rejected = 3;
    private const short Cancelled = 4;

    // verification_requests.subject_type
    private const short User = 1;
    private const short DomesticHelper = 2;

    // verification_documents.document_type
    private const short NationalId = 1;
    private const short BirthCertificate = 2;
    private const short StudentId = 3;
    private const short EmployeeId = 4;
    private const short Passport = 5;
    private const short HelperPhoto = 6;

    private const string UserFeeCode = "user_verification";
    private const string HelperFeeCode = "helper_verification";
    private const string HelperReverifyFeeCode = "helper_reverify";
    private const decimal DefaultUserFee = 100;
    private const decimal DefaultHelperFee = 150;

    // 01XXXXXXXXX, optionally with +88 / 88 in front. The second digit is 3-9.
    private static readonly Regex BangladeshiPhone = new(@"^(\+?88)?01[3-9]\d{8}$", RegexOptions.Compiled);
    private static readonly Regex BkashPin = new(@"^\d{4,5}$", RegexOptions.Compiled);

    public VerificationService(DbConnectionFactory db, CloudinaryUploader uploader)
    {
        _db = db;
        _uploader = uploader;
    }

    // ------------------------------------------------------------ fee and payment

    public async Task<decimal> GetUserFeeAsync()
    {
        using var connection = await _db.OpenAsync();
        return await ReadFeeAsync(connection, UserFeeCode, DefaultUserFee);
    }

    // A helper who was rejected before pays the smaller re-verification fee.
    public async Task<decimal> GetHelperFeeAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var code = await HelperFeeCodeAsync(connection, userId);
        return await ReadFeeAsync(connection, code, DefaultHelperFee);
    }

    private static async Task<string> HelperFeeCodeAsync(IDbConnection connection, long userId)
    {
        var rejectedBefore = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM verification_requests WHERE user_id = @userId AND subject_type = @subjectType AND status = @rejected)",
            new { userId, subjectType = DomesticHelper, rejected = Rejected });
        return rejectedBefore ? HelperReverifyFeeCode : HelperFeeCode;
    }

    private static async Task<decimal> ReadFeeAsync(IDbConnection connection, string code, decimal fallback)
    {
        var amount = await connection.ExecuteScalarAsync<decimal?>(
            "SELECT amount_bdt FROM fee_settings WHERE code = @code", new { code });
        return amount ?? fallback;
    }

    public Task<(VerificationPaymentDto? Data, string? Error)> PayUserFeeAsync(long userId, BkashPaymentDto dto)
        => PayFeeAsync(userId, User, dto);

    public Task<(VerificationPaymentDto? Data, string? Error)> PayHelperFeeAsync(long userId, BkashPaymentDto dto)
        => PayFeeAsync(userId, DomesticHelper, dto);

    // The fake bKash portal. Any Bangladeshi number with any 4-5 digit PIN goes
    // through; the PIN is validated and dropped, only the number is kept.
    private async Task<(VerificationPaymentDto? Data, string? Error)> PayFeeAsync(long userId, short subjectType, BkashPaymentDto dto)
    {
        var number = new string((dto.BkashNumber ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        if (!BangladeshiPhone.IsMatch(number))
        {
            return (null, "Enter a valid Bangladeshi bKash number, like 01712345678.");
        }

        if (!BkashPin.IsMatch(dto.Pin ?? string.Empty))
        {
            return (null, "The bKash PIN must be 4 or 5 digits.");
        }

        // Keep the local 11-digit form so all rows look alike.
        number = "0" + number[^10..];

        using var connection = await _db.OpenAsync();

        var pendingRequest = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM verification_requests WHERE user_id = @userId AND subject_type = @subjectType AND status = @pending)",
            new { userId, subjectType, pending = Pending });
        if (pendingRequest)
        {
            return (null, "You already have a verification request under review.");
        }

        var feeCode = subjectType == DomesticHelper ? await HelperFeeCodeAsync(connection, userId) : UserFeeCode;
        var amount = await ReadFeeAsync(connection, feeCode, subjectType == DomesticHelper ? DefaultHelperFee : DefaultUserFee);
        var transactionId = NewTransactionId();

        var row = await connection.QuerySingleAsync<PaymentRow>("""
            INSERT INTO verification_payments (user_id, fee_code, amount_bdt, bkash_number, transaction_id)
            VALUES (@userId, @feeCode, @amount, @number, @transactionId)
            RETURNING id AS Id, transaction_id AS TransactionId, amount_bdt AS AmountBdt,
                      bkash_number AS BkashNumber, paid_at_utc AS PaidAtUtc
            """, new { userId, feeCode, amount, number, transactionId });

        return (new VerificationPaymentDto
        {
            PaymentId = row.Id.ToString(),
            TransactionId = row.TransactionId,
            AmountBdt = row.AmountBdt,
            BkashNumber = row.BkashNumber,
            PaidAtUtc = row.PaidAtUtc
        }, null);
    }

    // Looks like a real bKash trx id: 10 upper-case letters and digits.
    private static string NewTransactionId()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";
        return string.Create(10, alphabet, static (span, chars) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            }
        });
    }

    // ------------------------------------------------------------ user submission

    public sealed class UploadedFile
    {
        public Stream Content { get; init; } = Stream.Null;
        public string FileName { get; init; } = string.Empty;
        public string? ContentType { get; init; }
    }

    // identityDocumentType is "National ID (NID)" or "Birth certificate".
    // The second document is decided by the profile's occupation: a student
    // must send a student ID, a job holder an employee ID, everyone else nothing.
    public async Task<string?> SubmitUserAsync(long userId, string identityDocumentType, UploadedFile identityFile,
        UploadedFile? occupationFile, long paymentId)
    {
        var identityTypeId = identityDocumentType.Trim() switch
        {
            "National ID (NID)" => NationalId,
            "Birth certificate" => BirthCertificate,
            "Birth Certificate" => BirthCertificate,
            _ => (short)0
        };
        if (identityTypeId == 0)
        {
            return "Choose your NID or birth certificate as the identity document.";
        }

        var identityName = Path.GetFileName(identityFile.FileName);
        if (string.IsNullOrWhiteSpace(identityName))
        {
            return "The identity document needs a file name.";
        }

        using var connection = await _db.OpenAsync();

        var occupation = await connection.ExecuteScalarAsync<string?>(
            "SELECT occupation FROM user_additional_profile_info WHERE user_id = @userId", new { userId });
        var occupationTypeId = OccupationDocumentType(occupation);

        string? occupationName = null;
        if (occupationTypeId is not null)
        {
            if (occupationFile is null)
            {
                return occupationTypeId == StudentId
                    ? "Your profile says you are a student, so a student ID photo is required."
                    : "Your profile says you are a job holder, so an employee ID photo is required.";
            }

            occupationName = Path.GetFileName(occupationFile.FileName);
            if (string.IsNullOrWhiteSpace(occupationName))
            {
                return "The second document needs a file name.";
            }
        }

        var pending = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM verification_requests WHERE user_id = @userId AND subject_type = @subjectType AND status = @pending)",
            new { userId, subjectType = User, pending = Pending });
        if (pending)
        {
            return "You already have a verification request under review.";
        }

        var paymentOk = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM verification_payments WHERE id = @paymentId AND user_id = @userId AND verification_request_id IS NULL)",
            new { paymentId, userId });
        if (!paymentOk)
        {
            return "Pay the verification fee through bKash before sending the application.";
        }

        var (identityUrl, identityError) = await _uploader.UploadAsync(identityFile.Content, identityName, identityFile.ContentType);
        if (identityUrl is null)
        {
            return identityError ?? "Could not store the identity document.";
        }

        string? occupationUrl = null;
        if (occupationTypeId is not null)
        {
            var (url, error) = await _uploader.UploadAsync(occupationFile!.Content, occupationName!, occupationFile.ContentType);
            if (url is null)
            {
                return error ?? "Could not store the second document.";
            }
            occupationUrl = url;
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            var requestId = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO verification_requests (user_id, subject_type) VALUES (@userId, @subjectType) RETURNING id",
                new { userId, subjectType = User }, transaction);

            await InsertDocumentAsync(connection, transaction, requestId, identityTypeId, identityUrl, identityName);
            if (occupationTypeId is not null)
            {
                await InsertDocumentAsync(connection, transaction, requestId, occupationTypeId.Value, occupationUrl!, occupationName!);
            }

            var linked = await connection.ExecuteAsync(
                "UPDATE verification_payments SET verification_request_id = @requestId WHERE id = @paymentId AND user_id = @userId AND verification_request_id IS NULL",
                new { requestId, paymentId, userId }, transaction);
            if (linked == 0)
            {
                transaction.Rollback();
                return "This payment was already used for another application.";
            }

            transaction.Commit();
            return null;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            transaction.Rollback();
            return "You already have a verification request under review.";
        }
    }

    // Matches the occupation choices on the profile's edit dialog.
    private static short? OccupationDocumentType(string? occupation)
    {
        if (string.IsNullOrWhiteSpace(occupation)) return null;
        if (occupation.StartsWith("Student", StringComparison.OrdinalIgnoreCase)) return StudentId;
        if (occupation.Equals("Job holder", StringComparison.OrdinalIgnoreCase)) return EmployeeId;
        return null;
    }

    private static Task InsertDocumentAsync(IDbConnection connection, IDbTransaction transaction,
        long requestId, short documentType, string url, string fileName)
        => connection.ExecuteAsync("""
            INSERT INTO verification_documents (verification_request_id, document_type, document_url, original_file_name)
            VALUES (@requestId, @documentType, @url, @fileName)
            """, new { requestId, documentType, url, fileName = fileName.Length > 160 ? fileName[..160] : fileName }, transaction);

    public async Task<string?> CancelUserAsync(long userId) => await CancelAsync(userId, User);

    // ------------------------------------------------------------ helper submission

    public async Task<HelperVerificationStatusDto> GetHelperStatusAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var isVerified = await connection.ExecuteScalarAsync<bool>(
            "SELECT coalesce((SELECT is_verified FROM domestic_helper_profiles WHERE user_id = @userId), false)", new { userId });
        return await Nestify.Api.Helpers.HelperWorkspaceService.ReadVerificationAsync(connection, userId, isVerified);
    }

    // A helper sends a photo of herself and her NID, after paying the fee
    // through the bKash portal. The admin sees both next to her profile.
    public async Task<string?> SubmitHelperAsync(long userId, UploadedFile photoFile, UploadedFile nidFile, long paymentId)
    {
        var photoName = Path.GetFileName(photoFile.FileName);
        var nidName = Path.GetFileName(nidFile.FileName);
        if (string.IsNullOrWhiteSpace(photoName) || string.IsNullOrWhiteSpace(nidName))
        {
            return "Both photos need a file name.";
        }

        using var connection = await _db.OpenAsync();

        var hasProfile = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM domestic_helper_profiles WHERE user_id = @userId)", new { userId });
        if (!hasProfile)
        {
            return "Create your helper profile before applying for verification.";
        }

        var pending = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM verification_requests WHERE user_id = @userId AND subject_type = @subjectType AND status = @pending)",
            new { userId, subjectType = DomesticHelper, pending = Pending });
        if (pending)
        {
            return "You already have a verification request under review.";
        }

        var paymentOk = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM verification_payments WHERE id = @paymentId AND user_id = @userId AND verification_request_id IS NULL)",
            new { paymentId, userId });
        if (!paymentOk)
        {
            return "Pay the verification fee through bKash before sending the application.";
        }

        var (photoUrl, photoError) = await _uploader.UploadAsync(photoFile.Content, photoName, photoFile.ContentType);
        if (photoUrl is null)
        {
            return photoError ?? "Could not store your photo.";
        }

        var (nidUrl, nidError) = await _uploader.UploadAsync(nidFile.Content, nidName, nidFile.ContentType);
        if (nidUrl is null)
        {
            return nidError ?? "Could not store the NID photo.";
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            var requestId = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO verification_requests (user_id, subject_type) VALUES (@userId, @subjectType) RETURNING id",
                new { userId, subjectType = DomesticHelper }, transaction);

            await InsertDocumentAsync(connection, transaction, requestId, HelperPhoto, photoUrl, photoName);
            await InsertDocumentAsync(connection, transaction, requestId, NationalId, nidUrl, nidName);

            var linked = await connection.ExecuteAsync(
                "UPDATE verification_payments SET verification_request_id = @requestId WHERE id = @paymentId AND user_id = @userId AND verification_request_id IS NULL",
                new { requestId, paymentId, userId }, transaction);
            if (linked == 0)
            {
                transaction.Rollback();
                return "This payment was already used for another application.";
            }

            transaction.Commit();
            return null;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            transaction.Rollback();
            return "You already have a verification request under review.";
        }
    }

    public async Task<string?> CancelHelperAsync(long userId) => await CancelAsync(userId, DomesticHelper);

    private async Task<string?> CancelAsync(long userId, short subjectType)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync("""
            UPDATE verification_requests
            SET status = @cancelled, decided_at_utc = now()
            WHERE user_id = @userId AND subject_type = @subjectType AND status = @pending
            """, new { userId, subjectType, pending = Pending, cancelled = Cancelled });
        return changed == 0 ? "There is no pending verification application to cancel." : null;
    }

    // ------------------------------------------------------------ admin

    // Everything an admin needs to decide: the applicant's profile, the
    // documents and the payment. Withdrawn requests stay out of the queue.
    public async Task<List<VerificationRequestDto>> GetQueueAsync()
    {
        using var connection = await _db.OpenAsync();

        var requests = (await connection.QueryAsync<QueueRow>("""
            SELECT v.id AS Id, v.user_id AS UserId, v.subject_type AS SubjectType, v.status AS Status,
                   v.rejection_reason AS RejectionReason,
                   v.submitted_at_utc AS SubmittedUtc, v.decided_at_utc AS DecidedUtc,
                   u.full_name AS ApplicantName, u.email AS Email, u.phone_number AS Phone,
                   coalesce(hp.photo_url, p.profile_picture_url, '') AS ProfilePictureUrl,
                   p.occupation AS Occupation, p.organization_name AS OrganizationName,
                   p.date_of_birth AS DateOfBirth,
                   coalesce(p.is_smoker, false) AS IsSmoker, coalesce(p.is_drinker, false) AS IsDrinker,
                   pay.amount_bdt AS PaymentAmount, pay.bkash_number AS PaymentNumber,
                   pay.transaction_id AS PaymentTransactionId, pay.paid_at_utc AS PaymentPaidAtUtc
            FROM verification_requests v
            JOIN users u ON u.id = v.user_id
            LEFT JOIN user_additional_profile_info p ON p.user_id = v.user_id
            LEFT JOIN domestic_helper_profiles hp ON hp.user_id = v.user_id AND v.subject_type = @helper
            LEFT JOIN verification_payments pay ON pay.verification_request_id = v.id
            WHERE v.status <> @cancelled
            ORDER BY v.status, v.submitted_at_utc
            """, new { cancelled = Cancelled, helper = DomesticHelper })).ToList();

        var documents = (await connection.QueryAsync<DocumentRow>("""
            SELECT d.verification_request_id AS RequestId, d.document_type AS DocumentType,
                   d.document_url AS Url, d.original_file_name AS FileName
            FROM verification_documents d
            JOIN verification_requests v ON v.id = d.verification_request_id
            WHERE v.status <> @cancelled
            ORDER BY d.document_type DESC
            """, new { cancelled = Cancelled })).ToLookup(d => d.RequestId);

        // Helper requests carry her profile (Domestic_Help.sql) so the admin
        // can check the photo and NID against what she wrote about herself.
        var helperProfiles = (await connection.QueryAsync<HelperProfileRow>("""
            SELECT hp.user_id AS UserId, hp.headline AS Headline, coalesce(hp.bio, '') AS Bio,
                   hp.languages AS Languages, hp.monthly_rate AS MonthlyRate, hp.years_experience AS ExperienceYears,
                   coalesce(hp.average_rating, 0) AS RatingAverage, hp.review_count AS RatingCount,
                   coalesce(up.name || ', ' || di.name, '') AS AreaName, coalesce(a.address_line, '') AS AddressLine,
                   coalesce(a.latitude, 0) AS Latitude, coalesce(a.longitude, 0) AS Longitude,
                   (SELECT string_agg(hs.service_type::text, ',' ORDER BY hs.service_type) FROM helper_services hs WHERE hs.helper_profile_id = hp.id) AS ServiceTypes
            FROM domestic_helper_profiles hp
            LEFT JOIN helper_addresses a ON a.helper_profile_id = hp.id
            LEFT JOIN upazilas up ON up.id = a.upazila_id
            LEFT JOIN districts di ON di.id = up.district_id
            WHERE hp.user_id IN (SELECT user_id FROM verification_requests WHERE subject_type = @helper AND status <> @cancelled)
            """, new { helper = DomesticHelper, cancelled = Cancelled })).ToDictionary(h => h.UserId);

        return requests.Select(r => new VerificationRequestDto
        {
            Id = r.Id.ToString(),
            ApplicantName = r.ApplicantName,
            Email = r.Email,
            Phone = r.Phone,
            ProfilePictureUrl = string.IsNullOrWhiteSpace(r.ProfilePictureUrl) ? UserProfileDto.DefaultPictureUrl : r.ProfilePictureUrl,
            SubjectType = r.SubjectType == DomesticHelper ? "Domestic Helper" : "User",
            Occupation = r.Occupation,
            OrganizationName = r.OrganizationName,
            DateOfBirth = r.DateOfBirth,
            IsSmoker = r.IsSmoker,
            IsDrinker = r.IsDrinker,
            Documents = documents[r.Id].Select(d => new VerificationDocumentDto
            {
                DocumentType = DocumentLabel(d.DocumentType),
                Url = d.Url,
                FileName = d.FileName
            }).ToList(),
            Payment = r.PaymentTransactionId is null ? null : new VerificationPaymentSummaryDto
            {
                AmountBdt = r.PaymentAmount ?? 0,
                BkashNumber = r.PaymentNumber ?? string.Empty,
                TransactionId = r.PaymentTransactionId,
                PaidAtUtc = r.PaymentPaidAtUtc ?? r.SubmittedUtc
            },
            HelperProfile = r.SubjectType == DomesticHelper && helperProfiles.TryGetValue(r.UserId, out var helper)
                ? new VerificationHelperProfileDto
                {
                    Headline = helper.Headline,
                    Bio = helper.Bio,
                    Languages = helper.Languages,
                    Services = (helper.ServiceTypes ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => Nestify.Shared.Dtos.Helpers.ServiceTypes.Label((ServiceType)int.Parse(t)))
                        .ToList(),
                    MonthlyRate = helper.MonthlyRate,
                    ExperienceYears = helper.ExperienceYears,
                    AreaName = helper.AreaName,
                    AddressLine = helper.AddressLine,
                    Latitude = (double)helper.Latitude,
                    Longitude = (double)helper.Longitude,
                    RatingAverage = (double)helper.RatingAverage,
                    RatingCount = helper.RatingCount
                }
                : null,
            SubmittedUtc = r.SubmittedUtc,
            DecidedUtc = r.DecidedUtc,
            Status = (VerificationStatus)(r.Status - 1),
            RejectionReason = r.RejectionReason
        }).ToList();
    }

    public async Task<string?> DecideAsync(long requestId, long adminId, bool approve, string? reason)
    {
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!approve && reason is null)
        {
            return "Write the reason for rejecting this request.";
        }
        if (reason is { Length: > 500 })
        {
            reason = reason[..500];
        }

        using var connection = await _db.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var request = await connection.QuerySingleOrDefaultAsync<DecisionRow>(
            "SELECT v.user_id AS UserId, v.subject_type AS SubjectType, u.full_name AS ApplicantName FROM verification_requests v JOIN users u ON u.id = v.user_id WHERE v.id = @requestId AND v.status = @pending FOR UPDATE OF v",
            new { requestId, pending = Pending }, transaction);
        if (request is null)
        {
            return "This verification request is no longer pending.";
        }

        await connection.ExecuteAsync("""
            UPDATE verification_requests
            SET status = @status, rejection_reason = @reason, decided_at_utc = now(), decided_by_admin_id = @adminId
            WHERE id = @requestId
            """, new { requestId, adminId, status = approve ? Approved : Rejected, reason = approve ? null : reason }, transaction);

        if (approve)
        {
            if (request.SubjectType == DomesticHelper)
            {
                await connection.ExecuteAsync("UPDATE domestic_helper_profiles SET is_verified = true WHERE user_id = @userId",
                    new { request.UserId }, transaction);
            }
            else
            {
                await connection.ExecuteAsync("UPDATE user_additional_profile_info SET is_verified = true, updated_at_utc = now() WHERE user_id = @userId",
                    new { request.UserId }, transaction);
            }
        }

        await connection.ExecuteAsync(
            "INSERT INTO admin_audit_log (admin_user_id, action, target, note, kind) VALUES (@adminId, @action, @target, @note, @kind)",
            new
            {
                adminId,
                action = approve ? "Approved verification" : "Rejected verification",
                target = $"{(request.SubjectType == DomesticHelper ? "Helper" : "User")} {request.ApplicantName} (#{requestId})",
                note = reason,
                kind = approve ? "ok" : "danger"
            }, transaction);

        transaction.Commit();
        return null;
    }

    // ------------------------------------------------------------ rows

    private static string DocumentLabel(short type) => type switch
    {
        NationalId => "National ID (NID)",
        BirthCertificate => "Birth certificate",
        StudentId => "Student ID",
        EmployeeId => "Employee ID",
        Passport => "Passport",
        HelperPhoto => "Helper photo",
        _ => "Document"
    };

    private sealed class PaymentRow
    {
        public long Id { get; init; }
        public string TransactionId { get; init; } = string.Empty;
        public decimal AmountBdt { get; init; }
        public string BkashNumber { get; init; } = string.Empty;
        public DateTime PaidAtUtc { get; init; }
    }

    private sealed class DecisionRow
    {
        public long UserId { get; init; }
        public short SubjectType { get; init; }
        public string ApplicantName { get; init; } = string.Empty;
    }

    private sealed class QueueRow
    {
        public long Id { get; init; }
        public long UserId { get; init; }
        public short SubjectType { get; init; }
        public short Status { get; init; }
        public string? RejectionReason { get; init; }
        public DateTime SubmittedUtc { get; init; }
        public DateTime? DecidedUtc { get; init; }
        public string ApplicantName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string ProfilePictureUrl { get; init; } = string.Empty;
        public string? Occupation { get; init; }
        public string? OrganizationName { get; init; }
        public DateOnly? DateOfBirth { get; init; }
        public bool IsSmoker { get; init; }
        public bool IsDrinker { get; init; }
        public decimal? PaymentAmount { get; init; }
        public string? PaymentNumber { get; init; }
        public string? PaymentTransactionId { get; init; }
        public DateTime? PaymentPaidAtUtc { get; init; }
    }

    private sealed class HelperProfileRow
    {
        public long UserId { get; init; }
        public string Headline { get; init; } = string.Empty;
        public string Bio { get; init; } = string.Empty;
        public string Languages { get; init; } = string.Empty;
        public decimal MonthlyRate { get; init; }
        public int ExperienceYears { get; init; }
        public decimal RatingAverage { get; init; }
        public int RatingCount { get; init; }
        public string AreaName { get; init; } = string.Empty;
        public string AddressLine { get; init; } = string.Empty;
        public decimal Latitude { get; init; }
        public decimal Longitude { get; init; }
        public string? ServiceTypes { get; init; }
    }

    private sealed class DocumentRow
    {
        public long RequestId { get; init; }
        public short DocumentType { get; init; }
        public string Url { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
    }
}
