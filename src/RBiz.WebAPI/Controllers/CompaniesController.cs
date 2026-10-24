using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;

namespace RBiz.WebAPI.Controllers;

/// <summary>
/// The businesses the signed-in person owns or works on.
/// </summary>
[ApiVersion("1.0")]
public sealed class CompaniesController : ApiControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService) => _companyService = companyService;

    /// <summary>
    /// Every company the caller can reach.
    /// </summary>
    /// <remarks>
    /// Feeds both the company switcher and the "existing companies" panel on the
    /// registration screen, including the address-reuse dropdown. Not paged: a person has
    /// a handful of companies, and paging a switcher would be a worse experience than one
    /// small response.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<CompanySummaryDto>))]
    public async Task<ActionResult<IReadOnlyList<CompanySummaryDto>>> GetMine(
        CancellationToken cancellationToken)
    {
        var companies = await _companyService.GetMyCompaniesAsync(cancellationToken);

        return Ok(companies);
    }

    /// <summary>
    /// Returns one company the caller can reach.
    /// </summary>
    /// <response code="404">No such company, or the caller is not a member of it.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CompanyDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<CompanyDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var company = await _companyService.GetByIdAsync(id, cancellationToken);

        return Ok(company);
    }

    /// <summary>
    /// Registers a new company for the signed-in person.
    /// </summary>
    /// <remarks>
    /// The company, its address, its GST registration where applicable, the caller's
    /// ownership of it and its primary location are created in one transaction, so a
    /// failure never leaves a company that is half set up.
    ///
    /// Send a fresh <c>requestId</c> per attempt and repeat it on retry: a double-clicked
    /// button or a retried call returns the company the first attempt created rather than
    /// making a second set of books.
    ///
    /// Two situations are legitimate but easy to get wrong, so the server stops once with
    /// a 409 and asks for confirmation: a company name already used in this account, and a
    /// GSTIN already used by another of the caller's companies. Resubmit the same request
    /// with <c>confirmDuplicateName</c> or <c>confirmDuplicateGstin</c> set to continue.
    /// </remarks>
    /// <response code="201">Created; the Location header points at the new company.</response>
    /// <response code="400">A field is missing or malformed; see the errors object.</response>
    /// <response code="409">A duplicate name or GSTIN needs confirming first.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CompanyDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<CompanyDto>> Create(
        [FromBody] CreateCompanyDto request,
        CancellationToken cancellationToken)
    {
        var company = await _companyService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = company.Id }, company);
    }
}
