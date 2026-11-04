namespace BBDevPulse.Logic;

/// <summary>Indicates an invalid people CSV input.</summary>
internal sealed class PeopleCsvFormatException(string message) : FormatException(message);
