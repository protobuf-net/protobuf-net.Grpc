using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Collections.Generic;
using Xunit;

namespace protobuf_net.Grpc.Test.Issues
{
    class TestServerBinder : ServerBinder // just tracks what methods are observed
    {
        public HashSet<string> Methods { get; } = new HashSet<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        protected override bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext, Method<TRequest, TResponse> method, MethodStub<TService> stub)
        {
            Methods.Add(method.Name);
            return true;
        }
        protected internal override void OnWarn(string message, object?[]? args = null)
            => Warnings.Add(string.Format(message, args));
        protected internal override void OnError(string message, object?[]? args = null)
            => Errors.Add(string.Format(message, args));
    }

    public class Issue87
    {
        [Theory]
        [InlineData(typeof(MyService), null)]
        [InlineData(typeof(MyServiceBase), null)]
        [InlineData(typeof(Foo), nameof(Foo))]
        [InlineData(typeof(FooBase), null)]
        [InlineData(typeof(Bar), nameof(Bar))]
        [InlineData(typeof(BarBase), nameof(BarBase))]
        [InlineData(typeof(IDerivedService), "protobuf_net.Grpc.Test.Issues.DerivedService")]
        [InlineData(typeof(IBaseService), "protobuf_net.Grpc.Test.Issues.BaseService")]
        [InlineData(typeof(INotDerivedService), null)]
        [InlineData(typeof(INotBaseService), null)]
        public void IsService(Type type, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Assert.False(ServiceBinder.Default.IsServiceContract(type, out _));
                Assert.Null(name);
            }
            else
            {
                Assert.True(ServiceBinder.Default.IsServiceContract(type, out var svcName));
                Assert.Equal(name, svcName);
            }
        }

        [Theory]
        [InlineData(typeof(Foo), new[] { nameof(Foo.PublicDerived), nameof(FooBase.PublicBase), nameof(FooBase.PublicPolymorphic) })]
        [InlineData(typeof(FooBase), new string[] { })]
        [InlineData(typeof(Bar), new[] { nameof(Bar.PublicDerived), nameof(BarBase.PublicBase), nameof(BarBase.PublicPolymorphic) })]
        [InlineData(typeof(BarBase), new[] { nameof(BarBase.PublicBase), nameof(BarBase.PublicPolymorphic) })]
        [InlineData(typeof(MyServiceBase), new[] { nameof(IBaseService.BaseServiceMethodExplicit), nameof(IBaseService.BaseServiceMethodImplicit) })]
        [InlineData(typeof(MyService), new[] { nameof(IBaseService.BaseServiceMethodExplicit), nameof(IBaseService.BaseServiceMethodImplicit), nameof(IDerivedService.DerivedServiceMethodExplicit), nameof(IDerivedService.DerivedServiceMethodImplicit) })]
        public void CanSeeCorrectMethods(Type type, string[] methods)
        {
            var binder = new TestServerBinder();
            int count = binder.Bind(this, type);
            Assert.Equal(methods.Length, count);
            Assert.Equal(methods.Length, binder.Methods.Count);
            foreach (var method in methods)
            {
                Assert.Contains(method, binder.Methods);
            }

            Assert.Empty(binder.Warnings);
            Assert.Empty(binder.Errors);
        }

        class MyService : MyServiceBase, IDerivedService, INotDerivedService
        {
            public void DerivedServiceMethodImplicit() { }
            public void DerivedNotServiceMethodImplicit() { }

            void IDerivedService.DerivedServiceMethodExplicit() { }
            void INotDerivedService.DerivedNotServiceMethodExplicit() { }
        }
        
        class MyServiceBase : IBaseService, INotBaseService
        {
            public void BaseNotServiceMethodImplicit() { }
            public void BaseServiceMethodImplicit() { }

            void IBaseService.BaseServiceMethodExplicit() { }
            void INotBaseService.BaseNotServiceMethodExplicit() { }
        }

        [Service]
        interface IDerivedService
        {
            void DerivedServiceMethodExplicit();
            void DerivedServiceMethodImplicit();
        }
        interface INotDerivedService
        {
            void DerivedNotServiceMethodExplicit();
            void DerivedNotServiceMethodImplicit();
        }
        [Service]
        interface IBaseService
        {
            void BaseServiceMethodExplicit();
            void BaseServiceMethodImplicit();
        }
        interface INotBaseService
        {
            void BaseNotServiceMethodExplicit();
            void BaseNotServiceMethodImplicit();
        }

        class Foo : FooBase, IGrpcService
        {
            public void PublicDerived() { }
            internal void NonPublicDerived() { }
            protected void ProtectedDerived() { }

            public override void PublicPolymorphic() { }
            protected override void ProtectedPolymorphic() { }
        }
        class FooBase
        {
            public void PublicBase() { }
            internal void NonPublicBase() { }
            protected void ProtectedBase() { }

            public virtual void PublicPolymorphic() { }
            protected virtual void ProtectedPolymorphic() { }
        }

        public class Bar : BarBase
        {
            public void PublicDerived() { }
            internal void NonPublicDerived() { }
            protected void ProtectedDerived() { }

            public override void PublicPolymorphic() { }
            protected override void ProtectedPolymorphic() { }
        }
        public class BarBase : IGrpcService
        {
            public void PublicBase() { }
            internal void NonPublicBase() { }
            protected void ProtectedBase() { }

            public virtual void PublicPolymorphic() { }
            protected virtual void ProtectedPolymorphic() { }
        }
    }
}
