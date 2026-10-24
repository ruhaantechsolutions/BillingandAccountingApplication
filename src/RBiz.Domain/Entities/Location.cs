using RBiz.Domain.Sync;

namespace RBiz.Domain.Entities;

/// <summary>
/// A place a company operates from: head office, shop, branch or factory.
///
/// Created from the company's address at registration so the business has somewhere to
/// bill from on day one, without asking for the same address twice. Warehouses are a
/// separate concept and are added by the inventory module, because one location may hold
/// several of them and an office may hold none.
/// </summary>
public class Location : SyncableEntity
{
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Exactly one location per company carries this. It is the default place of business
    /// for billing until the user configures others.
    /// </summary>
    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public Company? Company { get; set; }
}
