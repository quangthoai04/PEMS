# Clean Architecture

## Dependency Rule
`PEMS.Domain` has no dependencies.
`PEMS.Application` depends only on `PEMS.Domain`.
`PEMS.Infrastructure` depends on `PEMS.Application` and `PEMS.Domain`.
`PEMS.Api` depends on `PEMS.Application`.
