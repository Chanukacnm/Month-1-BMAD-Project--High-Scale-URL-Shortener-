# Story 1.4: Redirection Logic

Status: review

## Story

As a user,
I want to be redirected to the original destination when I use a short link,
so that I can reach the intended content quickly.

## Acceptance Criteria

1. API endpoint `GET /{code}` (or a dedicated redirect route) retrieves the original URL.
2. The system returns a 302 (Found) redirect to the original destination.
3. If the short code does not exist, the system returns a 404 (Not Found).
4. Redirection is performant and uses the persistence layer.

## Tasks / Subtasks

- [x] Implement Redirection Query in Application Layer. (AC: 1)
  - [x] Create `GetOriginalUrlQuery` and Handler.
- [x] Implement Redirection Endpoint in Api Layer. (AC: 2, 3)
  - [x] Add `GET /{code}` route to `UrlsController`.
  - [x] Perform `Redirect()` using the result from the query.
- [x] Handle Missing Codes. (AC: 3)
  - [x] Ensure 404 is returned when code is null or not found.

## Dev Notes

- Use `IApplicationDbContext` to query the `ShortUrls` table.
- Indexing on `ShortCode` was already established in Story 1.2, ensuring performance.
- Consider caching in Epic 4, but for now, it's a direct DB lookup.

### Project Structure Notes

- Query: `UrlShortener.Application/Urls/Queries/GetOriginalUrlQuery.cs`
- Controller Update: `UrlShortener.Api/Controllers/UrlsController.cs`

### References

- [Source: architecture.md#L45-L50]
- [Source: epics.md#L104-L115]

## Dev Agent Record

### Agent Model Used

Antigravity v1.0

### Debug Log References

### Completion Notes List

- Implemented `GetOriginalUrlQuery` for optimized DB retrieval (AsNoTracking).
- Added `GET /{code}` endpoint to `UrlsController` for 302 redirects.
- Correctly handled 404 Not Found for invalid short codes.
- Verified build is passing with redirection logic.

### File List

- `src/UrlShortener.Application/Urls/Queries/GetOriginalUrlQuery.cs`
- `src/UrlShortener.Api/Controllers/UrlsController.cs`
