using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class MemoizingQueryFingerprinterTests
{
    [Fact]
    public void The_same_query_is_hashed_once_per_kind()
    {
        var counting = new CountingFingerprinter();
        var fingerprinter = new MemoizingQueryFingerprinter(counting);
        var query = Query("SELECT 1");

        fingerprinter.StructuralFingerprint(query);
        fingerprinter.StructuralFingerprint(query);
        fingerprinter.ExactFingerprint(query);
        fingerprinter.ExactFingerprint(query);

        counting.StructuralCalls.Should().Be(1);
        counting.ExactCalls.Should().Be(1);
    }

    [Fact]
    public void Two_executions_with_equal_values_are_hashed_separately()
    {
        // RecordedQuery is a record, so value equality would fold two separate executions of the
        // same SQL into one entry — and telling them apart is the whole basis of duplicate
        // detection. The cache keys on reference identity for exactly this reason.
        var counting = new CountingFingerprinter();
        var fingerprinter = new MemoizingQueryFingerprinter(counting);

        // Same parameter instance as well, so the two records really are equal by value.
        var parameters = new Dictionary<string, object?> { ["@id"] = 1 };
        var first = Query("SELECT 1", parameters);
        var second = Query("SELECT 1", parameters);
        first.Should().Be(second, "the two are equal by value");

        fingerprinter.ExactFingerprint(first);
        fingerprinter.ExactFingerprint(second);

        counting.ExactCalls.Should().Be(2);
    }

    [Fact]
    public void The_cached_value_is_the_one_the_inner_fingerprinter_returned()
    {
        var inner = new DefaultQueryAnalysisFactory()
            .CreateFingerprinter(SqlNormalizationMode.WhitespaceOnly);
        var fingerprinter = new MemoizingQueryFingerprinter(inner);
        var query = Query("SELECT 1");

        fingerprinter.ExactFingerprint(query).Should().Be(inner.ExactFingerprint(query));
        fingerprinter.StructuralFingerprint(query).Should().Be(inner.StructuralFingerprint(query));
    }

    private static RecordedQuery Query(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        return new RecordedQuery
        {
            CommandText = sql,
            Parameters = parameters ?? new Dictionary<string, object?>(),
            Duration = TimeSpan.FromMilliseconds(1),
            Timestamp = DateTimeOffset.UnixEpoch
        };
    }

    private sealed class CountingFingerprinter : IQueryFingerprinter
    {
        public int StructuralCalls { get; private set; }

        public int ExactCalls { get; private set; }

        public string StructuralFingerprint(RecordedQuery query)
        {
            StructuralCalls++;
            return $"structural:{query.CommandText}";
        }

        public string ExactFingerprint(RecordedQuery query)
        {
            ExactCalls++;
            return $"exact:{query.CommandText}";
        }
    }
}
