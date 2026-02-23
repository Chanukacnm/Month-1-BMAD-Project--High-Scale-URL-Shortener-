using MediatR;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application.Urls.Commands;
using UrlShortener.Application.Urls.Queries;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UrlsController : ControllerBase
{
    private readonly IMediator _mediator;

    public UrlsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUrlRequest request)
    {
        if (string.IsNullOrEmpty(request.OriginalUrl))
        {
            return BadRequest("OriginalUrl is required.");
        }

        var command = new CreateShortUrlCommand(request.OriginalUrl);
        var shortCode = await _mediator.Send(command);

        return Ok(new { shortCode });
    }

    [HttpGet("/{code}")]
    public async Task<IActionResult> RedirectToOriginal(string code)
    {
        var query = new GetOriginalUrlQuery(code);
        var originalUrl = await _mediator.Send(query);

        if (originalUrl == null)
        {
            return NotFound();
        }

        return Redirect(originalUrl);
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        var command = new DeleteShortUrlCommand(code);
        var deleted = await _mediator.Send(command);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}

public record CreateUrlRequest(string OriginalUrl);
