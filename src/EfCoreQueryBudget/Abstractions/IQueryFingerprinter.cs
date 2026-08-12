namespace EfCoreQueryBudget;

public interface IQueryFingerprinter
{
    string StructuralFingerprint(RecordedQuery query);

    string ExactFingerprint(RecordedQuery query);
}
