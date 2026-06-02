using Velucid.Silo.Authorization;

namespace Velucid.Silo.Tests.Infrastructure;

public class InMemoryOpenFgaAuthorizationService : IOpenFgaAuthorizationService
{
    private readonly HashSet<(string User, string Relation, string Object)> _tuples = [];

    private static readonly Dictionary<string, string[]> PermissionToRelations = new()
    {
        ["view_org"] = ["owner", "member"],
        ["view_members"] = ["owner", "member"],
        ["create_task"] = ["owner", "member"],
        ["create_product"] = ["owner"],
        ["delete_product"] = ["owner"],
        ["invite_member"] = ["owner"],
        ["change_member_role"] = ["owner"],
        ["remove_member"] = ["owner"],
        ["delete_org"] = ["owner"],
        ["manage_org_settings"] = ["owner"],
    };

    public Task<bool> Check(Guid userId, string permission, Guid resourceId)
    {
        if (!PermissionToRelations.TryGetValue(permission, out var relations))
            return Task.FromResult(false);

        var user = $"user:{userId}";
        var obj = $"organization:{resourceId}";

        foreach (var relation in relations)
        {
            if (_tuples.Contains((user, relation, obj)))
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task WriteTuples(IEnumerable<AuthorizationTuple> tuples)
    {
        foreach (var t in tuples)
            _tuples.Add((t.User, t.Relation, t.Object));
        return Task.CompletedTask;
    }

    public Task DeleteTuples(IEnumerable<AuthorizationTuple> tuples)
    {
        foreach (var t in tuples)
            _tuples.Remove((t.User, t.Relation, t.Object));
        return Task.CompletedTask;
    }
}
