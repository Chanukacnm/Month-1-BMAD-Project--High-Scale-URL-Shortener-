# Story 2.1: Click Tracking Decorator

Status: review

## Story

As a developer,
I want to capture click events automatically when a redirect occurs,
so that I can maintain centralized tracking logic across all redirection endpoints.

## Acceptance Criteria

1. An `AnalyticsFilter` or Middleware intercepts the redirection call.
2. Basic request metadata (IP, User Agent, Referer) is extracted.
3. The event is prepared for processing by the analytics subsystem.

## Tasks / Subtasks

- [x] Implement Analytics Extract Logic. (AC: 1, 2)
  - [x] Create `ClickMetadata` model in `UrlShortener.Application`.
  - [x] Implement `IAnalyticsService` interface.
- [x] Create Middleware/Filter for Interception. (AC: 1, 2)
  - [x] Implement `AnalyticsMiddleware` in the Api layer.
  - [x] Capture IP, UA, and Referer from the `HttpContext`.
- [x] Integration with Redirection. (AC: 3)
  - [x] Ensure the middleware only triggers for the redirect route.

## Dev Notes

- Use `HttpContext.Connection.RemoteIpAddress` for IP.
- Use `Request.Headers["User-Agent"]` and `Request.Headers["Referer"]`.
- The middleware should check if the route corresponds to a redirect (e.g., matches the pattern `/{code}`).

### Project Structure Notes

- Metadata Model: `UrlShortener.Application/Common/Models/ClickMetadata.cs`
- Service Interface: `UrlShortener.Application/Common/Interfaces/IAnalyticsService.cs`
- Middleware: `UrlShortener.Api/Middleware/AnalyticsMiddleware.cs`

### References

- [Source: epics.md#L132-L143]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Created `ClickMetadata` record to hold IP, User Agent, and Referer.
- Implemented `AnalyticsMiddleware` to intercept GET requests bypassing `/api/`.
- Registered `AnalyticsService` as a scoped service.
- Verified build and middleware integration in `Program.cs`.

### File List

- `src/UrlShortener.Application/Common/Models/ClickMetadata.cs`
- `src/UrlShortener.Application/Common/Interfaces/IAnalyticsService.cs`
- `src/UrlShortener.Api/Middleware/AnalyticsMiddleware.cs`
- `src/UrlShortener.Infrastructure/Services/AnalyticsService.cs`
- `src/UrlShortener.Api/Program.cs`
