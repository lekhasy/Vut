# Story 3.6: Authorization Test Suite

baseline_commit: (use commit from 3-5 after it completes)
Status: backlog

## Story

As a platform engineer,
I want comprehensive authorization tests covering OpenFGA permission checks,
so that we can verify the security model works correctly and catch regressions.

## Acceptance Criteria

1. **OpenFGA model test** — verify the model allows/denies correctly in unit test
2. **OrgGrain authorization tests** — each operation tested with Owner, Member, and non-member callers
3. **UserGrain authorization tests** — self-service operations tested with correct and incorrect caller
4. **Controller authorization tests** — verify 403 returned for unauthorized calls
5. **Integration test with OpenFGA** — real OpenFGA instance (or test container) for end-to-end verification

## Tasks / Subtasks

- [ ] Task 1: Add OpenFGA test infrastructure (real OpenFGA via testcontainers)
  - [ ] Subtask 1.1: Add `testcontainers-dotnet` package to test project
  - [ ] Subtask 1.2: Create `OpenFgaTestFixture.cs` using testcontainers to spin up centralized OpenFGA container
  - [ ] Subtask 1.3: Fixture initializes store, creates model, provides seed/clear helpers per test
  - [ ] Subtask 1.4: Teardown stops container after each test class
  - [ ] Subtask 1.5: Mark tests as `[Collection("OpenFga")]` so they run separately from unit tests
- [ ] Task 2: OrgGrain authorization tests
  - [ ] Subtask 2.1: `RenameOrg` — member allowed, non-member denied
  - [ ] Subtask 2.2: `DeleteOrg` — owner allowed, member denied, non-member denied
  - [ ] Subtask 2.3: `RemoveMember` — owner allowed, member denied
  - [ ] Subtask 2.4: `AddMember` — member allowed (transparent op), non-member denied
  - [ ] Subtask 2.5: `SendInvitation` — owner allowed, member denied (owner-only)
- [ ] Task 3: UserGrain authorization tests
  - [ ] Subtask 3.1: `UpdateProfile` — same user allowed, different user denied
  - [ ] Subtask 3.2: `RequestEmailVerification` — same user allowed, different user denied
  - [ ] Subtask 3.3: `VerifyEmail` — same user allowed, different user denied
- [ ] Task 4: Controller authorization tests
  - [ ] Subtask 4.1: Test that controller returns 403 when grain throws UnauthorizedException
  - [ ] Subtask 4.2: Test that controller uses server-side userId (no user-supplied userId bypass)
- [ ] Task 5: Coverage report
  - [ ] Subtask 5.1: Run coverage on authorization tests
  - [ ] Subtask 5.2: Ensure all permission checks have at least one test case

## Dev Notes

### Test Naming Convention

```
// OrgGrain authorization
RenameOrg_OwnerCalling_Succeeds()
RenameOrg_MemberCalling_Succeeds()
RenameOrg_NonMemberCalling_ThrowsUnauthorized()

DeleteOrg_OwnerCalling_Succeeds()
DeleteOrg_MemberCalling_ThrowsUnauthorized()

// UserGrain authorization
UpdateProfile_SameUserCalling_Succeeds()
UpdateProfile_DifferentUserCalling_ThrowsUnauthorized()
```

### Mock vs Integration

- **Unit tests**: Mock `IOpenFgaAuthorizationService` to test grain logic
- **Integration tests**: Use OpenFGA test container to verify real SDK behavior

For grains, the key is to mock the authorization service and verify the grain CALLS it correctly. The integration test verifies the actual OpenFGA Check returns correct results.

### Fixtures

```csharp
// Example test fixture setup
private OpenFgaTestFixture _fga;
private OrgGrain _grain;
private Mock<IOpenFgaAuthorizationService> _mockAuth;

[SetUp]
public async Task SetUp()
{
    _fga = new OpenFgaTestFixture();
    await _fga.InitializeAsync();
    _mockAuth = new Mock<IOpenFgaAuthorizationService>();

    // Seed tuples for test org
    await _fga.WriteTuples(new[] {
        new AuthorizationTuple { User = "user:owner1", Relation = "owner", Object = "organization:org1" },
        new AuthorizationTuple { User = "user:member1", Relation = "member", Object = "organization:org1" }
    });
}
```

## File List

**Files to CREATE:**
- `backend/tests/Velucid.Silo.Tests/Authorization/` — new test directory
- `backend/tests/Velucid.Silo.Tests/Authorization/OrgGrainAuthorizationTests.cs`
- `backend/tests/Velucid.Silo.Tests/Authorization/UserGrainAuthorizationTests.cs`
- `backend/tests/Velucid.Silo.Tests/Authorization/OpenFgaTestFixture.cs`

**Files to MODIFY:**
- `backend/tests/Velucid.Silo.Tests/Velucid.Silo.Tests.csproj` — add test dependencies

**Files to READ:**
- `backend/tests/Velucid.Silo.Tests/Grains/OrgGrainTests.cs` — existing grain tests
- OpenFGA .NET SDK test examples

## References

- Epic 3 spec: `_bmad-output/planning-artifacts/epic-3-authorization-openfga.md`
- Story 3.2: `_bmad-output/implementation-artifacts/3-2-migrate-org-grain-auth.md`
- Story 3.3: `_bmad-output/implementation-artifacts/3-3-migrate-user-grain-auth.md`