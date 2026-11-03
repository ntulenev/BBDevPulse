using System.Text;
using System.Globalization;

namespace BBDevPulse.Configuration;

internal static class ReportOutputPath
{
    public static string Resolve(string? configuredPath, string defaultPath, string extension, string? teamFilter)
    {
        var candidatePath = string.IsNullOrWhiteSpace(configuredPath) ? defaultPath : configuredPath.Trim();
        var absolutePath = Path.GetFullPath(candidatePath);
        var directory = Path.GetDirectoryName(absolutePath)!;
        var fileExtension = Path.GetExtension(absolutePath);
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            fileExtension = extension;
        }

        var fileName = Path.GetFileNameWithoutExtension(absolutePath);
        var teamSlug = Slugify(teamFilter);
        if (teamSlug.Length > 0)
        {
            fileName = teamSlug + "-" + fileName;
        }

        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        return Path.Combine(directory, fileName + "_" + dateSuffix + fileExtension);
    }

    private static string Slugify(string? team)
    {
        if (string.IsNullOrWhiteSpace(team))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var separatorPending = false;
        foreach (var character in team.Trim().Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0)
                {
                    _ = builder.Append('-');
                }

                _ = builder.Append(char.ToLowerInvariant(character));
                separatorPending = false;
            }
            else
            {
                separatorPending = true;
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
