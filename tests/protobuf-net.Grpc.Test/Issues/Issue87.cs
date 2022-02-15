using ProtoBuf.Grpc.Configuration;
using System;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.Grpc.Test.Issues
{


    public class Issue87
    {
        public Issue87(ITestOutputHelper log)
            => _log = log;
        private readonly ITestOutputHelper _log;
        private void Log(string message) => _log?.WriteLine(message);

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
        [InlineData(typeof(Foo), "/Foo/", new[] { nameof(Foo.PublicDerived), nameof(FooBase.PublicBase), nameof(FooBase.PublicPolymorphic) })]
        [InlineData(typeof(FooBase), "/FooBase/", new string[] { })]
        [InlineData(typeof(Bar), "/Bar/", new[] { nameof(Bar.PublicDerived), nameof(BarBase.PublicBase), nameof(BarBase.PublicPolymorphic) })]
        [InlineData(typeof(BarBase), "/BarBase/", new[] { nameof(BarBase.PublicBase), nameof(BarBase.PublicPolymorphic) })]
        [InlineData(typeof(MyServiceBase), "/protobuf_net.Grpc.Test.Issues.BaseService/", new[] { nameof(IBaseService.BaseServiceMethodExplicit), nameof(IBaseService.BaseServiceMethodImplicit) })]
        [InlineData(typeof(MyService), "/protobuf_net.Grpc.Test.Issues.", new[] {
            "BaseService/" + nameof(IBaseService.BaseServiceMethodExplicit), "BaseService/" + nameof(IBaseService.BaseServiceMethodImplicit),
            "DerivedService/" + nameof(IDerivedService.DerivedServiceMethodExplicit), "DerivedService/" + nameof(IDerivedService.DerivedServiceMethodImplicit)
        })]
        public void CanSeeCorrectMethods(Type type, string prefix, string[] methods)
        {
            var binder = new TestServerBinder();
            int count = binder.Bind(this, type);
            foreach (var bound in binder.Methods)
            {
                Log(bound);
            }
            Assert.Equal(methods.Length, count);
            Assert.Equal(methods.Length, binder.Methods.Count);
            foreach (var method in methods)
            {
                Assert.Contains(prefix + method, binder.Methods);
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
