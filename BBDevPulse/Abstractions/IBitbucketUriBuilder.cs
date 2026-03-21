namespace BBDevPulse.Abstractions;

/// <summary>
/// Builds Bitbucket API URIs with optional partial response field groups.
/// </summary>
internal interface IBitbucketUriBuilder
{
    /// <summary>
    /// Builds a relative Bitbucket URI for the provided path and field group.
    /// </summary>
    /// <param name="path">Relative Bitbucket API path.</param>
    /// <param name="fieldGroup">Optional partial response field group.</param>
    /// <returns>Relative URI with the requested field selection.</returns>
    Uri BuildRelativeUri(string path, BitbucketFieldGroup fieldGroup = BitbucketFieldGroup.None);
}
