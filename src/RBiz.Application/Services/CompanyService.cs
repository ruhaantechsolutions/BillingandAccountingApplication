using Microsoft.Extensions.Logging;
using RBiz.Application.Dtos;
using RBiz.Application.Interfaces;
using RBiz.Application.Mapping;
using RBiz.Domain.Entities;
using RBiz.Domain.Exceptions;
using RBiz.Domain.Utilities;

namespace RBiz.Application.Services;

/// <inheritdoc cref="ICompanyService"/>
public sealed class CompanyService : ICompanyService
{
    private const int MaxNameLength = 200;
    private const int MaxAddressLineLength = 200;
    private const int MaxCityLength = 100;

    /// <summary>
    /// A valid Indian PIN code: six digits, never starting with zero. Matches the rule the
    /// registration screen applies, so the client and the server agree.
    /// </summary>
    private static bool IsValidPinCode(string pin) =>
        pin.Length == 6 && pin[0] is >= '1' and <= '9' && pin.All(char.IsAsciiDigit);

    private readonly ICompanyRepository _companies;
    private readonly IGstinVerificationService _gstinVerification;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompanyService> _logger;

    public CompanyService(
        ICompanyRepository companies,
        IGstinVerificationService gstinVerification,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<CompanyService> logger)
    {
        _companies = companies;
        _gstinVerification = gstinVerification;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CompanySummaryDto>> GetMyCompaniesAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();

        return await _companies.GetForUserAsync(userId, cancellationToken);
    }

    public async Task<CompanyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();

