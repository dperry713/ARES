// The emergency stop is a process-wide latch. Running API test classes in
// parallel would let the stop test refuse transitions asserted by the lifecycle
// tests, so this assembly is serialized deliberately.
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
