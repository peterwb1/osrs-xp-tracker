namespace OsrsTracker.Api.Dtos;

public record AccountSummaryDto(int Id, string OsrsUsername, string DisplayName, DateTime CreatedAt, DateTime? LastPolledAt);
