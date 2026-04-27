namespace Change.Framework.Application
{
    public interface IUseCase<TRequest, TResult>
        where TRequest : struct
    {
        TResult Execute(in TRequest request);
    }
}
