[![](https://img.shields.io/nuget/v/soenneker.atomics.valuenullablelazys.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.atomics.valuenullablelazys/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.atomics.valuenullablelazys/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.atomics.valuenullablelazys/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.atomics.valuenullablelazys.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.atomics.valuenullablelazys/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Atomics.ValueNullableLazys
### Allocation-conscious lazy initialization that correctly caches null values.

## Installation

```
dotnet add package Soenneker.Atomics.ValueNullableLazys
```

## Usage

```csharp
using Soenneker.Atomics.ValueLocks;
using Soenneker.Atomics.ValueNullableLazys;

public sealed class Resolver
{
    private ValueNullableLazy<Result> _result;
    private ValueAtomicLock _initializationLock;

    public Result? Resolve() =>
        _result.GetOrCreate(ref _initializationLock, this,
            static resolver => resolver.TryResolve());
}
```

`ValueNullableLazy<T>` occupies one reference-sized field and uses a private sentinel to distinguish “not initialized” from “initialized with null.” Several lazy fields on an owner can share one `ValueAtomicLock`.

- `GetOrCreate` provides execution-and-publication semantics.
- `GetOrCreatePublicationOnly` may run the factory concurrently but atomically publishes one result.
- `GetOrCreateUnsafe` performs no synchronization.
- `TryGetValue` returns `true` after initialization even when the cached value is `null`.
