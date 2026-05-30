namespace Velucid.Silo.Authorization;

public sealed record AuthorizationTuple(
    string User,
    string Relation,
    string Object
);
