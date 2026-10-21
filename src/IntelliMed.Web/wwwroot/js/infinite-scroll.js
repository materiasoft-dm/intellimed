// Infinite-scroll helper: watches a sentinel element with IntersectionObserver and invokes a
// .NET method when it scrolls into view, so a Blazor page can page in more rows on demand instead
// of loading everything up front (see FeeSchedules.razor's Items tab).
window.IntelliMedInfiniteScroll = {
    _observers: {},

    observe: function (sentinelId, containerId, dotNetRef, methodName) {
        this.unobserve(sentinelId);

        var sentinel = document.getElementById(sentinelId);
        if (!sentinel) return;

        var root = containerId ? document.getElementById(containerId) : null;

        var observer = new IntersectionObserver(function (entries) {
            if (entries[0] && entries[0].isIntersecting) {
                dotNetRef.invokeMethodAsync(methodName);
            }
        }, { root: root, rootMargin: '150px' });

        observer.observe(sentinel);
        this._observers[sentinelId] = observer;
    },

    unobserve: function (sentinelId) {
        var existing = this._observers[sentinelId];
        if (existing) {
            existing.disconnect();
            delete this._observers[sentinelId];
        }
    }
};
