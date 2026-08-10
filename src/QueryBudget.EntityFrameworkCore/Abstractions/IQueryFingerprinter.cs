namespace ErickMorales.EntityFrameworkCore.QueryBudget;

public interface IQueryFingerprinter
{
    string StructuralFingerprint(RecordedQuery query);

    string ExactFingerprint(RecordedQuery query);
}
