using ErickMorales.EntityFrameworkCore.QueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.UnitTests;

public class QueryFingerprinterTests
{
    private readonly DefaultQueryFingerprinter _fingerprinter = new();

    [Fact]
    public void Structural_fingerprint_ignores_parameter_values()
    {
        var a = Query("SELECT * FROM users WHERE id = @id", ("@id", 1));
        var b = Query("SELECT * FROM users WHERE id = @id", ("@id", 2));

        _fingerprinter.StructuralFingerprint(a)
            .Should().Be(_fingerprinter.StructuralFingerprint(b));
    }

    [Fact]
    public void Exact_fingerprint_changes_with_parameters()
    {
        var a = Query("SELECT * FROM users WHERE id = @id", ("@id", 1));
        var b = Query("SELECT * FROM users WHERE id = @id", ("@id", 2));

        _fingerprinter.ExactFingerprint(a)
            .Should().NotBe(_fingerprinter.ExactFingerprint(b));
    }

    [Fact]
    public void Exact_fingerprint_matches_identical_parameters()
    {
        var a = Query("SELECT * FROM users WHERE id = @id", ("@id", 10));
        var b = Query("SELECT * FROM users WHERE id = @id", ("@id", 10));

        _fingerprinter.ExactFingerprint(a)
            .Should().Be(_fingerprinter.ExactFingerprint(b));
    }

    [Fact]
    public void Byte_arrays_are_hashed_not_embedded()
    {
        var query = Query("UPDATE blobs SET data = @data", ("@data", new byte[] { 1, 2, 3 }));
        var fingerprint = _fingerprinter.ExactFingerprint(query);
        fingerprint.Should().NotContain("AQID");
        fingerprint.Should().HaveLength(64);
    }

    private static RecordedQuery Query(
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        return new RecordedQuery
        {
            CommandText = sql,
            Parameters = parameters.ToDictionary(p => p.Name, p => p.Value),
            Duration = TimeSpan.FromMilliseconds(1),
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
