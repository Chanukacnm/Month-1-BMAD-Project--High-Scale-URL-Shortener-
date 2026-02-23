# Story 1.5: URL Management API (Delete)

Status: review

## Story

As a user,
I want to be able to delete my short URLs,
so that I can manage my links and remove unwanted mappings.

## Acceptance Criteria

1. API endpoint `DELETE /api/urls/{code}` is available.
2. The endpoint removes the mapping for the given short code from the database.
3. Returns 204 No Content on success.
4. Returns 404 Not Found if the short code does not exist.

## Tasks / Subtasks

- [x] Implement Delete Logic in Application Layer. (AC: 2)
  - [x] Create `DeleteShortUrlCommand` and Handler.
- [x] Implement Delete Endpoint in Api Layer. (AC: 1, 3, 4)
  - [x] Add `DELETE /api/urls/{code}` route to `UrlsController`.
  - [x] Return appropriate status codes (204 or 404).

## Dev Notes

- Use `IApplicationDbContext` to find and remove the entity.
- Ensure the entity tracking is handled correctly during removal.

### Project Structure Notes

- Command: `UrlShortener.Application/Urls/Commands/DeleteShortUrlCommand.cs`
- Controller Update: `UrlShortener.Api/Controllers/UrlsController.cs`

### References

- [Source: epics.md#L116-L127]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Implemented `DeleteShortUrlCommand` and its handler to remove URL mappings.
- Added `DELETE /api/urls/{code}` endpoint to `UrlsController`.
- Handled removal of entities using `IApplicationDbContext`.
- Confirmed build succeeds with the new functionality.

### File List

- `src/UrlShortener.Application/Urls/Commands/DeleteShortUrlCommand.cs`
- `src/UrlShortener.Api/Controllers/UrlsController.cs`
- `src/UrlShortener.Infrastructure/Persistence/ApplicationDbContext.cs`
