# API Contract Review Checklist

Use this checklist when reviewing changes to REST API endpoints, DTOs, or response shapes.

## Endpoint Changes

- [ ] All changed endpoints identified (route, method, parameters)
- [ ] New endpoints have appropriate HTTP methods
- [ ] Removed endpoints are not referenced by active consumers
- [ ] Route changes are classified as breaking

## Request Contract

- [ ] New required fields have no existing consumers that omit them
- [ ] Removed fields are not sent by active consumers
- [ ] Type changes are compatible (e.g., int to long is safe, long to int is not)
- [ ] Validation rule changes are classified (tightened = breaking, loosened = non-breaking)
- [ ] Default value changes are documented

## Response Contract

- [ ] Removed fields are not consumed by any client
- [ ] New fields do not break deserialisers with strict schemas
- [ ] Type changes are compatible
- [ ] Null behaviour changes are documented (previously non-null now nullable, or vice versa)
- [ ] Pagination contract unchanged or versioned

## Status Codes

- [ ] No changed meaning for existing status codes
- [ ] New error responses follow existing error format (e.g., ProblemDetails/RFC 7807)
- [ ] Removed status codes are not expected by consumers

## Versioning

- [ ] Breaking changes go into a new API version
- [ ] Non-breaking changes do not require a version bump
- [ ] Version routing is configured correctly

## OpenAPI / Swagger

- [ ] Spec file updated or auto-generation triggered
- [ ] Spec accurately reflects the changes
- [ ] Generated client SDKs can be rebuilt without errors

## Consumer Impact

- [ ] Affected consumers identified (frontend, mobile, third-party)
- [ ] Migration path documented for breaking changes
- [ ] Deprecation notices added for phased removal

## Tests

- [ ] Integration tests updated for changed contracts
- [ ] New endpoints have integration tests
- [ ] Breaking change scenarios are tested
