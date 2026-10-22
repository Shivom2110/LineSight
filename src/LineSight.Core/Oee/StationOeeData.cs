namespace LineSight.Core.Oee;

/// <summary>Total downtime for one reason within the OEE window.</summary>
public sealed record DowntimeSummary(string Reason, double DurationSeconds, int Occurrences);

/// <summary>What the database aggregates for one station: OEE inputs plus the downtime breakdown.</summary>
public sealed record StationOeeData(string StationName, OeeInput Input, IReadOnlyList<DowntimeSummary> Downtime);
