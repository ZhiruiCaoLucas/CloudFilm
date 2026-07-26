namespace CloudFilm.Retrieve.Hospital;

/// <summary>
/// The result of probing a hospital imaging source.
/// </summary>
/// <param name="IsReachable">Whether the source responded successfully.</param>
/// <param name="Detail">An operator facing description of the outcome.</param>
/// <param name="RoundTripTime">How long the probe took.</param>
public sealed record HospitalConnectivity(bool IsReachable, string Detail, TimeSpan RoundTripTime)
{
    /// <summary>
    /// Creates a successful probe result.
    /// </summary>
    /// <param name="roundTripTime">How long the probe took.</param>
    /// <returns>A reachable result.</returns>
    public static HospitalConnectivity Reachable(TimeSpan roundTripTime) =>
        new(true, "Reachable.", roundTripTime);

    /// <summary>
    /// Creates a failed probe result.
    /// </summary>
    /// <param name="detail">Why the source could not be reached.</param>
    /// <param name="roundTripTime">How long the probe took before failing.</param>
    /// <returns>An unreachable result.</returns>
    public static HospitalConnectivity Unreachable(string detail, TimeSpan roundTripTime) =>
        new(false, detail, roundTripTime);
}
