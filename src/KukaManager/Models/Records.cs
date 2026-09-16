namespace KukaManager.Models;

public sealed record LookupItem(string Text, string? Value);
public sealed record BalanceInfo(int Lessons, long AmountFen);
public sealed record MetricItem(string Title, string Value, string Subtitle);
public sealed record FinanceAccountSummary(
    string Account, long Count, long IncomeFen, long ExpenseFen, long FeeFen, long NetFen,
    long? CurrentBalanceFen, long? LatestBalanceFen, string? BalanceDate,
    long PostBalanceNetFen, bool Estimated, string? LatestDate);

public sealed record FormChoice(string Text, object? Value)
{
    // This fallback is intentional.  WPF can ignore DisplayMemberPath while a
    // custom ComboBox template is being rebuilt; never expose the record's
    // generated "FormChoice { ... }" representation to an operator.
    public override string ToString() => Text;
}
public enum FormFieldKind { Text, LongText, Choice, Reference, Date, Time, Money, Integer, Check }
public sealed record FormField(
    string Key, string Label, FormFieldKind Kind, object? DefaultValue = null,
    IReadOnlyList<FormChoice>? Choices = null, bool Required = false);
