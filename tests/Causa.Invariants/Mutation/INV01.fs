// Owner: domain. INV-01 mutation test (PERSIST-023-O2): the read-back comparison is load-bearing.
namespace Causa.Invariants.Mutation

open Xunit
open Causa.Persistence
open Causa.Invariants.SaveFixture

module INV01 =
    /// The mutant the plan names: the read-back comparison removed by comparing the written bytes with themselves.
    let private comparisonRemoved investigation commit at (written: FileSet) (_readBack: FileSet) =
        Confirmation.ofReadBack investigation commit at written written

    [<Fact>]
    let ReadBackSkipped () =
        // The oracle of Property.INV01.NoSavedWithoutMatchingReadBack holds for the real constructor ...
        Assert.Empty(confirmationViolations Confirmation.ofReadBack)
        // ... and fails, for the intended reason, once the comparison is removed: different bytes get confirmed.
        let killed = confirmationViolations comparisonRemoved
        Assert.NotEmpty(killed)
        Assert.All(killed, fun v -> Assert.Contains("different bytes", v))
