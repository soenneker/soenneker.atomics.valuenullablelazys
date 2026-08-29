[![](https://img.shields.io/nuget/v/soenneker.atomics.valuenullablelazys.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.atomics.valuenullablelazys/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.atomics.valuenullablelazys/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.atomics.valuenullablelazys/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.atomics.valuenullablelazys.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.atomics.valuenullablelazys/)

# Soenneker.Atomics.ValueNullableLazys

Provides inline lazy storage that distinguishes an uninitialized value from a successfully initialized `null` result.

## Install

```bash
dotnet add package Soenneker.Atomics.ValueNullableLazys
```

## What you get

- `ValueNullableLazy<T>` — Provides inline lazy storage that distinguishes an uninitialized value from a successfully initialized `null` result.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `ValueNullableLazy<T>.IsValueCreated` | Gets a value indicating whether initialization has completed successfully, including when the result was null. | Gets a value indicating whether initialization has completed successfully, including when the result was null. |
| `ValueNullableLazy<T>.TryGetValue(value)` | Attempts to read the initialized value without invoking a factory. A true return value with a null output means initialization completed with null. | true if the requested update was applied; otherwise, false. |
| `ValueNullableLazy<T>.GetOrCreate(sync, factory)` | Gets the initialized value or invokes `factory` exactly once using execution-and-publication semantics. | The requested value. |
| `ValueNullableLazy<T>.GetOrCreate(sync, state, factory)` | Gets the initialized value or invokes `factory` exactly once using execution-and-publication semantics. Supplying state allows callers to use a static factory and avoid a closure allocation. | The requested value. |
| `ValueNullableLazy<T>.GetOrCreateUnsafe(factory)` | Gets or creates the value without locking. This method is only safe when the caller provides external synchronization or guarantees single-threaded access. | The requested value. |
| `ValueNullableLazy<T>.GetOrCreateUnsafe(state, factory)` | Gets or creates the value without locking. Supplying state allows callers to use a static factory and avoid a closure allocation. | The requested value. |
| `ValueNullableLazy<T>.GetOrCreatePublicationOnly(factory)` | Gets the initialized value or atomically publishes one factory result. During a race the factory may run more than once, but every caller observes the single published result. | The requested value. |
| `ValueNullableLazy<T>.GetOrCreatePublicationOnly(state, factory)` | Gets the initialized value or atomically publishes one factory result. Supplying state allows callers to use a static factory and avoid a closure allocation. | The requested value. |

## Important behavior

- `ValueNullableLazy<T>`: The default value is ready to use and occupies one reference-sized field. A `ValueAtomicLock` can be shared by several lazy fields on the same owner, avoiding a separate synchronization object for every value. This is a mutable `struct` intended for use as a private field. Avoid copying it because each copy has independent initialization state. Exceptions thrown by a factory are not cached, and a later call may retry initialization.
