// Assembly-level xUnit configuration. Placed in its own file so the attribute
// precedes all other elements (CS1730). The emergency-stop latch is
// process-wide (static) while the runner executes test classes in parallel;
// a P6 stop test holding the latch mid-flight would flake P0–P5 tests.
// Correctness of fail-closed tests outranks runner throughput.
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
