namespace Velucid.Silo.Authorization;

public interface IOpenFgaAuthorizationService
{
    Task<bool> Check(Guid userId, string permission, Guid resourceId);
    Task WriteTuples(IEnumerable<AuthorizationTuple> tuples);
    Task DeleteTuples(IEnumerable<AuthorizationTuple> tuples);
}
