using Xunit;

// Integration Tests share one real MySQL database (pems_test). xUnit runs different test
// classes in parallel by default, which let one class's DisposeAsync cleanup delete another
// class's in-flight data (observed as NotFound / DbUpdateConcurrencyException / a duplicate
// check that should have conflicted but didn't, when CreateFaqApiTests and UpdateFaqApiTests
// ran together). Disabling parallelization here is the runtime safety layer; each test class
// still using its own dedicated cleanup prefix (see DatabaseResetHelper) is the data-isolation
// layer — both are required, neither alone is sufficient.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
