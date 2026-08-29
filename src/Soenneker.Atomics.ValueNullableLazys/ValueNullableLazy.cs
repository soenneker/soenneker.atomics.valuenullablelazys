using Soenneker.Atomics.ValueLocks;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Soenneker.Atomics.ValueNullableLazys;

/// <summary>
/// Provides inline lazy storage that distinguishes an uninitialized value from a successfully initialized
/// <see langword="null"/> result.
/// </summary>
/// <typeparam name="T">The reference type stored by the lazy value.</typeparam>
/// <remarks>
/// <para>
/// The default value is ready to use and occupies one reference-sized field. A <see cref="ValueAtomicLock"/> can be shared
/// by several lazy fields on the same owner, avoiding a separate synchronization object for every value.
/// </para>
/// <para>
/// This is a mutable <see langword="struct"/> intended for use as a private field. Avoid copying it because each copy has
/// independent initialization state. Exceptions thrown by a factory are not cached, and a later call may retry initialization.
/// </para>
/// </remarks>
[DebuggerDisplay("IsValueCreated = {IsValueCreated}")]
public struct ValueNullableLazy<T> where T : class
{
    private object? _value;

    /// <summary>
    /// Gets a value indicating whether initialization has completed successfully, including when the result was null.
    /// </summary>
    public bool IsValueCreated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _value) is not null;
    }

    /// <summary>
    /// Attempts to read the initialized value without invoking a factory. A true return value with a null output means
    /// initialization completed with null.
    /// </summary>
    /// <param name="value">Replacement value to store atomically.</param>
    /// <returns>true if the requested update was applied; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(out T? value)
    {
        object? stored = Volatile.Read(ref _value);
        if (stored is null)
        {
            value = null;
            return false;
        }

        value = Unwrap(stored);
        return true;
    }

    /// <summary>
    /// Gets the initialized value or invokes <paramref name="factory"/> exactly once using execution-and-publication semantics.
    /// </summary>
    /// <param name="sync">Synchronization object guarding one-time initialization.</param>
    /// <param name="factory">Factory used to create a value when one is needed.</param>
    /// <returns>The requested value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetOrCreate(ref ValueAtomicLock sync, Func<T?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return GetOrCreate(ref sync, factory, static valueFactory => valueFactory());
    }

    /// <summary>
    /// Gets the initialized value or invokes <paramref name="factory"/> exactly once using execution-and-publication semantics.
    /// Supplying state allows callers to use a static factory and avoid a closure allocation.
    /// </summary>
    /// <typeparam name="TState">Type of state passed to the callback.</typeparam>
    /// <param name="sync">Synchronization object guarding one-time initialization.</param>
    /// <param name="state">State value used by the variant.</param>
    /// <param name="factory">Factory used to create a value when one is needed.</param>
    /// <returns>The requested value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetOrCreate<TState>(ref ValueAtomicLock sync, TState state, Func<TState, T?> factory)
    {
        object? stored = Volatile.Read(ref _value);

        if (stored is null)
            stored = Initialize(ref sync, state, factory);

        return Unwrap(stored);
    }

    /// <summary>
    /// Gets or creates the value without locking. This method is only safe when the caller provides external synchronization
    /// or guarantees single-threaded access.
    /// </summary>
    /// <param name="factory">Factory used to create a value when one is needed.</param>
    /// <returns>The requested value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetOrCreateUnsafe(Func<T?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return GetOrCreateUnsafe(factory, static valueFactory => valueFactory());
    }

    /// <summary>
    /// Gets or creates the value without locking. Supplying state allows callers to use a static factory and avoid a closure allocation.
    /// </summary>
    /// <typeparam name="TState">Type of state passed to the callback.</typeparam>
    /// <param name="state">State value used by the variant.</param>
    /// <param name="factory">Factory used to create a value when one is needed.</param>
    /// <returns>The requested value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetOrCreateUnsafe<TState>(TState state, Func<TState, T?> factory)
    {
        object? stored = _value;
        if (stored is null)
        {
            stored = Wrap(Create(state, factory));
            _value = stored;
        }

        return Unwrap(stored);
    }

    /// <summary>
    /// Gets the initialized value or atomically publishes one factory result. During a race the factory may run more than once,
    /// but every caller observes the single published result.
    /// </summary>
    /// <param name="factory">Factory used to create a value when one is needed.</param>
    /// <returns>The requested value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetOrCreatePublicationOnly(Func<T?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return GetOrCreatePublicationOnly(factory, static valueFactory => valueFactory());
    }

    /// <summary>
    /// Gets the initialized value or atomically publishes one factory result. Supplying state allows callers to use a static
    /// factory and avoid a closure allocation.
    /// </summary>
    /// <typeparam name="TState">Type of state passed to the callback.</typeparam>
    /// <param name="state">State value used by the variant.</param>
    /// <param name="factory">Factory used to create a value when one is needed.</param>
    /// <returns>The requested value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetOrCreatePublicationOnly<TState>(TState state, Func<TState, T?> factory)
    {
        object? stored = Volatile.Read(ref _value);
        if (stored is not null)
            return Unwrap(stored);

        object created = Wrap(Create(state, factory));
        stored = Interlocked.CompareExchange(ref _value, created, null) ?? created;
        return Unwrap(stored);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object Initialize<TState>(ref ValueAtomicLock sync, TState state, Func<TState, T?> factory)
    {
        lock (sync.Get())
        {
            object? stored = _value;
            if (stored is not null)
                return stored;

            stored = Wrap(Create(state, factory));
            Volatile.Write(ref _value, stored);
            return stored;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T? Create<TState>(TState state, Func<TState, T?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory(state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object Wrap(T? value) => value ?? ValueNullableLazySentinel.Null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T? Unwrap(object value) => ReferenceEquals(value, ValueNullableLazySentinel.Null) ? null : (T)value;
}
