using Microsoft.Extensions.Options;

namespace BBDevPulse.Configuration;

/// <summary>
/// Validates <see cref="BitbucketOptions"/>.
/// </summary>
public sealed class BitbucketOptionsValidator : IValidateOptions<BitbucketOptions>
{
    /// <summary>
    /// Validates the provided options instance.
    /// </summary>
    /// <param name="name">Options name.</param>
    /// <param name="options">Options instance.</param>
    /// <returns>Validation result.</returns>
    public ValidateOptionsResult Validate(string? name, BitbucketOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();

        if (options.Days <= 0)
        {
            errors.Add("Bitbucket:Days must be greater than 0.");
        }

        if (options.PageLength <= 0)
        {
            errors.Add("Bitbucket:PageLength must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(options.Workspace))
        {
            errors.Add("Bitbucket:Workspace is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            errors.Add("Bitbucket:Username is required.");
        }

        if (string.IsNullOrWhiteSpace(options.AppPassword))
        {
            errors.Add("Bitbucket:AppPassword is required.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
