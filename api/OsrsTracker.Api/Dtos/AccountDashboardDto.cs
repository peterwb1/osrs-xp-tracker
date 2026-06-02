namespace OsrsTracker.Api.Dtos;

public record AccountDashboardDto(
    string DisplayName,
    string OsrsUsername,
    DateTime? LastPolledAt,
    int TotalLevel,
    long TotalXp,
    int CombatLevel,
    long XpGainedToday,
    long XpGainedThisWeek,
    DashboardSkillGainDto? FastestSkill,
    DashboardLevelUpDto? LastLevelUp);

public record DashboardSkillGainDto(string SkillName, long XpGained);

public record DashboardLevelUpDto(string SkillName, int Level, DateTime At);
