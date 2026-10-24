using System.Security.Cryptography;
using System.Text;
using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class ResultRepository : IResultRepository
{
    private const string KeyPrefixTag = "imr_";

    private readonly AppDbContext _context;

    public ResultRepository(AppDbContext context)
    {
        _context = context;
    }

    // ---- reading ----

    public async Task<PagedResult<ResultSummaryDto>> SearchAsync(ResultSearchDto search)
    {
        var query = ScopedQuery(search.ClinicId, search.PractitionerId);

        if (search.Status.HasValue) query = query.Where(r => r.Status == search.Status.Value);
        if (search.ClientId.HasValue) query = query.Where(r => r.ClientId == search.ClientId.Value);
        if (search.AbnormalOnly) query = query.Where(r => r.HasAbnormal);

        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var term = search.Query.Trim().ToLower();
            query = query.Where(r =>
                (r.PatientFamilyName != null && r.PatientFamilyName.ToLower().Contains(term)) ||
                (r.PatientGivenName != null && r.PatientGivenName.ToLower().Contains(term)) ||
                (r.ReportName != null && r.ReportName.ToLower().Contains(term)) ||
                r.SenderName.ToLower().Contains(term) ||
                (r.Client != null && (r.Client.LastName.ToLower().Contains(term) || r.Client.FirstName.ToLower().Contains(term))));
        }

        var page = Math.Max(search.Page, 1);
        var pageSize = Math.Clamp(search.PageSize, 1, 100);
        var total = await query.CountAsync();

        // Abnormal first, then newest — what a doctor most needs to see at the top of the inbox.
        var rows = await query
            .Include(r => r.Client)
            .Include(r => r.AssignedPractitioner)
            .OrderByDescending(r => r.HasAbnormal)
            .ThenByDescending(r => r.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ResultSummaryDto>
        {
            Items = rows.Select(ToSummary).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ResultDetailDto?> GetByIdAsync(int id)
    {
        var r = await _context.ResultReports
            .Include(x => x.Client)
            .Include(x => x.AssignedPractitioner)
            .Include(x => x.Items)
            .Include(x => x.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return null;

        string? reviewer = null;
        if (r.ReviewedByUserId != null)
        {
            reviewer = await _context.Users
                .Where(u => u.Id == r.ReviewedByUserId)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync();
        }

        var dto = new ResultDetailDto
        {
            MessageControlId = r.MessageControlId,
            PatientIdentifier = r.PatientIdentifier,
            PatientSex = r.PatientSex,
            PatientMedicareNumber = r.PatientMedicareNumber,
            OrderingProviderNumber = r.OrderingProviderNumber,
            PlacerOrderNumber = r.PlacerOrderNumber,
            FillerOrderNumber = r.FillerOrderNumber,
            ReportedAt = r.ReportedAt,
            ReviewedAt = r.ReviewedAt,
            ReviewedByName = string.IsNullOrWhiteSpace(reviewer) ? null : reviewer.Trim(),
            ReviewComment = r.ReviewComment,
            HoldNote = r.HoldNote,
            Items = r.Items.OrderBy(i => i.Sequence).Select(i => new ResultItemDto
            {
                Sequence = i.Sequence,
                Code = i.Code,
                Name = i.Name,
                Value = i.Value,
                Units = i.Units,
                ReferenceRange = i.ReferenceRange,
                AbnormalFlag = i.AbnormalFlag,
                IsAbnormal = i.IsAbnormal,
                ResultStatus = i.ResultStatus,
                Comment = i.Comment
            }).ToList(),
            Attachments = r.Attachments.Select(a => new ResultAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType
            }).ToList()
        };
        CopySummary(ToSummary(r), dto);
        return dto;
    }

    public async Task<ResultCountsDto> GetCountsAsync(int? clinicId, int? practitionerId)
    {
        var query = ScopedQuery(clinicId, practitionerId);
        var grouped = await query
            .Where(r => r.Status != ResultStatus.Archived)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Abnormal = g.Count(r => r.HasAbnormal) })
            .ToListAsync();

        return new ResultCountsDto
        {
            Inbox = grouped.FirstOrDefault(g => g.Status == ResultStatus.Inbox)?.Count ?? 0,
            InboxAbnormal = grouped.FirstOrDefault(g => g.Status == ResultStatus.Inbox)?.Abnormal ?? 0,
            Unmatched = grouped.FirstOrDefault(g => g.Status == ResultStatus.Unmatched)?.Count ?? 0,
            Held = grouped.FirstOrDefault(g => g.Status == ResultStatus.Held)?.Count ?? 0
        };
    }

    public Task<ResultAttachment?> GetAttachmentAsync(int reportId, int attachmentId) =>
        _context.ResultAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.ResultReportId == reportId);

    public Task<int?> GetClinicIdAsync(int id) =>
        _context.ResultReports.Where(r => r.Id == id).Select(r => (int?)r.ClinicId).FirstOrDefaultAsync();

    // ---- review actions ----

    public async Task<bool> MatchAsync(int id, int clientId, bool rememberLabIdentifier, string userId)
    {
        var report = await _context.ResultReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report == null || report.Status == ResultStatus.Archived) return false;

        // The client must belong to the report's clinic — never file across clinics.
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.ClinicId == report.ClinicId);
        if (client == null) return false;

        report.ClientId = clientId;
        report.MatchedByUserId = userId;
        report.MatchedAt = DateTime.UtcNow;
        if (report.Status == ResultStatus.Unmatched) report.Status = ResultStatus.Inbox;

        if (rememberLabIdentifier && !string.IsNullOrWhiteSpace(report.PatientIdentifier))
        {
            var known = await _context.ClientLabIdentifiers.AnyAsync(l =>
                l.ClientId == clientId && l.SenderName == report.SenderName && l.Identifier == report.PatientIdentifier);
            if (!known)
            {
                _context.ClientLabIdentifiers.Add(new ClientLabIdentifier
                {
                    ClientId = clientId,
                    SenderName = report.SenderName,
                    Identifier = report.PatientIdentifier!
                });
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AcknowledgeAsync(int id, string? comment, string userId)
    {
        var report = await _context.ResultReports.FirstOrDefaultAsync(r => r.Id == id);
        // An unmatched report has no patient to attach a review to — it must be matched first.
        if (report == null || report.Status is ResultStatus.Unmatched or ResultStatus.Archived) return false;

        report.Status = ResultStatus.Archived;
        report.ReviewedByUserId = userId;
        report.ReviewedAt = DateTime.UtcNow;
        report.ReviewComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HoldAsync(int id, string? note)
    {
        var report = await _context.ResultReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report == null || report.Status != ResultStatus.Inbox) return false;

        report.Status = ResultStatus.Held;
        report.HoldNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReopenAsync(int id)
    {
        var report = await _context.ResultReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report == null || report.Status is not (ResultStatus.Held or ResultStatus.Archived)) return false;

        report.Status = ResultStatus.Inbox;
        report.HoldNote = null;
        await _context.SaveChangesAsync();
        return true;
    }

    // ---- ingest keys ----

    public async Task<List<ResultIngestKeyDto>> GetIngestKeysAsync(int? clinicId)
    {
        var query = _context.ResultIngestKeys.AsQueryable();
        if (clinicId.HasValue) query = query.Where(k => k.ClinicId == clinicId.Value);

        return await query
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ResultIngestKeyDto
            {
                Id = k.Id,
                ClinicId = k.ClinicId,
                Label = k.Label,
                KeyPrefix = k.KeyPrefix,
                IsActive = k.IsActive,
                CreatedAt = k.CreatedAt,
                LastUsedAt = k.LastUsedAt
            })
            .ToListAsync();
    }

    public async Task<CreatedResultIngestKeyDto> CreateIngestKeyAsync(int clinicId, string label)
    {
        var key = KeyPrefixTag + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var entity = new ResultIngestKey
        {
            ClinicId = clinicId,
            Label = label.Trim(),
            KeyPrefix = key[..8],
            KeyHash = Hash(key)
        };
        _context.ResultIngestKeys.Add(entity);
        await _context.SaveChangesAsync();

        return new CreatedResultIngestKeyDto
        {
            Id = entity.Id,
            ClinicId = entity.ClinicId,
            Label = entity.Label,
            KeyPrefix = entity.KeyPrefix,
            IsActive = true,
            CreatedAt = entity.CreatedAt,
            Key = key
        };
    }

    public async Task<bool> RevokeIngestKeyAsync(int id)
    {
        var key = await _context.ResultIngestKeys.FirstOrDefaultAsync(k => k.Id == id);
        if (key == null) return false;
        key.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int?> ResolveIngestKeyAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(KeyPrefixTag, StringComparison.Ordinal)) return null;

        var hash = Hash(key.Trim());
        var entity = await _context.ResultIngestKeys.FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive);
        if (entity == null) return null;

        entity.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return entity.ClinicId;
    }

    // ---- helpers ----

    /// <summary>
    /// A practitioner sees their own reports plus the clinic-wide (unassigned) ones; null means all.
    /// Unmatched reports have no patient yet and are shown to everyone with access, since they are
    /// whoever's job it is to match them.
    /// </summary>
    private IQueryable<ResultReport> ScopedQuery(int? clinicId, int? practitionerId)
    {
        var query = _context.ResultReports.AsQueryable();
        if (clinicId.HasValue) query = query.Where(r => r.ClinicId == clinicId.Value);
        if (practitionerId.HasValue)
        {
            var pid = practitionerId.Value;
            query = query.Where(r => r.AssignedPractitionerId == null
                                     || r.AssignedPractitionerId == pid
                                     || r.Status == ResultStatus.Unmatched);
        }
        return query;
    }

    private static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static ResultSummaryDto ToSummary(ResultReport r) => new()
    {
        Id = r.Id,
        Status = r.Status,
        Kind = r.Kind,
        ClientId = r.ClientId,
        ClientName = r.Client == null ? null : $"{r.Client.FirstName} {r.Client.LastName}".Trim(),
        PatientNameAsReceived = string.Join(" ", new[] { r.PatientGivenName, r.PatientFamilyName }.Where(s => !string.IsNullOrWhiteSpace(s))),
        PatientDateOfBirth = r.PatientDateOfBirth,
        SenderName = r.SenderName,
        ReportName = r.ReportName,
        CollectedAt = r.CollectedAt,
        ReceivedAt = r.ReceivedAt,
        HasAbnormal = r.HasAbnormal,
        AssignedPractitionerId = r.AssignedPractitionerId,
        AssignedPractitionerName = r.AssignedPractitioner == null
            ? null
            : $"{r.AssignedPractitioner.Title} {r.AssignedPractitioner.FirstName} {r.AssignedPractitioner.LastName}".Replace("  ", " ").Trim(),
        OrderingProviderName = r.OrderingProviderName
    };

    private static void CopySummary(ResultSummaryDto from, ResultDetailDto to)
    {
        to.Id = from.Id;
        to.Status = from.Status;
        to.Kind = from.Kind;
        to.ClientId = from.ClientId;
        to.ClientName = from.ClientName;
        to.PatientNameAsReceived = from.PatientNameAsReceived;
        to.PatientDateOfBirth = from.PatientDateOfBirth;
        to.SenderName = from.SenderName;
        to.ReportName = from.ReportName;
        to.CollectedAt = from.CollectedAt;
        to.ReceivedAt = from.ReceivedAt;
        to.HasAbnormal = from.HasAbnormal;
        to.AssignedPractitionerId = from.AssignedPractitionerId;
        to.AssignedPractitionerName = from.AssignedPractitionerName;
        to.OrderingProviderName = from.OrderingProviderName;
    }
}
