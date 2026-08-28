using Xunit;

namespace IntelliMed.Tests.Ui;

/// <summary>
/// Base for every UI test class. Its only job is to close the browser contexts a test opened,
/// once that test has finished.
///
/// This needs a per-test hook, and <see cref="PlaywrightServerFixture"/> cannot provide one: it is
/// a collection fixture, so it is constructed once and disposed once, after the entire collection.
/// xUnit, by contrast, constructs a fresh instance of the *test class* for every test method — so
/// an <see cref="IAsyncLifetime"/> implemented here runs after each test, which is what we want.
///
/// Without this, contexts accumulated for the whole run: roughly two per test, since the common
/// pattern creates a client on one page and then opens a second, fresh one. Headed, that was a
/// visible window each.
/// </summary>
public abstract class UiTestBase : IAsyncLifetime
{
    protected readonly PlaywrightServerFixture Fixture;

    protected UiTestBase(PlaywrightServerFixture fixture) => Fixture = fixture;

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync() => Fixture.CloseOpenContextsAsync();
}
