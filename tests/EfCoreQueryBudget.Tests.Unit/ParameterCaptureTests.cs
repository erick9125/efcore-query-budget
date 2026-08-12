using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class ParameterCaptureTests
{
    [Fact]
    public void A_binary_payload_is_not_retained_by_reference()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var captured = ParameterCapture.Capture(payload);

        var before = Fingerprint(captured);
        payload[0] = 99;

        captured.Should().BeOfType<ParameterSnapshot>();
        Fingerprint(captured).Should().Be(before, "the payload was projected, not referenced");
    }

    [Fact]
    public void A_binary_payload_keeps_its_reported_shape()
    {
        var captured = ParameterCapture.Capture(new byte[1024]);

        Report(captured, QueryParameterDisplayMode.TypesOnly).Should().Contain("byte[1024]");
        Report(captured, QueryParameterDisplayMode.Full).Should().Contain("byte[1024]#redacted");
    }

    [Fact]
    public void Two_binary_payloads_of_the_same_length_are_told_apart()
    {
        var first = ParameterCapture.Capture(new byte[] { 1, 2, 3 });
        var second = ParameterCapture.Capture(new byte[] { 4, 5, 6 });

        Fingerprint(first).Should().NotBe(Fingerprint(second));
    }

    [Fact]
    public void A_long_string_is_truncated()
    {
        var captured = ParameterCapture.Capture(new string('a', 500));

        captured.Should().BeOfType<ParameterSnapshot>()
            .Which.Text!.Length.Should().BeLessThan(300);
    }

    [Fact]
    public void Two_long_strings_sharing_a_prefix_are_told_apart()
    {
        // Truncation alone would make these the same value and report them as one duplicated query.
        var first = ParameterCapture.Capture(new string('a', 500) + "first");
        var second = ParameterCapture.Capture(new string('a', 500) + "second");

        Fingerprint(first).Should().NotBe(Fingerprint(second));
    }

    [Fact]
    public void A_long_string_does_not_split_a_surrogate_pair()
    {
        // The leading "a" offsets the emoji so the cut would land between the halves of one.
        var value = "a" + string.Concat(Enumerable.Repeat("\U0001F600", 400));
        var captured = (ParameterSnapshot)ParameterCapture.Capture(value)!;

        var text = captured.Text!.Trim('"')[..^3];
        char.IsHighSurrogate(text[^1]).Should().BeFalse();
    }

    [Theory]
    [InlineData(42)]
    [InlineData(true)]
    [InlineData("short")]
    [InlineData(1.5)]
    public void Scalars_and_short_strings_pass_through_unchanged(object value)
    {
        ParameterCapture.Capture(value).Should().Be(value);
    }

    [Fact]
    public void Immutable_scalars_keep_their_type_for_reports()
    {
        ParameterCapture.Capture(Guid.Empty).Should().BeOfType<Guid>();
        ParameterCapture.Capture(DateTimeOffset.UnixEpoch).Should().BeOfType<DateTimeOffset>();
        ParameterCapture.Capture(DayOfWeek.Monday).Should().BeOfType<DayOfWeek>();
    }

    [Fact]
    public void Dbnull_becomes_null()
    {
        ParameterCapture.Capture(DBNull.Value).Should().BeNull();
        ParameterCapture.Capture(null).Should().BeNull();
    }

    [Fact]
    public void An_array_is_projected_and_not_retained_by_reference()
    {
        var values = new[] { 1, 2, 3 };
        var captured = ParameterCapture.Capture(values);

        var before = Fingerprint(captured);
        values[0] = 99;

        captured.Should().BeOfType<ParameterSnapshot>()
            .Which.TypeName.Should().Be("Int32[3]");
        Fingerprint(captured).Should().Be(before);
    }

    private static string Fingerprint(object? captured)
    {
        return new DefaultQueryAnalysisFactory().CreateFingerprinter(SqlNormalizationMode.WhitespaceOnly).ExactFingerprint(new RecordedQuery
        {
            CommandText = "SELECT * FROM t WHERE p = @p",
            Parameters = new Dictionary<string, object?> { ["@p"] = captured },
            Timestamp = DateTimeOffset.UnixEpoch
        });
    }

    private static string Report(object? captured, QueryParameterDisplayMode mode)
    {
        var query = new RecordedQuery
        {
            CommandText = "SELECT * FROM t WHERE p = @p",
            Parameters = new Dictionary<string, object?> { ["@p"] = captured },
            Timestamp = DateTimeOffset.UnixEpoch
        };

        var options = new QueryBudgetOptions { MaxQueries = 0, ParameterDisplayMode = mode };
        var metrics = new QueryMetricsCalculator().Calculate([query, query], options);
        var result = new QueryBudgetEvaluator().Evaluate(metrics);

        return new DefaultQueryReportFormatter().Format(result);
    }
}
