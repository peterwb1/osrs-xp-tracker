namespace OsrsTracker.Api.Dtos;

public record SkillSnapshotDto(int SkillId, string SkillName, int DisplayOrder, long? Xp, int? Level, int? Rank);
