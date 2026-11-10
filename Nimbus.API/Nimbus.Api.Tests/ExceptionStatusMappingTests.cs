using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.API.Middleware;
using Nimbus.Application.Common.Exceptions;
namespace Nimbus.Api.Tests;

/// <summary>"Exists but not yours" (403) and "does not exist" (404) are different answers: the flight view relies on telling them apart.</summary>
public class ExceptionStatusMappingTests
{
    private static async Task<int> StatusFor(Exception thrown)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw thrown, NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(ctx);
        return ctx.Response.StatusCode;
    }

    [Test]
    public async Task ForbiddenException_Answers403()
    {
        Assert.That(await StatusFor(new ForbiddenException("FlightPreparation", Guid.NewGuid())), Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task ProviderUnavailableException_Answers502()
    {
        Assert.That(await StatusFor(new ProviderUnavailableException("The aviation weather service")), Is.EqualTo(StatusCodes.Status502BadGateway));
    }

    [Test]
    public async Task NotFoundException_StillAnswers404()
    {
        Assert.That(await StatusFor(new NotFoundException("FlightPreparation", Guid.NewGuid())), Is.EqualTo(StatusCodes.Status404NotFound));
    }
}
