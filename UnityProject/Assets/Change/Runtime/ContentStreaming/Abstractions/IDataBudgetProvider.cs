namespace Change.Runtime.ContentStreaming
{
    public interface IDataBudgetProvider
    {
        long DailyRemainingBytes { get; }

        void Consume(long bytes);
    }
}
