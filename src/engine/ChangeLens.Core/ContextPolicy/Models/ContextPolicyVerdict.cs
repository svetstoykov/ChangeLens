namespace ChangeLens.Core.ContextPolicy.Models;

/// <summary>
///     Defines the disclosure verdict for one evidence node.
/// </summary>
public enum ContextPolicyVerdict
{
    /// <summary>
    ///     The node is disclosed unchanged.
    /// </summary>
    Allow,

    /// <summary>
    ///     The node is disclosed with secret-bearing lines replaced.
    /// </summary>
    Redact,

    /// <summary>
    ///     The node is not disclosed.
    /// </summary>
    Exclude,
}
