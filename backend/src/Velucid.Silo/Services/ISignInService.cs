using Velucid.Silo.Models;

namespace Velucid.Silo.Services;

public interface ISignInService
{
    Task<SignInResult> SignIn(
        string sub, string providerName,
        string displayName, string avatarUrl, string? email);
}