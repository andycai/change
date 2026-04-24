namespace Fun.Framework.Cqrs
{
    public interface IQueryHandler<TQuery, TResult>
        where TQuery : struct, IQuery<TResult>
    {
        TResult Handle(in TQuery query);
    }
}
