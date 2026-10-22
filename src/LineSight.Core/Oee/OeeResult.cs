namespace LineSight.Core.Oee;

/// <summary>
/// OEE broken into its three factors. All values are ratios (0.85 = 85%), rounded to 4 decimals.
/// </summary>
/// <param name="PerformanceAboveIdeal">
/// True when Performance &gt; 100%. That is physically "faster than ideal", which almost always
/// means the station's ideal cycle time is configured wrong, so we flag it rather than hide it.
/// </param>
public sealed record OeeResult(
    double Availability,
    double Performance,
    double Quality,
    double Oee,
    bool PerformanceAboveIdeal);
