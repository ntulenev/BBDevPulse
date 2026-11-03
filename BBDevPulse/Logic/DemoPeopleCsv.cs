using BBDevPulse.Models;

namespace BBDevPulse.Logic;

internal static class DemoPeopleCsv
{
    public static async Task<Dictionary<DisplayName, PersonCsvRow>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var stream = typeof(DemoPeopleCsv).Assembly.GetManifestResourceStream("BBDevPulse.Demo.people.csv")
            ?? throw new InvalidOperationException("The built-in demo people CSV is missing.");
        return await PeopleCsvProvider.ReadPeopleAsync(stream, cancellationToken).ConfigureAwait(false);
    }
}
