namespace RBiz.Domain.Common;

/// <summary>
/// Marks an entity that must never be written to the audit trail.
///
/// The audit interceptor records the changed columns of every tracked entity as JSON.
/// For a table that stores credentials that would copy the secret into a table which is
/// append-only, widely readable and never pruned - so the credential would outlive the
/// session it belonged to.
///
/// It also keeps the trail useful: a refresh token rotates on every reconnect, and
/// thousands of rows recording that would bury the business events an auditor is
/// actually looking for.
/// </summary>
public interface INonAuditable
{
}
