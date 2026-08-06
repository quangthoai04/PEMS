// The real tests for GET /api/public/search live in the projects that actually build and run:
//
//   tests/PEMS.UnitTests/PublicContent/SearchInformationQueryTests.cs
//       — visibility, VI/EN strictness, relevance ranking, limit + hasMore, deep-link payload
//         (EF InMemory, via PublicSearchTestDbContext).
//   tests/PEMS.UnitTests/PublicContent/GetPublicFaqDetailQueryTests.cs
//       — GET /api/public/faqs/{faqId}, the /faq?faqId= deep-link endpoint.
//   tests/PEMS.IntegrationTests/PublicContent/PublicSearchSqlTranslationTests.cs
//       — proof the section queries translate to MySQL SQL (filters/ranking/limit pushed down).
//
// This directory is NOT one of those projects: tests/PEMS.ApplicationTests has no .csproj and is not
// listed in PEMS.slnx, so nothing here is ever compiled or executed. The file previously in its place
// held a [Fact(Skip = "Pending UC specification")] stub, which read as "coverage exists, temporarily
// disabled" when in fact no such test could run at all. It is left as this pointer rather than a stub
// so the next reader is sent to the tests that do run.
