using AwesomeAssertions;
using Soenneker.Atomics.ValueLocks;
using Soenneker.Tests.Unit;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Atomics.ValueNullableLazys.Tests;

public sealed class ValueNullableLazyTests : UnitTest
{
    [Test]
    public void Default_should_be_uninitialized_and_reference_sized()
    {
        var value = new ValueNullableLazy<object>();

        value.IsValueCreated.Should().BeFalse();
        value.TryGetValue(out _).Should().BeFalse();
        Unsafe.SizeOf<ValueNullableLazy<object>>().Should().Be(IntPtr.Size);
    }

    [Test]
    public void GetOrCreate_should_cache_null()
    {
        var holder = new Holder();

        Payload? first = holder.Value.GetOrCreate(ref holder.Sync, holder, static state =>
        {
            state.FactoryCalls++;
            return null;
        });
        Payload? second = holder.Value.GetOrCreate(ref holder.Sync, holder, static state =>
        {
            state.FactoryCalls++;
            return new Payload(42);
        });

        first.Should().BeNull();
        second.Should().BeNull();
        holder.FactoryCalls.Should().Be(1);
        holder.Value.IsValueCreated.Should().BeTrue();
        holder.Value.TryGetValue(out Payload? cached).Should().BeTrue();
        cached.Should().BeNull();
    }

    [Test]
    public void GetOrCreate_should_cache_a_non_null_value()
    {
        var holder = new Holder();

        Payload? first = holder.Value.GetOrCreate(ref holder.Sync, static () => new Payload(42));
        Payload? second = holder.Value.GetOrCreate(ref holder.Sync, static () => new Payload(43));

        ReferenceEquals(first, second).Should().BeTrue();
        second!.Number.Should().Be(42);
    }

    [Test]
    public void Concurrent_execution_and_publication_should_cache_null_once()
    {
        var holder = new Holder();
        var values = new Payload?[128];

        Parallel.For(0, values.Length, i =>
        {
            values[i] = holder.Value.GetOrCreate(ref holder.Sync, holder, static state =>
            {
                Interlocked.Increment(ref state.FactoryCalls);
                Thread.SpinWait(20_000);
                return null;
            });
        });

        holder.FactoryCalls.Should().Be(1);
        values.All(value => value is null).Should().BeTrue();
        holder.Value.IsValueCreated.Should().BeTrue();
    }

    [Test]
    public void Publication_only_should_publish_one_non_null_result()
    {
        var holder = new Holder();
        var values = new Payload?[128];

        Parallel.For(0, values.Length, i =>
        {
            values[i] = holder.Value.GetOrCreatePublicationOnly(holder, static state =>
            {
                int number = Interlocked.Increment(ref state.FactoryCalls);
                Thread.SpinWait(20_000);
                return new Payload(number);
            });
        });

        holder.FactoryCalls.Should().BeGreaterThanOrEqualTo(1);
        values.All(value => ReferenceEquals(values[0], value)).Should().BeTrue();
    }

    [Test]
    public void Unsafe_initialization_should_cache_null_without_creating_the_lock()
    {
        var holder = new Holder();

        Payload? first = holder.Value.GetOrCreateUnsafe(static () => null);
        Payload? second = holder.Value.GetOrCreateUnsafe(static () => new Payload(42));

        first.Should().BeNull();
        second.Should().BeNull();
        holder.Value.IsValueCreated.Should().BeTrue();
        holder.Sync.IsValueCreated.Should().BeFalse();
    }

    private sealed class Holder
    {
        public ValueNullableLazy<Payload> Value;
        public ValueAtomicLock Sync;
        public int FactoryCalls;
    }

    private sealed record Payload(int Number);
}
