namespace BBDevPulse.Models;

/// <summary>Describes whether supplemental data was obtained.</summary>
public enum DataAvailability
{
    /// <summary>The source or an older cache does not distinguish the reason.</summary>
    Unknown = 0,
    /// <summary>The data was not requested.</summary>
    NotRequested = 1,
    /// <summary>The request did not yield usable data.</summary>
    Unavailable = 2,
    /// <summary>The data was obtained, including an empty or zero result.</summary>
    Available = 3,
    /// <summary>An aggregate is missing some or all source data.</summary>
    Incomplete = 4
}
