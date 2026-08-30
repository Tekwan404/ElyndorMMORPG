namespace Elyndor.IntegrationTests.Postgres;

[CollectionDefinition(Name)]
public sealed class PostgresFixtureDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
