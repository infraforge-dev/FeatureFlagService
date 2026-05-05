using System.Net;
using Banderas.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Banderas.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class ArchivedFlag409MiddlewareTests : IntegrationTestBase
{
    public ArchivedFlag409MiddlewareTests(BanderasApiFactory factory)
        : base(factory) { }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FlagDomainException_Returns409ConflictProblemDetailsAsync()
    {
        // Arrange — test-only endpoint registered in BanderasApiFactory
        //   throws FlagDomainException("Test flag 'test-flag' is archived and cannot be modified.")

        // Act
        HttpResponseMessage response = await Client.GetAsync("/test/throw-domain-exception");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.Conflict);
        body.Title.Should().Be("Conflict");
        body.Detail.Should().Contain("test-flag");
        body.Detail.Should().Contain("archived and cannot be modified");
    }
}
