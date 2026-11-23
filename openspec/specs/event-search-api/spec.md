# event-search-api Specification

## Purpose

Provides a high-performance HTTP read-only search API for event discovery, full-text multi-match queries, geo-distance filtering, faceted aggregations, and autocomplete suggestions over ElasticSearch.

## Requirements

### Requirement: Full-Text Event Search with Multi-Field Boosting
The system SHALL accept search queries via `GET /api/v1/search/events` and execute full-text multi-match queries over ElasticSearch with relevance boosting (`title^3`, `category^2`, `tags^1.5`, `description`).

#### Scenario: Full-text query matches event title and tags
- **WHEN** a client issues `GET /api/v1/search/events?q=rock`
- **THEN** the system returns matching published events sorted by relevance score, including title, category, schedule, venue, pricing range, and tags in the standardized response envelope.

#### Scenario: Empty query returns default page of published events
- **WHEN** a client issues `GET /api/v1/search/events` without a query string `q`
- **THEN** the system returns the first page of active events ordered by start date ascending.

### Requirement: Faceted Search Filtering by Category, Status, Price, and Dates
The system SHALL support filtering search results by category, status, date intervals (`from_date`, `to_date`), and price ranges (`min_price`, `max_price`), along with category and city aggregation facets.

#### Scenario: Filter events by category and price range
- **WHEN** a client issues `GET /api/v1/search/events?category=CONCERT&max_price=300`
- **THEN** the system filters documents matching category `CONCERT` with ticket tier pricing less than or equal to 300 and includes category/city aggregation counts in the response.

#### Scenario: Filter events by upcoming date window
- **WHEN** a client issues `GET /api/v1/search/events?from_date=2026-10-01T00:00:00Z&to_date=2026-10-31T23:59:59Z`
- **THEN** the system restricts results to events occurring within October 2026.

### Requirement: Geo-Distance Proximity Filtering and Sorting
The system SHALL filter and sort events by geographic proximity when latitude, longitude, and radius parameters are supplied.

#### Scenario: Search events within geographic radius
- **WHEN** a client issues `GET /api/v1/search/events?lat=-22.98&lon=-43.41&radius_km=25&sort_by=distance`
- **THEN** the system filters events within 25 kilometers of the provided coordinates and sorts results by ascending distance from the reference location.

### Requirement: Autocomplete Prefix Search Suggestions
The system SHALL provide low-latency prefix search suggestions via `GET /api/v1/search/events/autocomplete` utilizing Edge N-Gram analyzers.

#### Scenario: Autocomplete event title prefix
- **WHEN** a client issues `GET /api/v1/search/events/autocomplete?prefix=roc&limit=5`
- **THEN** the system returns up to 5 suggested event titles, categories, and identifiers matching the prefix `roc`.

#### Scenario: Autocomplete with empty or missing prefix
- **WHEN** a client issues `GET /api/v1/search/events/autocomplete` with an empty or whitespace prefix
- **THEN** the system returns an empty suggestions list without error.

### Requirement: Event Search Document Lookup by Identifier
The system SHALL retrieve a single event search document by identifier via `GET /api/v1/search/events/{id}`.

#### Scenario: Lookup existing event by ID
- **WHEN** a client requests an existing event identifier via `GET /api/v1/search/events/{id}`
- **THEN** the system returns HTTP 200 with the full event search document details.

#### Scenario: Lookup non-existent event by ID
- **WHEN** a client requests an identifier that does not exist in ElasticSearch
- **THEN** the system returns HTTP 404 with a standardized not-found error response.

### Requirement: Search Service Health Monitoring
The system SHALL expose `GET /api/v1/search/health` verifying operational readiness and cluster connectivity to ElasticSearch.

#### Scenario: ElasticSearch cluster is healthy
- **WHEN** a client queries `GET /api/v1/search/health` and ElasticSearch responds successfully
- **THEN** the system returns HTTP 200 with status `Healthy`.

#### Scenario: ElasticSearch cluster is unreachable
- **WHEN** ElasticSearch is unreachable or returns a cluster health error
- **THEN** the system returns HTTP 503 with status `Unhealthy`.
