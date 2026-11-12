using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nimbus.Application.Features.FlightPreparations.Commands.CreateFlightPreparation;
using Nimbus.Application.Features.FlightPreparations.Commands.DeleteFlightPreparation;
using Nimbus.Application.Features.FlightPreparations.Commands.UpdateFlightPreparation;
using Nimbus.Application.Features.FlightPreparations.Images.Delete;
using Nimbus.Application.Features.FlightPreparations.Images.Get;
using Nimbus.Application.Features.FlightPreparations.Images.List;
using Nimbus.Application.Features.FlightPreparations.Images.Reorder;
using Nimbus.Application.Features.FlightPreparations.Images.Upload;
using Nimbus.Application.Features.FlightPreparations.Queries.GetFlightDetail;
using Nimbus.Application.Features.FlightPreparations.Queries.GetFlightPreparation;
using Nimbus.Application.Features.FlightPreparations.Queries.ListFlights;
using Nimbus.Contracts.DTOs.Features.FlightPreparations;
using Nimbus.Domain.Enums;
namespace Nimbus.API.Controllers;

/// <summary>
///     The signed-in user's flight overview: own flights plus those shared with them (an admin sees all).
///     The user comes from the auth context only; filtering, sorting and paging run in SQL.
/// </summary>
[ApiController]
[Route("api/flight-preparations")]
[Authorize]
public class FlightPreparationsController : ControllerBase
{
    private readonly ISender _mediator;

    public FlightPreparationsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<FlightListPageDto>> List(
        [FromQuery] FlightListFilter filter = FlightListFilter.All,
        [FromQuery] FlightListSort sort = FlightListSort.Date,
        [FromQuery] bool descending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Ok(await _mediator.Send(new ListFlightsQuery(filter, sort, descending, page, pageSize), ct));
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FlightPreparationDto>> Get(Guid id, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetFlightPreparationQuery(id), ct));
    }

    /// <summary>Read-only view for the owner, an admin or anyone the flight is shared with; 403 for everyone else, 404 when it does not exist.</summary>
    [HttpGet("{id:guid}/detail")]
    public async Task<ActionResult<FlightDetailDto>> Detail(Guid id, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetFlightDetailQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<FlightPreparationDto>> Create(FlightPreparationRequestDto request, CancellationToken ct)
    {
        var created = await _mediator.Send(new CreateFlightPreparationCommand(request), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FlightPreparationDto>> Update(Guid id, FlightPreparationRequestDto request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new UpdateFlightPreparationCommand(id, request), ct));
    }
    /// <summary>Permanently deletes the flight and everything that belongs to it. Owner or admin only; 403 for anyone else who can see it, 404 when it does not exist.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteFlightPreparationCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    ///     Attaches an image (multipart: <c>file</c> and <c>section</c>) to the flight. Owner or admin only. The type and size are
    ///     checked and the bytes must decode as a real image; 422 otherwise. The body ceiling here only stops a runaway upload, the
    ///     configured per-file limit is enforced by the handler.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<ActionResult<FlightImageDto>> UploadImage(Guid id, [FromForm] UploadFlightImageForm form, CancellationToken ct)
    {
        if (form.File is null)
            return UnprocessableEntity(new { title = "Validation failed", status = 422, errors = new Dictionary<string, string[]> { ["file"] = ["A file is required."] } });

        await using var content = form.File.OpenReadStream();
        var created = await _mediator.Send(new UploadFlightImageCommand(id, form.Section, content, form.File.ContentType), ct);
        return Created($"api/flight-preparations/{id}/images/{created.Id}", created);
    }

    /// <summary>The flight's images with short-lived links, for the editor. Anyone who may see the flight may list them.</summary>
    [HttpGet("{id:guid}/images")]
    public async Task<ActionResult<IReadOnlyList<FlightImageDto>>> ListImages(Guid id, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new ListFlightImagesQuery(id), ct));
    }

    /// <summary>Sets the order of one section's images: the body names every image of that section, first to last. Owner or admin only.</summary>
    [HttpPut("{id:guid}/images/order")]
    public async Task<ActionResult> ReorderImages(Guid id, ReorderFlightImagesRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<FlightPreparationSection>(request.Section, ignoreCase: true, out var section) || !Enum.IsDefined(section))
            return UnprocessableEntity(new { title = "Validation failed", status = 422, errors = new Dictionary<string, string[]> { ["section"] = ["Unknown section."] } });

        await _mediator.Send(new ReorderFlightImagesCommand(id, section, request.ImageIds), ct);
        return NoContent();
    }

    /// <summary>The image's bytes (or its thumbnail with <c>?thumbnail=true</c>), checked against the viewer's access to the flight on every request.</summary>
    [HttpGet("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> GetImage(Guid id, Guid imageId, [FromQuery] bool thumbnail = false, CancellationToken ct = default)
    {
        var download = await _mediator.Send(new GetFlightImageQuery(id, imageId, thumbnail), ct);
        Response.Headers.CacheControl = "private, max-age=300";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(download.Content, download.ContentType);
    }

    /// <summary>Removes the image's metadata row and its files. Owner or admin only. A storage failure is a 502 and leaves the image in place to retry.</summary>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<ActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteFlightImageCommand(id, imageId), ct);
        return NoContent();
    }
}

/// <summary>Multipart form of an image upload.</summary>
public sealed class UploadFlightImageForm
{
    public IFormFile? File { get; init; }
    public FlightPreparationSection Section { get; init; }
}
