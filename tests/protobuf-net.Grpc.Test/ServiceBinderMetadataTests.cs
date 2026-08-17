using ProtoBuf.Grpc.Configuration;
using System;
using System.Linq;
using Xunit;

namespace protobuf_net.Grpc.Test;

public class ServiceBinderMetadataTests
{
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class MarkAttribute(string who) : Attribute
    {
        public string Who { get; } = who;
    }

    // deliberately private to this test: the mapping cache is process-wide and keyed on these types,
    // so contract types shared with other suites would make the result depend on test order
    private interface IShared { void Op(); }

    private sealed class FirstImpl : IShared
    {
        [Mark(nameof(FirstImpl))]
        public void Op() { }
    }

    private sealed class SecondImpl : IShared
    {
        [Mark(nameof(SecondImpl))]
        public void Op() { }
    }

    /// <summary>
    /// Two classes implementing one contract must each report their own method's attributes.
    /// </summary>
    /// <remarks>
    /// An interface mapping is a property of the (contract, implementation) <em>pair</em>. The cache
    /// behind <c>GetMethodImplementation</c> used to be keyed on the contract alone, so the second
    /// implementation asked for was handed the first one's <c>TargetMethods</c> and attributes were
    /// read off the wrong class - silently, and order-dependently. That matters beyond metadata
    /// tidiness: an <c>[Authorize]</c> on one implementation would apply to, or go missing from, the
    /// other.
    /// </remarks>
    [Fact]
    public void MetadataIsPerImplementation()
    {
        var op = typeof(IShared).GetMethod(nameof(IShared.Op))!;

        // order matters: before the fix, whichever was asked for first populated the cache for both
        Assert.Equal(nameof(FirstImpl), MarkFor(op, typeof(FirstImpl)));
        Assert.Equal(nameof(SecondImpl), MarkFor(op, typeof(SecondImpl)));

        // and again in reverse, so the test cannot pass by luck of ordering
        Assert.Equal(nameof(SecondImpl), MarkFor(op, typeof(SecondImpl)));
        Assert.Equal(nameof(FirstImpl), MarkFor(op, typeof(FirstImpl)));

        static string MarkFor(System.Reflection.MethodInfo op, Type serviceType)
            => ServiceBinder.Default.GetMetadata(op, typeof(IShared), serviceType)
                .OfType<MarkAttribute>().Select(static x => x.Who).SingleOrDefault() ?? "(none)";
    }

    /// <summary>The same thing one level down, against the API that actually does the lookup.</summary>
    [Fact]
    public void MethodImplementationIsPerImplementation()
    {
        var op = typeof(IShared).GetMethod(nameof(IShared.Op))!;

        Assert.Equal(typeof(FirstImpl),
            ServiceBinder.Default.GetMethodImplementation(op, typeof(IShared), typeof(FirstImpl))?.DeclaringType);
        Assert.Equal(typeof(SecondImpl),
            ServiceBinder.Default.GetMethodImplementation(op, typeof(IShared), typeof(SecondImpl))?.DeclaringType);
    }
}
