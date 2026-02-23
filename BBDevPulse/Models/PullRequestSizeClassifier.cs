namespace BBDevPulse.Models;

/// <summary>
/// Converts pull request churn into a T-shirt size label.
/// </summary>
public static class PullRequestSizeClassifier
{
    /// <summary>
    /// Classifies pull request size by line churn.
    /// </summary>
    /// <param name="lineChurn">Total changed lines (added + removed).</param>
    /// <returns>T-shirt size label.</returns>
    public static string Classify(int lineChurn)
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
}
