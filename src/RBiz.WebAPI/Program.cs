using Microsoft.AspNetCore.Mvc;
using RBiz.Application;
using RBiz.Application.Sync;
using RBiz.Infrastructure;
using RBiz.WebAPI.Errors;
using RBiz.WebAPI.Extensions;
using Serilog;

// Logs anything that fails before the host is even built.
SerilogExtensions.AddBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSerilogLogging();

    // The two chained calls below are the only place the layers are composed. Each layer
    // registers what it owns and nothing else, so Program.cs never names a concrete
    // repository, DbContext or token generator.
    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddJwtAuthentication(builder.Configuration);

    // --- PWA / offline sync -------------------------------------------------------
    // Bound here because the host is the only layer that knows where configuration
    // comes from; SyncOptions itself lives in the Application layer.
    builder.Services.AddOptions<SyncOptions>()
        .Bind(builder.Configuration.GetSection(SyncOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddPwaCors(builder.Configuration);
    // ------------------------------------------------------------------------------

    builder.Services
        .AddApiVersioningConfiguration()
        .AddSwaggerConfiguration();

    builder.Services.AddControllers();

    // Turns a failed [ApiController] model binding into the same ProblemDetails shape the
    // global handler produces, so a client only ever has one error format to parse.
    builder.Services.Configure<ApiBehaviorOptions>(options =>
        options.SuppressModelStateInvalidFilter = false);

    // AddProblemDetails supplies the writer the handler below uses.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    var app = builder.Build();

    // First in the pipeline: anything thrown further down lands here.
    app.UseExceptionHandler();

    // Turns a bare 404 or 405 produced by the framework itself into a ProblemDetails body
    // rather than an empty response.
    app.UseStatusCodePages();

    app.UseRequestLogging();

    // Before authentication: a rejected preflight never carries credentials, so it must
    // be answered even for an endpoint that requires a token.
    app.UseCors(CorsExtensions.PolicyName);

    if (app.Environment.IsDevelopment())
    {
        // Documentation is exposed outside Development only deliberately: it describes
        // every endpoint and payload, which is not something to publish by default.
        app.UseSwaggerUserInterface();
    }
    else
    {
        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Applies migrations and seeds the first tenant when configured to do so.
    await app.InitialiseDatabaseAsync();

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "R-Biz API terminated unexpectedly during startup.");
    throw;
}
finally
{
    // Flushes anything still buffered in a file sink before the process exits.
    await Log.CloseAndFlushAsync();
}