        var company = await _companies.GetForUserAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), id);

        var role = company.Members.FirstOrDefault(m => m.UserId == userId)?.Role ?? CompanyRole.Member;

        return company.ToDto(role);
    }

    public async Task<CompanyDto> CreateAsync(
        CreateCompanyDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();

        if (request.RequestId == Guid.Empty)
        {
            throw ValidationException.For(
                nameof(request.RequestId),
                "requestId is required so a retried submission does not create a second company.");
        }

        // Answered before anything is validated or written: a retry of a request that
        // already succeeded must return the original company, not a second one and not an
        // error about a duplicate name.
        var existing = await _companies.FindByRequestIdAsync(userId, request.RequestId, cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Company creation request {RequestId} replayed by user {UserId}; returning company {CompanyId}.",
                request.RequestId,
                userId,
                existing.Id);

            return existing.ToDto(CompanyRole.Owner);
        }

        var name = ValidateName(request);
        var address = await ResolveAddressAsync(request, userId, cancellationToken);
        var gstin = ValidateGst(request);

        await CheckDuplicatesAsync(request, userId, name, gstin, cancellationToken);

        var company = await BuildCompanyAsync(request, userId, name, address, gstin, cancellationToken);

        _companies.Add(company);

        // One save: company, address, GST registration, ownership and primary location
        // commit together or not at all, so a failure cannot leave a half-created company.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Company {CompanyId} created by user {UserId}. GST status {GstStatus}, address {AddressSource}.",
            company.Id,
            userId,
            company.GstStatus,
            request.AddressSource);

        return company.ToDto(CompanyRole.Owner);
    }

    /// <summary>Name: present, trimmed and within a sane length.</summary>
    private static string ValidateName(CreateCompanyDto request)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            throw ValidationException.For(nameof(request.Name), "Enter the company name.");
        }

        if (name.Length > MaxNameLength)
        {
            throw ValidationException.For(
                nameof(request.Name),
                $"Company name cannot be longer than {MaxNameLength} characters.");
        }

        return name;
    }

    /// <summary>
    /// Produces the address the new company will own, either from the caller's fields or
    /// copied from a company they already have.
    /// </summary>
    private async Task<CompanyAddress> ResolveAddressAsync(
        CreateCompanyDto request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (request.AddressSource is AddressSource.CopyFromCompany)
        {
            if (request.CopyAddressFromCompanyId is not { } sourceId || sourceId == Guid.Empty)
            {
                throw ValidationException.For(
                    nameof(request.CopyAddressFromCompanyId),
                    "Select the company whose address you want to reuse.");
            }

            // Re-checked against the caller's own companies. The client rendered that
            // dropdown, so its contents are a suggestion, not an authorisation.
            var source = await _companies.GetForUserAsync(sourceId, userId, cancellationToken);

            if (source?.Address is null)
            {
                throw ValidationException.For(
                    nameof(request.CopyAddressFromCompanyId),
                    "That company is not available to copy an address from.");
            }

            // Values are copied, never shared. Editing the source company's address later
            // must not change this one.
            return new CompanyAddress
            {
                Line1 = source.Address.Line1,
                Line2 = source.Address.Line2,
                City = source.Address.City,
                State = source.Address.State,
                PinCode = source.Address.PinCode,
                Country = source.Address.Country,

                // Audit metadata only: where these values came from, with no live link.
                CopiedFromCompanyId = sourceId
            };
        }

        if (request.Address is not { } supplied)
        {
            throw ValidationException.For(nameof(request.Address), "Enter the company address.");
        }

        var errors = new Dictionary<string, string[]>();

        var line1 = supplied.Line1?.Trim() ?? string.Empty;
        var line2 = supplied.Line2?.Trim();
        var city = supplied.City?.Trim() ?? string.Empty;
        var pin = supplied.PinCode?.Trim() ?? string.Empty;

        if (line1.Length == 0)
        {
            errors["Address.Line1"] = ["Enter the address."];
        }
        else if (line1.Length > MaxAddressLineLength)
        {
            errors["Address.Line1"] = [$"Address line 1 cannot be longer than {MaxAddressLineLength} characters."];
        }

        if (line2 is { Length: > MaxAddressLineLength })
        {
            errors["Address.Line2"] = [$"Address line 2 cannot be longer than {MaxAddressLineLength} characters."];
        }

        if (city.Length == 0)
        {
            errors["Address.City"] = ["Enter the city."];
        }
        else if (city.Length > MaxCityLength)
        {
            errors["Address.City"] = [$"City cannot be longer than {MaxCityLength} characters."];
        }

        // Stored in one canonical spelling so later grouping and reporting do not split a
        // state across capitalisations.
        var state = IndianStates.Canonicalise(supplied.State);

        if (state is null)
        {
            errors["Address.State"] = ["Select the state."];
        }

        if (!IsValidPinCode(pin))
        {
            errors["Address.PinCode"] = ["Enter a valid 6-digit PIN code."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return new CompanyAddress
        {
            Line1 = line1,
            Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2,
            City = city,
            State = state!,
            PinCode = pin,

            // Fixed for now. Taken from the server rather than the request so a client
            // cannot create a company in a country the product does not yet support.
            Country = "India"
        };
    }

    /// <summary>
    /// Enforces the Yes/No branch: a GSTIN when registered, and none when not.
    /// </summary>
    private static string? ValidateGst(CreateCompanyDto request)
    {
        if (request.IsGstRegistered is not { } isRegistered)
        {
            throw ValidationException.For(nameof(request.IsGstRegistered), "Choose Yes or No.");
        }

        var gstin = GstinValidator.Normalise(request.Gstin);

        if (!isRegistered)
        {
            // Rejected rather than ignored. Accepting a GSTIN while recording the company
            // as unregistered would leave the caller believing it was saved.
            if (gstin.Length > 0)
            {
                throw ValidationException.For(
                    nameof(request.Gstin),
                    "Remove the GSTIN, or set the company as GST registered.");
            }

            return null;
        }

        return GstinValidator.Validate(gstin) switch
        {
            GstinValidationResult.Valid => gstin,

            GstinValidationResult.Empty => throw ValidationException.For(
                nameof(request.Gstin),
                "GSTIN is required for a GST registered company."),

            GstinValidationResult.UnknownStateCode => throw ValidationException.For(
                nameof(request.Gstin),
                "The first two digits of the GSTIN are not a valid state code."),

            // Worth its own message: the number is the right shape, so the user has almost
            // certainly mistyped one character rather than invented the whole thing.
            GstinValidationResult.ChecksumInvalid => throw ValidationException.For(
                nameof(request.Gstin),
                "This GSTIN failed its check digit, so one of the characters is wrong. Please re-check it."),

            _ => throw ValidationException.For(
                nameof(request.Gstin),
                "GSTIN format is invalid. It has 15 characters, for example 24ABCDE1234F1Z6.")
        };
    }

    /// <summary>
    /// Stops once on a duplicate name or GSTIN. Both are legitimate situations, so the
    /// caller is asked to confirm rather than refused - but the confirmation is checked on
    /// the server, not assumed because the client showed a checkbox.
    /// </summary>
    private async Task CheckDuplicatesAsync(
        CreateCompanyDto request,
        Guid userId,
        string name,
        string? gstin,
        CancellationToken cancellationToken)
    {
        if (!request.ConfirmDuplicateName &&
            await _companies.NameExistsForUserAsync(userId, name, cancellationToken))
        {
            throw new ConflictException(
                $"A company named '{name}' already exists under your account. " +
                "If this is a different business, confirm and submit again.");
        }

        if (gstin is null || request.ConfirmDuplicateGstin)
        {
            return;
        }

        var owner = await _companies.FindGstinOwnerNameForUserAsync(userId, gstin, cancellationToken);

        if (owner is not null)
        {
            throw new ConflictException(
                $"This GSTIN is already used by '{owner}'. Two companies on one registration " +
                "can produce duplicate accounting books. Confirm and submit again to continue.");
        }
    }

    /// <summary>
    /// Assembles the company and everything created alongside it.
    /// </summary>
    private async Task<Company> BuildCompanyAsync(
        CreateCompanyDto request,
        Guid userId,
        string name,
        CompanyAddress address,
        string? gstin,
        CancellationToken cancellationToken)
    {
        var companyId = Guid.CreateVersion7();

        var company = new Company
        {
            Id = companyId,
            Name = name,
            IsActive = true,
            CreationRequestId = request.RequestId,
            GstStatus = gstin is null ? GstRegistrationStatus.NotRegistered : GstRegistrationStatus.Registered,
            Address = address
        };

        address.Id = Guid.CreateVersion7();
        address.CompanyId = companyId;

        if (gstin is not null)
        {
            // Never blocks creation. An unreachable register leaves the registration marked
            // unverified, which is a different thing from invalid and is recorded as such.
            var status = await _gstinVerification.VerifyAsync(gstin, cancellationToken);

            company.GstRegistration = new CompanyGstRegistration
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId,
                Gstin = gstin,
                RegistrationType = request.GstRegistrationType ?? GstRegistrationType.Regular,
                VerificationStatus = status,
                VerifiedAtUtc = status is GstinVerificationStatus.Verified
                    ? _timeProvider.GetUtcNow().UtcDateTime
                    : null
            };
        }

        // The creator owns what they create.
        company.Members.Add(new UserCompany
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CompanyId = companyId,
            Role = CompanyRole.Owner,
            IsActive = true
        });

        // A business needs somewhere to bill from on day one, and the address has just been
        // collected - asking for it again as a "location" would be the same question twice.
        company.Locations.Add(new Location
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            Name = $"{address.City} (primary)",
            IsPrimary = true,
            IsActive = true
        });

        return company;
    }

    private Guid RequireUser() =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException(
            "A valid access token is required to manage companies.");
}
