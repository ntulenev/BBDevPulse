namespace BBDevPulse.Models;

/// <summary>
/// Converts pull request size metric into a T-shirt size label.
/// </summary>
public static class PullRequestSizeClassifier
{
    /// <summary>
    /// Classifies pull request size.
    /// </summary>
    /// <param name="value">Metric value used by selected mode.</param>
    /// <param name="mode">Pull request size mode.</param>
    /// <returns>T-shirt size label.</returns>
    public static string Classify(int value, PullRequestSizeMode mode = PullRequestSizeMode.Lines)
    {
        return mode switch
        {
            PullRequestSizeMode.Lines => ClassifyByLines(value),
            PullRequestSizeMode.Files => ClassifyByFiles(value),
            _ => ClassifyByLines(value)
        };
    }

    private static string ClassifyByLines(int lineChurn)
    {
        if (lineChurn <= 100)
        {
            return "XS";
        }

        if (lineChurn <= 300)
        {
            return "S";
        }

        if (lineChurn <= 700)
        {
            return "M";
        }

        if (lineChurn <= 1200)
        {
            return "L";
        }

        return "XL";
    }

    private static string ClassifyByFiles(int filesChanged)
    {
        if (filesChanged <= 2)
        {
            return "XS";
        }

        if (filesChanged <= 5)
        {
            return "S";
        }

        if (filesChanged <= 10)
        {
            return "M";
        }

        if (filesChanged <= 20)
        {
            return "L";
        }

        return "XL";
    }
}
