#if NET6_0_OR_GREATER

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using ProtoBuf.Grpc.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace protobuf_net.Grpc.Test.Issues;

public class Issue324
{
    private readonly ITestOutputHelper _output;

    public Issue324(ITestOutputHelper output)
        => _output = output;

    [Fact]
    public void Execute()
    {
        var watch = Stopwatch.StartNew();
        IServiceCollection svcCol = new ServiceCollection();
        svcCol.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new TestOutputLoggerProvider(_output));
        });
        svcCol.AddSingleton(new DiagnosticListener("test"));
        svcCol.AddControllers();
        svcCol.AddCodeFirstGrpc();
        var svc = svcCol.BuildServiceProvider();
        var app = new ApplicationBuilder(svc);
        app.UseRouting();
        watch.Stop();
        _output.WriteLine($"Init: {watch.ElapsedMilliseconds}ms");

        watch.Reset();
        app.UseEndpoints(ep =>
        {
            ep.MapGrpcService<TestService0>();
            ep.MapGrpcService<TestService1>();
            ep.MapGrpcService<TestService2>();
            ep.MapGrpcService<TestService3>();
            ep.MapGrpcService<TestService4>();
            ep.MapGrpcService<TestService5>();
            ep.MapGrpcService<TestService6>();
            ep.MapGrpcService<TestService7>();
            ep.MapGrpcService<TestService8>();
            ep.MapGrpcService<TestService9>();
            ep.MapGrpcService<TestService10>();
            ep.MapGrpcService<TestService11>();
            ep.MapGrpcService<TestService12>();
            ep.MapGrpcService<TestService13>();
            ep.MapGrpcService<TestService14>();
            ep.MapGrpcService<TestService15>();
            ep.MapGrpcService<TestService16>();
            ep.MapGrpcService<TestService17>();
            ep.MapGrpcService<TestService18>();
            ep.MapGrpcService<TestService19>();
            ep.MapGrpcService<TestService20>();
            ep.MapGrpcService<TestService21>();
            ep.MapGrpcService<TestService22>();
            ep.MapGrpcService<TestService23>();
            ep.MapGrpcService<TestService24>();
            ep.MapGrpcService<TestService25>();
            ep.MapGrpcService<TestService26>();
            ep.MapGrpcService<TestService27>();
            ep.MapGrpcService<TestService28>();
            ep.MapGrpcService<TestService29>();
            ep.MapGrpcService<TestService30>();
            ep.MapGrpcService<TestService31>();
            ep.MapGrpcService<TestService32>();
            ep.MapGrpcService<TestService33>();
            ep.MapGrpcService<TestService34>();
            ep.MapGrpcService<TestService35>();
            ep.MapGrpcService<TestService36>();
            ep.MapGrpcService<TestService37>();
            ep.MapGrpcService<TestService38>();
            ep.MapGrpcService<TestService39>();
            ep.MapGrpcService<TestService40>();
            ep.MapGrpcService<TestService41>();
            ep.MapGrpcService<TestService42>();
            ep.MapGrpcService<TestService43>();
            ep.MapGrpcService<TestService44>();
            ep.MapGrpcService<TestService45>();
            ep.MapGrpcService<TestService46>();
            ep.MapGrpcService<TestService47>();
            ep.MapGrpcService<TestService48>();
            ep.MapGrpcService<TestService49>();
            ep.MapGrpcService<TestService50>();
            ep.MapGrpcService<TestService51>();
            ep.MapGrpcService<TestService52>();
            ep.MapGrpcService<TestService53>();
            ep.MapGrpcService<TestService54>();
            ep.MapGrpcService<TestService55>();
            ep.MapGrpcService<TestService56>();
            ep.MapGrpcService<TestService57>();
            ep.MapGrpcService<TestService58>();
            ep.MapGrpcService<TestService59>();
            ep.MapGrpcService<TestService60>();
            ep.MapGrpcService<TestService61>();
            ep.MapGrpcService<TestService62>();
            ep.MapGrpcService<TestService63>();
            ep.MapGrpcService<TestService64>();
            ep.MapGrpcService<TestService65>();
            ep.MapGrpcService<TestService66>();
            ep.MapGrpcService<TestService67>();
            ep.MapGrpcService<TestService68>();
            ep.MapGrpcService<TestService69>();
            ep.MapGrpcService<TestService70>();
            ep.MapGrpcService<TestService71>();
            ep.MapGrpcService<TestService72>();
            ep.MapGrpcService<TestService73>();
            ep.MapGrpcService<TestService74>();
            ep.MapGrpcService<TestService75>();
            ep.MapGrpcService<TestService76>();
            ep.MapGrpcService<TestService77>();
            ep.MapGrpcService<TestService78>();
            ep.MapGrpcService<TestService79>();
            ep.MapGrpcService<TestService80>();
            ep.MapGrpcService<TestService81>();
            ep.MapGrpcService<TestService82>();
            ep.MapGrpcService<TestService83>();
            ep.MapGrpcService<TestService84>();
            ep.MapGrpcService<TestService85>();
            ep.MapGrpcService<TestService86>();
            ep.MapGrpcService<TestService87>();
            ep.MapGrpcService<TestService88>();
            ep.MapGrpcService<TestService89>();
            ep.MapGrpcService<TestService90>();
            ep.MapGrpcService<TestService91>();
            ep.MapGrpcService<TestService92>();
            ep.MapGrpcService<TestService93>();
            ep.MapGrpcService<TestService94>();
            ep.MapGrpcService<TestService95>();
            ep.MapGrpcService<TestService96>();
            ep.MapGrpcService<TestService97>();
            ep.MapGrpcService<TestService98>();
            ep.MapGrpcService<TestService99>();
        });
        watch.Stop();
        _output.WriteLine($"UseEndpoints: {watch.ElapsedMilliseconds}ms");

        watch.Reset();
        _ = app.Build();
        watch.Stop();
        _output.WriteLine($"Build: {watch.ElapsedMilliseconds}ms");
    }

    class TestOutputLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public TestOutputLoggerProvider(ITestOutputHelper output)
            => _output = output;


        public ILogger CreateLogger(string categoryName) => new Logger(_output, categoryName);

        public void Dispose() { }

        class Logger : ILogger, IDisposable
        {
            private readonly ITestOutputHelper _output;
            private readonly string _categoryName;

            public Logger(ITestOutputHelper output, string categoryName)
            {
                _output = output;
                _categoryName = categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => this;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _output.WriteLine(_categoryName + ":" + formatter(state, exception));
            }
            public void Dispose() { }
        }
    }


    #region Model
    public class TestService0 : ITestService0
    {
        Task<Model0> ITestService0.BasicAsync(Model0 model) => throw new NotImplementedException();
        Model0 ITestService0.BasicSync(Model0 model) => throw new NotImplementedException();
        Task ITestService0.ClientStreaming(IAsyncEnumerable<Model0.Model0_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model0.Model0_0> ITestService0.Duplex(IAsyncEnumerable<Model0.Model0_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model0.Model0_0> ITestService0.ServerStreaming() => throw new NotImplementedException();
        Task ITestService0.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService0
    {
        Task VoidVoidAsync();

        Model0 BasicSync(Model0 model);

        Task<Model0> BasicAsync(Model0 model);

        IAsyncEnumerable<Model0.Model0_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model0.Model0_1> model);

        IAsyncEnumerable<Model0.Model0_0> Duplex(IAsyncEnumerable<Model0.Model0_1> model);
    }

    [ProtoContract]
    public class Model0
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model0_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model0_1> Bars { get; } = new();

        [ProtoContract]
        public class Model0_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model0_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService1 : ITestService1
    {
        Task<Model1> ITestService1.BasicAsync(Model1 model) => throw new NotImplementedException();
        Model1 ITestService1.BasicSync(Model1 model) => throw new NotImplementedException();
        Task ITestService1.ClientStreaming(IAsyncEnumerable<Model1.Model1_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model1.Model1_0> ITestService1.Duplex(IAsyncEnumerable<Model1.Model1_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model1.Model1_0> ITestService1.ServerStreaming() => throw new NotImplementedException();
        Task ITestService1.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService1
    {
        Task VoidVoidAsync();

        Model1 BasicSync(Model1 model);

        Task<Model1> BasicAsync(Model1 model);

        IAsyncEnumerable<Model1.Model1_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model1.Model1_1> model);

        IAsyncEnumerable<Model1.Model1_0> Duplex(IAsyncEnumerable<Model1.Model1_1> model);
    }

    [ProtoContract]
    public class Model1
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model1_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model1_1> Bars { get; } = new();

        [ProtoContract]
        public class Model1_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model1_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService2 : ITestService2
    {
        Task<Model2> ITestService2.BasicAsync(Model2 model) => throw new NotImplementedException();
        Model2 ITestService2.BasicSync(Model2 model) => throw new NotImplementedException();
        Task ITestService2.ClientStreaming(IAsyncEnumerable<Model2.Model2_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model2.Model2_0> ITestService2.Duplex(IAsyncEnumerable<Model2.Model2_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model2.Model2_0> ITestService2.ServerStreaming() => throw new NotImplementedException();
        Task ITestService2.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService2
    {
        Task VoidVoidAsync();

        Model2 BasicSync(Model2 model);

        Task<Model2> BasicAsync(Model2 model);

        IAsyncEnumerable<Model2.Model2_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model2.Model2_1> model);

        IAsyncEnumerable<Model2.Model2_0> Duplex(IAsyncEnumerable<Model2.Model2_1> model);
    }

    [ProtoContract]
    public class Model2
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model2_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model2_1> Bars { get; } = new();

        [ProtoContract]
        public class Model2_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model2_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService3 : ITestService3
    {
        Task<Model3> ITestService3.BasicAsync(Model3 model) => throw new NotImplementedException();
        Model3 ITestService3.BasicSync(Model3 model) => throw new NotImplementedException();
        Task ITestService3.ClientStreaming(IAsyncEnumerable<Model3.Model3_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model3.Model3_0> ITestService3.Duplex(IAsyncEnumerable<Model3.Model3_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model3.Model3_0> ITestService3.ServerStreaming() => throw new NotImplementedException();
        Task ITestService3.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService3
    {
        Task VoidVoidAsync();

        Model3 BasicSync(Model3 model);

        Task<Model3> BasicAsync(Model3 model);

        IAsyncEnumerable<Model3.Model3_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model3.Model3_1> model);

        IAsyncEnumerable<Model3.Model3_0> Duplex(IAsyncEnumerable<Model3.Model3_1> model);
    }

    [ProtoContract]
    public class Model3
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model3_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model3_1> Bars { get; } = new();

        [ProtoContract]
        public class Model3_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model3_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService4 : ITestService4
    {
        Task<Model4> ITestService4.BasicAsync(Model4 model) => throw new NotImplementedException();
        Model4 ITestService4.BasicSync(Model4 model) => throw new NotImplementedException();
        Task ITestService4.ClientStreaming(IAsyncEnumerable<Model4.Model4_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model4.Model4_0> ITestService4.Duplex(IAsyncEnumerable<Model4.Model4_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model4.Model4_0> ITestService4.ServerStreaming() => throw new NotImplementedException();
        Task ITestService4.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService4
    {
        Task VoidVoidAsync();

        Model4 BasicSync(Model4 model);

        Task<Model4> BasicAsync(Model4 model);

        IAsyncEnumerable<Model4.Model4_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model4.Model4_1> model);

        IAsyncEnumerable<Model4.Model4_0> Duplex(IAsyncEnumerable<Model4.Model4_1> model);
    }

    [ProtoContract]
    public class Model4
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model4_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model4_1> Bars { get; } = new();

        [ProtoContract]
        public class Model4_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model4_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService5 : ITestService5
    {
        Task<Model5> ITestService5.BasicAsync(Model5 model) => throw new NotImplementedException();
        Model5 ITestService5.BasicSync(Model5 model) => throw new NotImplementedException();
        Task ITestService5.ClientStreaming(IAsyncEnumerable<Model5.Model5_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model5.Model5_0> ITestService5.Duplex(IAsyncEnumerable<Model5.Model5_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model5.Model5_0> ITestService5.ServerStreaming() => throw new NotImplementedException();
        Task ITestService5.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService5
    {
        Task VoidVoidAsync();

        Model5 BasicSync(Model5 model);

        Task<Model5> BasicAsync(Model5 model);

        IAsyncEnumerable<Model5.Model5_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model5.Model5_1> model);

        IAsyncEnumerable<Model5.Model5_0> Duplex(IAsyncEnumerable<Model5.Model5_1> model);
    }

    [ProtoContract]
    public class Model5
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model5_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model5_1> Bars { get; } = new();

        [ProtoContract]
        public class Model5_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model5_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService6 : ITestService6
    {
        Task<Model6> ITestService6.BasicAsync(Model6 model) => throw new NotImplementedException();
        Model6 ITestService6.BasicSync(Model6 model) => throw new NotImplementedException();
        Task ITestService6.ClientStreaming(IAsyncEnumerable<Model6.Model6_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model6.Model6_0> ITestService6.Duplex(IAsyncEnumerable<Model6.Model6_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model6.Model6_0> ITestService6.ServerStreaming() => throw new NotImplementedException();
        Task ITestService6.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService6
    {
        Task VoidVoidAsync();

        Model6 BasicSync(Model6 model);

        Task<Model6> BasicAsync(Model6 model);

        IAsyncEnumerable<Model6.Model6_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model6.Model6_1> model);

        IAsyncEnumerable<Model6.Model6_0> Duplex(IAsyncEnumerable<Model6.Model6_1> model);
    }

    [ProtoContract]
    public class Model6
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model6_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model6_1> Bars { get; } = new();

        [ProtoContract]
        public class Model6_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model6_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService7 : ITestService7
    {
        Task<Model7> ITestService7.BasicAsync(Model7 model) => throw new NotImplementedException();
        Model7 ITestService7.BasicSync(Model7 model) => throw new NotImplementedException();
        Task ITestService7.ClientStreaming(IAsyncEnumerable<Model7.Model7_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model7.Model7_0> ITestService7.Duplex(IAsyncEnumerable<Model7.Model7_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model7.Model7_0> ITestService7.ServerStreaming() => throw new NotImplementedException();
        Task ITestService7.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService7
    {
        Task VoidVoidAsync();

        Model7 BasicSync(Model7 model);

        Task<Model7> BasicAsync(Model7 model);

        IAsyncEnumerable<Model7.Model7_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model7.Model7_1> model);

        IAsyncEnumerable<Model7.Model7_0> Duplex(IAsyncEnumerable<Model7.Model7_1> model);
    }

    [ProtoContract]
    public class Model7
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model7_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model7_1> Bars { get; } = new();

        [ProtoContract]
        public class Model7_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model7_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService8 : ITestService8
    {
        Task<Model8> ITestService8.BasicAsync(Model8 model) => throw new NotImplementedException();
        Model8 ITestService8.BasicSync(Model8 model) => throw new NotImplementedException();
        Task ITestService8.ClientStreaming(IAsyncEnumerable<Model8.Model8_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model8.Model8_0> ITestService8.Duplex(IAsyncEnumerable<Model8.Model8_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model8.Model8_0> ITestService8.ServerStreaming() => throw new NotImplementedException();
        Task ITestService8.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService8
    {
        Task VoidVoidAsync();

        Model8 BasicSync(Model8 model);

        Task<Model8> BasicAsync(Model8 model);

        IAsyncEnumerable<Model8.Model8_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model8.Model8_1> model);

        IAsyncEnumerable<Model8.Model8_0> Duplex(IAsyncEnumerable<Model8.Model8_1> model);
    }

    [ProtoContract]
    public class Model8
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model8_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model8_1> Bars { get; } = new();

        [ProtoContract]
        public class Model8_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model8_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService9 : ITestService9
    {
        Task<Model9> ITestService9.BasicAsync(Model9 model) => throw new NotImplementedException();
        Model9 ITestService9.BasicSync(Model9 model) => throw new NotImplementedException();
        Task ITestService9.ClientStreaming(IAsyncEnumerable<Model9.Model9_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model9.Model9_0> ITestService9.Duplex(IAsyncEnumerable<Model9.Model9_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model9.Model9_0> ITestService9.ServerStreaming() => throw new NotImplementedException();
        Task ITestService9.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService9
    {
        Task VoidVoidAsync();

        Model9 BasicSync(Model9 model);

        Task<Model9> BasicAsync(Model9 model);

        IAsyncEnumerable<Model9.Model9_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model9.Model9_1> model);

        IAsyncEnumerable<Model9.Model9_0> Duplex(IAsyncEnumerable<Model9.Model9_1> model);
    }

    [ProtoContract]
    public class Model9
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model9_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model9_1> Bars { get; } = new();

        [ProtoContract]
        public class Model9_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model9_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService10 : ITestService10
    {
        Task<Model10> ITestService10.BasicAsync(Model10 model) => throw new NotImplementedException();
        Model10 ITestService10.BasicSync(Model10 model) => throw new NotImplementedException();
        Task ITestService10.ClientStreaming(IAsyncEnumerable<Model10.Model10_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model10.Model10_0> ITestService10.Duplex(IAsyncEnumerable<Model10.Model10_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model10.Model10_0> ITestService10.ServerStreaming() => throw new NotImplementedException();
        Task ITestService10.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService10
    {
        Task VoidVoidAsync();

        Model10 BasicSync(Model10 model);

        Task<Model10> BasicAsync(Model10 model);

        IAsyncEnumerable<Model10.Model10_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model10.Model10_1> model);

        IAsyncEnumerable<Model10.Model10_0> Duplex(IAsyncEnumerable<Model10.Model10_1> model);
    }

    [ProtoContract]
    public class Model10
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model10_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model10_1> Bars { get; } = new();

        [ProtoContract]
        public class Model10_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model10_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService11 : ITestService11
    {
        Task<Model11> ITestService11.BasicAsync(Model11 model) => throw new NotImplementedException();
        Model11 ITestService11.BasicSync(Model11 model) => throw new NotImplementedException();
        Task ITestService11.ClientStreaming(IAsyncEnumerable<Model11.Model11_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model11.Model11_0> ITestService11.Duplex(IAsyncEnumerable<Model11.Model11_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model11.Model11_0> ITestService11.ServerStreaming() => throw new NotImplementedException();
        Task ITestService11.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService11
    {
        Task VoidVoidAsync();

        Model11 BasicSync(Model11 model);

        Task<Model11> BasicAsync(Model11 model);

        IAsyncEnumerable<Model11.Model11_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model11.Model11_1> model);

        IAsyncEnumerable<Model11.Model11_0> Duplex(IAsyncEnumerable<Model11.Model11_1> model);
    }

    [ProtoContract]
    public class Model11
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model11_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model11_1> Bars { get; } = new();

        [ProtoContract]
        public class Model11_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model11_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService12 : ITestService12
    {
        Task<Model12> ITestService12.BasicAsync(Model12 model) => throw new NotImplementedException();
        Model12 ITestService12.BasicSync(Model12 model) => throw new NotImplementedException();
        Task ITestService12.ClientStreaming(IAsyncEnumerable<Model12.Model12_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model12.Model12_0> ITestService12.Duplex(IAsyncEnumerable<Model12.Model12_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model12.Model12_0> ITestService12.ServerStreaming() => throw new NotImplementedException();
        Task ITestService12.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService12
    {
        Task VoidVoidAsync();

        Model12 BasicSync(Model12 model);

        Task<Model12> BasicAsync(Model12 model);

        IAsyncEnumerable<Model12.Model12_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model12.Model12_1> model);

        IAsyncEnumerable<Model12.Model12_0> Duplex(IAsyncEnumerable<Model12.Model12_1> model);
    }

    [ProtoContract]
    public class Model12
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model12_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model12_1> Bars { get; } = new();

        [ProtoContract]
        public class Model12_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model12_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService13 : ITestService13
    {
        Task<Model13> ITestService13.BasicAsync(Model13 model) => throw new NotImplementedException();
        Model13 ITestService13.BasicSync(Model13 model) => throw new NotImplementedException();
        Task ITestService13.ClientStreaming(IAsyncEnumerable<Model13.Model13_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model13.Model13_0> ITestService13.Duplex(IAsyncEnumerable<Model13.Model13_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model13.Model13_0> ITestService13.ServerStreaming() => throw new NotImplementedException();
        Task ITestService13.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService13
    {
        Task VoidVoidAsync();

        Model13 BasicSync(Model13 model);

        Task<Model13> BasicAsync(Model13 model);

        IAsyncEnumerable<Model13.Model13_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model13.Model13_1> model);

        IAsyncEnumerable<Model13.Model13_0> Duplex(IAsyncEnumerable<Model13.Model13_1> model);
    }

    [ProtoContract]
    public class Model13
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model13_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model13_1> Bars { get; } = new();

        [ProtoContract]
        public class Model13_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model13_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService14 : ITestService14
    {
        Task<Model14> ITestService14.BasicAsync(Model14 model) => throw new NotImplementedException();
        Model14 ITestService14.BasicSync(Model14 model) => throw new NotImplementedException();
        Task ITestService14.ClientStreaming(IAsyncEnumerable<Model14.Model14_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model14.Model14_0> ITestService14.Duplex(IAsyncEnumerable<Model14.Model14_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model14.Model14_0> ITestService14.ServerStreaming() => throw new NotImplementedException();
        Task ITestService14.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService14
    {
        Task VoidVoidAsync();

        Model14 BasicSync(Model14 model);

        Task<Model14> BasicAsync(Model14 model);

        IAsyncEnumerable<Model14.Model14_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model14.Model14_1> model);

        IAsyncEnumerable<Model14.Model14_0> Duplex(IAsyncEnumerable<Model14.Model14_1> model);
    }

    [ProtoContract]
    public class Model14
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model14_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model14_1> Bars { get; } = new();

        [ProtoContract]
        public class Model14_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model14_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService15 : ITestService15
    {
        Task<Model15> ITestService15.BasicAsync(Model15 model) => throw new NotImplementedException();
        Model15 ITestService15.BasicSync(Model15 model) => throw new NotImplementedException();
        Task ITestService15.ClientStreaming(IAsyncEnumerable<Model15.Model15_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model15.Model15_0> ITestService15.Duplex(IAsyncEnumerable<Model15.Model15_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model15.Model15_0> ITestService15.ServerStreaming() => throw new NotImplementedException();
        Task ITestService15.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService15
    {
        Task VoidVoidAsync();

        Model15 BasicSync(Model15 model);

        Task<Model15> BasicAsync(Model15 model);

        IAsyncEnumerable<Model15.Model15_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model15.Model15_1> model);

        IAsyncEnumerable<Model15.Model15_0> Duplex(IAsyncEnumerable<Model15.Model15_1> model);
    }

    [ProtoContract]
    public class Model15
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model15_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model15_1> Bars { get; } = new();

        [ProtoContract]
        public class Model15_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model15_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService16 : ITestService16
    {
        Task<Model16> ITestService16.BasicAsync(Model16 model) => throw new NotImplementedException();
        Model16 ITestService16.BasicSync(Model16 model) => throw new NotImplementedException();
        Task ITestService16.ClientStreaming(IAsyncEnumerable<Model16.Model16_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model16.Model16_0> ITestService16.Duplex(IAsyncEnumerable<Model16.Model16_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model16.Model16_0> ITestService16.ServerStreaming() => throw new NotImplementedException();
        Task ITestService16.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService16
    {
        Task VoidVoidAsync();

        Model16 BasicSync(Model16 model);

        Task<Model16> BasicAsync(Model16 model);

        IAsyncEnumerable<Model16.Model16_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model16.Model16_1> model);

        IAsyncEnumerable<Model16.Model16_0> Duplex(IAsyncEnumerable<Model16.Model16_1> model);
    }

    [ProtoContract]
    public class Model16
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model16_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model16_1> Bars { get; } = new();

        [ProtoContract]
        public class Model16_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model16_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService17 : ITestService17
    {
        Task<Model17> ITestService17.BasicAsync(Model17 model) => throw new NotImplementedException();
        Model17 ITestService17.BasicSync(Model17 model) => throw new NotImplementedException();
        Task ITestService17.ClientStreaming(IAsyncEnumerable<Model17.Model17_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model17.Model17_0> ITestService17.Duplex(IAsyncEnumerable<Model17.Model17_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model17.Model17_0> ITestService17.ServerStreaming() => throw new NotImplementedException();
        Task ITestService17.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService17
    {
        Task VoidVoidAsync();

        Model17 BasicSync(Model17 model);

        Task<Model17> BasicAsync(Model17 model);

        IAsyncEnumerable<Model17.Model17_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model17.Model17_1> model);

        IAsyncEnumerable<Model17.Model17_0> Duplex(IAsyncEnumerable<Model17.Model17_1> model);
    }

    [ProtoContract]
    public class Model17
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model17_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model17_1> Bars { get; } = new();

        [ProtoContract]
        public class Model17_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model17_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService18 : ITestService18
    {
        Task<Model18> ITestService18.BasicAsync(Model18 model) => throw new NotImplementedException();
        Model18 ITestService18.BasicSync(Model18 model) => throw new NotImplementedException();
        Task ITestService18.ClientStreaming(IAsyncEnumerable<Model18.Model18_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model18.Model18_0> ITestService18.Duplex(IAsyncEnumerable<Model18.Model18_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model18.Model18_0> ITestService18.ServerStreaming() => throw new NotImplementedException();
        Task ITestService18.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService18
    {
        Task VoidVoidAsync();

        Model18 BasicSync(Model18 model);

        Task<Model18> BasicAsync(Model18 model);

        IAsyncEnumerable<Model18.Model18_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model18.Model18_1> model);

        IAsyncEnumerable<Model18.Model18_0> Duplex(IAsyncEnumerable<Model18.Model18_1> model);
    }

    [ProtoContract]
    public class Model18
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model18_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model18_1> Bars { get; } = new();

        [ProtoContract]
        public class Model18_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model18_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService19 : ITestService19
    {
        Task<Model19> ITestService19.BasicAsync(Model19 model) => throw new NotImplementedException();
        Model19 ITestService19.BasicSync(Model19 model) => throw new NotImplementedException();
        Task ITestService19.ClientStreaming(IAsyncEnumerable<Model19.Model19_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model19.Model19_0> ITestService19.Duplex(IAsyncEnumerable<Model19.Model19_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model19.Model19_0> ITestService19.ServerStreaming() => throw new NotImplementedException();
        Task ITestService19.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService19
    {
        Task VoidVoidAsync();

        Model19 BasicSync(Model19 model);

        Task<Model19> BasicAsync(Model19 model);

        IAsyncEnumerable<Model19.Model19_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model19.Model19_1> model);

        IAsyncEnumerable<Model19.Model19_0> Duplex(IAsyncEnumerable<Model19.Model19_1> model);
    }

    [ProtoContract]
    public class Model19
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model19_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model19_1> Bars { get; } = new();

        [ProtoContract]
        public class Model19_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model19_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService20 : ITestService20
    {
        Task<Model20> ITestService20.BasicAsync(Model20 model) => throw new NotImplementedException();
        Model20 ITestService20.BasicSync(Model20 model) => throw new NotImplementedException();
        Task ITestService20.ClientStreaming(IAsyncEnumerable<Model20.Model20_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model20.Model20_0> ITestService20.Duplex(IAsyncEnumerable<Model20.Model20_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model20.Model20_0> ITestService20.ServerStreaming() => throw new NotImplementedException();
        Task ITestService20.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService20
    {
        Task VoidVoidAsync();

        Model20 BasicSync(Model20 model);

        Task<Model20> BasicAsync(Model20 model);

        IAsyncEnumerable<Model20.Model20_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model20.Model20_1> model);

        IAsyncEnumerable<Model20.Model20_0> Duplex(IAsyncEnumerable<Model20.Model20_1> model);
    }

    [ProtoContract]
    public class Model20
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model20_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model20_1> Bars { get; } = new();

        [ProtoContract]
        public class Model20_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model20_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService21 : ITestService21
    {
        Task<Model21> ITestService21.BasicAsync(Model21 model) => throw new NotImplementedException();
        Model21 ITestService21.BasicSync(Model21 model) => throw new NotImplementedException();
        Task ITestService21.ClientStreaming(IAsyncEnumerable<Model21.Model21_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model21.Model21_0> ITestService21.Duplex(IAsyncEnumerable<Model21.Model21_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model21.Model21_0> ITestService21.ServerStreaming() => throw new NotImplementedException();
        Task ITestService21.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService21
    {
        Task VoidVoidAsync();

        Model21 BasicSync(Model21 model);

        Task<Model21> BasicAsync(Model21 model);

        IAsyncEnumerable<Model21.Model21_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model21.Model21_1> model);

        IAsyncEnumerable<Model21.Model21_0> Duplex(IAsyncEnumerable<Model21.Model21_1> model);
    }

    [ProtoContract]
    public class Model21
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model21_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model21_1> Bars { get; } = new();

        [ProtoContract]
        public class Model21_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model21_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService22 : ITestService22
    {
        Task<Model22> ITestService22.BasicAsync(Model22 model) => throw new NotImplementedException();
        Model22 ITestService22.BasicSync(Model22 model) => throw new NotImplementedException();
        Task ITestService22.ClientStreaming(IAsyncEnumerable<Model22.Model22_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model22.Model22_0> ITestService22.Duplex(IAsyncEnumerable<Model22.Model22_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model22.Model22_0> ITestService22.ServerStreaming() => throw new NotImplementedException();
        Task ITestService22.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService22
    {
        Task VoidVoidAsync();

        Model22 BasicSync(Model22 model);

        Task<Model22> BasicAsync(Model22 model);

        IAsyncEnumerable<Model22.Model22_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model22.Model22_1> model);

        IAsyncEnumerable<Model22.Model22_0> Duplex(IAsyncEnumerable<Model22.Model22_1> model);
    }

    [ProtoContract]
    public class Model22
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model22_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model22_1> Bars { get; } = new();

        [ProtoContract]
        public class Model22_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model22_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService23 : ITestService23
    {
        Task<Model23> ITestService23.BasicAsync(Model23 model) => throw new NotImplementedException();
        Model23 ITestService23.BasicSync(Model23 model) => throw new NotImplementedException();
        Task ITestService23.ClientStreaming(IAsyncEnumerable<Model23.Model23_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model23.Model23_0> ITestService23.Duplex(IAsyncEnumerable<Model23.Model23_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model23.Model23_0> ITestService23.ServerStreaming() => throw new NotImplementedException();
        Task ITestService23.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService23
    {
        Task VoidVoidAsync();

        Model23 BasicSync(Model23 model);

        Task<Model23> BasicAsync(Model23 model);

        IAsyncEnumerable<Model23.Model23_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model23.Model23_1> model);

        IAsyncEnumerable<Model23.Model23_0> Duplex(IAsyncEnumerable<Model23.Model23_1> model);
    }

    [ProtoContract]
    public class Model23
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model23_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model23_1> Bars { get; } = new();

        [ProtoContract]
        public class Model23_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model23_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService24 : ITestService24
    {
        Task<Model24> ITestService24.BasicAsync(Model24 model) => throw new NotImplementedException();
        Model24 ITestService24.BasicSync(Model24 model) => throw new NotImplementedException();
        Task ITestService24.ClientStreaming(IAsyncEnumerable<Model24.Model24_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model24.Model24_0> ITestService24.Duplex(IAsyncEnumerable<Model24.Model24_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model24.Model24_0> ITestService24.ServerStreaming() => throw new NotImplementedException();
        Task ITestService24.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService24
    {
        Task VoidVoidAsync();

        Model24 BasicSync(Model24 model);

        Task<Model24> BasicAsync(Model24 model);

        IAsyncEnumerable<Model24.Model24_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model24.Model24_1> model);

        IAsyncEnumerable<Model24.Model24_0> Duplex(IAsyncEnumerable<Model24.Model24_1> model);
    }

    [ProtoContract]
    public class Model24
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model24_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model24_1> Bars { get; } = new();

        [ProtoContract]
        public class Model24_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model24_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService25 : ITestService25
    {
        Task<Model25> ITestService25.BasicAsync(Model25 model) => throw new NotImplementedException();
        Model25 ITestService25.BasicSync(Model25 model) => throw new NotImplementedException();
        Task ITestService25.ClientStreaming(IAsyncEnumerable<Model25.Model25_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model25.Model25_0> ITestService25.Duplex(IAsyncEnumerable<Model25.Model25_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model25.Model25_0> ITestService25.ServerStreaming() => throw new NotImplementedException();
        Task ITestService25.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService25
    {
        Task VoidVoidAsync();

        Model25 BasicSync(Model25 model);

        Task<Model25> BasicAsync(Model25 model);

        IAsyncEnumerable<Model25.Model25_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model25.Model25_1> model);

        IAsyncEnumerable<Model25.Model25_0> Duplex(IAsyncEnumerable<Model25.Model25_1> model);
    }

    [ProtoContract]
    public class Model25
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model25_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model25_1> Bars { get; } = new();

        [ProtoContract]
        public class Model25_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model25_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService26 : ITestService26
    {
        Task<Model26> ITestService26.BasicAsync(Model26 model) => throw new NotImplementedException();
        Model26 ITestService26.BasicSync(Model26 model) => throw new NotImplementedException();
        Task ITestService26.ClientStreaming(IAsyncEnumerable<Model26.Model26_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model26.Model26_0> ITestService26.Duplex(IAsyncEnumerable<Model26.Model26_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model26.Model26_0> ITestService26.ServerStreaming() => throw new NotImplementedException();
        Task ITestService26.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService26
    {
        Task VoidVoidAsync();

        Model26 BasicSync(Model26 model);

        Task<Model26> BasicAsync(Model26 model);

        IAsyncEnumerable<Model26.Model26_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model26.Model26_1> model);

        IAsyncEnumerable<Model26.Model26_0> Duplex(IAsyncEnumerable<Model26.Model26_1> model);
    }

    [ProtoContract]
    public class Model26
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model26_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model26_1> Bars { get; } = new();

        [ProtoContract]
        public class Model26_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model26_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService27 : ITestService27
    {
        Task<Model27> ITestService27.BasicAsync(Model27 model) => throw new NotImplementedException();
        Model27 ITestService27.BasicSync(Model27 model) => throw new NotImplementedException();
        Task ITestService27.ClientStreaming(IAsyncEnumerable<Model27.Model27_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model27.Model27_0> ITestService27.Duplex(IAsyncEnumerable<Model27.Model27_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model27.Model27_0> ITestService27.ServerStreaming() => throw new NotImplementedException();
        Task ITestService27.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService27
    {
        Task VoidVoidAsync();

        Model27 BasicSync(Model27 model);

        Task<Model27> BasicAsync(Model27 model);

        IAsyncEnumerable<Model27.Model27_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model27.Model27_1> model);

        IAsyncEnumerable<Model27.Model27_0> Duplex(IAsyncEnumerable<Model27.Model27_1> model);
    }

    [ProtoContract]
    public class Model27
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model27_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model27_1> Bars { get; } = new();

        [ProtoContract]
        public class Model27_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model27_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService28 : ITestService28
    {
        Task<Model28> ITestService28.BasicAsync(Model28 model) => throw new NotImplementedException();
        Model28 ITestService28.BasicSync(Model28 model) => throw new NotImplementedException();
        Task ITestService28.ClientStreaming(IAsyncEnumerable<Model28.Model28_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model28.Model28_0> ITestService28.Duplex(IAsyncEnumerable<Model28.Model28_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model28.Model28_0> ITestService28.ServerStreaming() => throw new NotImplementedException();
        Task ITestService28.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService28
    {
        Task VoidVoidAsync();

        Model28 BasicSync(Model28 model);

        Task<Model28> BasicAsync(Model28 model);

        IAsyncEnumerable<Model28.Model28_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model28.Model28_1> model);

        IAsyncEnumerable<Model28.Model28_0> Duplex(IAsyncEnumerable<Model28.Model28_1> model);
    }

    [ProtoContract]
    public class Model28
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model28_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model28_1> Bars { get; } = new();

        [ProtoContract]
        public class Model28_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model28_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService29 : ITestService29
    {
        Task<Model29> ITestService29.BasicAsync(Model29 model) => throw new NotImplementedException();
        Model29 ITestService29.BasicSync(Model29 model) => throw new NotImplementedException();
        Task ITestService29.ClientStreaming(IAsyncEnumerable<Model29.Model29_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model29.Model29_0> ITestService29.Duplex(IAsyncEnumerable<Model29.Model29_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model29.Model29_0> ITestService29.ServerStreaming() => throw new NotImplementedException();
        Task ITestService29.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService29
    {
        Task VoidVoidAsync();

        Model29 BasicSync(Model29 model);

        Task<Model29> BasicAsync(Model29 model);

        IAsyncEnumerable<Model29.Model29_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model29.Model29_1> model);

        IAsyncEnumerable<Model29.Model29_0> Duplex(IAsyncEnumerable<Model29.Model29_1> model);
    }

    [ProtoContract]
    public class Model29
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model29_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model29_1> Bars { get; } = new();

        [ProtoContract]
        public class Model29_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model29_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService30 : ITestService30
    {
        Task<Model30> ITestService30.BasicAsync(Model30 model) => throw new NotImplementedException();
        Model30 ITestService30.BasicSync(Model30 model) => throw new NotImplementedException();
        Task ITestService30.ClientStreaming(IAsyncEnumerable<Model30.Model30_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model30.Model30_0> ITestService30.Duplex(IAsyncEnumerable<Model30.Model30_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model30.Model30_0> ITestService30.ServerStreaming() => throw new NotImplementedException();
        Task ITestService30.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService30
    {
        Task VoidVoidAsync();

        Model30 BasicSync(Model30 model);

        Task<Model30> BasicAsync(Model30 model);

        IAsyncEnumerable<Model30.Model30_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model30.Model30_1> model);

        IAsyncEnumerable<Model30.Model30_0> Duplex(IAsyncEnumerable<Model30.Model30_1> model);
    }

    [ProtoContract]
    public class Model30
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model30_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model30_1> Bars { get; } = new();

        [ProtoContract]
        public class Model30_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model30_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService31 : ITestService31
    {
        Task<Model31> ITestService31.BasicAsync(Model31 model) => throw new NotImplementedException();
        Model31 ITestService31.BasicSync(Model31 model) => throw new NotImplementedException();
        Task ITestService31.ClientStreaming(IAsyncEnumerable<Model31.Model31_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model31.Model31_0> ITestService31.Duplex(IAsyncEnumerable<Model31.Model31_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model31.Model31_0> ITestService31.ServerStreaming() => throw new NotImplementedException();
        Task ITestService31.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService31
    {
        Task VoidVoidAsync();

        Model31 BasicSync(Model31 model);

        Task<Model31> BasicAsync(Model31 model);

        IAsyncEnumerable<Model31.Model31_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model31.Model31_1> model);

        IAsyncEnumerable<Model31.Model31_0> Duplex(IAsyncEnumerable<Model31.Model31_1> model);
    }

    [ProtoContract]
    public class Model31
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model31_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model31_1> Bars { get; } = new();

        [ProtoContract]
        public class Model31_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model31_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService32 : ITestService32
    {
        Task<Model32> ITestService32.BasicAsync(Model32 model) => throw new NotImplementedException();
        Model32 ITestService32.BasicSync(Model32 model) => throw new NotImplementedException();
        Task ITestService32.ClientStreaming(IAsyncEnumerable<Model32.Model32_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model32.Model32_0> ITestService32.Duplex(IAsyncEnumerable<Model32.Model32_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model32.Model32_0> ITestService32.ServerStreaming() => throw new NotImplementedException();
        Task ITestService32.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService32
    {
        Task VoidVoidAsync();

        Model32 BasicSync(Model32 model);

        Task<Model32> BasicAsync(Model32 model);

        IAsyncEnumerable<Model32.Model32_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model32.Model32_1> model);

        IAsyncEnumerable<Model32.Model32_0> Duplex(IAsyncEnumerable<Model32.Model32_1> model);
    }

    [ProtoContract]
    public class Model32
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model32_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model32_1> Bars { get; } = new();

        [ProtoContract]
        public class Model32_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model32_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService33 : ITestService33
    {
        Task<Model33> ITestService33.BasicAsync(Model33 model) => throw new NotImplementedException();
        Model33 ITestService33.BasicSync(Model33 model) => throw new NotImplementedException();
        Task ITestService33.ClientStreaming(IAsyncEnumerable<Model33.Model33_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model33.Model33_0> ITestService33.Duplex(IAsyncEnumerable<Model33.Model33_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model33.Model33_0> ITestService33.ServerStreaming() => throw new NotImplementedException();
        Task ITestService33.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService33
    {
        Task VoidVoidAsync();

        Model33 BasicSync(Model33 model);

        Task<Model33> BasicAsync(Model33 model);

        IAsyncEnumerable<Model33.Model33_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model33.Model33_1> model);

        IAsyncEnumerable<Model33.Model33_0> Duplex(IAsyncEnumerable<Model33.Model33_1> model);
    }

    [ProtoContract]
    public class Model33
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model33_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model33_1> Bars { get; } = new();

        [ProtoContract]
        public class Model33_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model33_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService34 : ITestService34
    {
        Task<Model34> ITestService34.BasicAsync(Model34 model) => throw new NotImplementedException();
        Model34 ITestService34.BasicSync(Model34 model) => throw new NotImplementedException();
        Task ITestService34.ClientStreaming(IAsyncEnumerable<Model34.Model34_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model34.Model34_0> ITestService34.Duplex(IAsyncEnumerable<Model34.Model34_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model34.Model34_0> ITestService34.ServerStreaming() => throw new NotImplementedException();
        Task ITestService34.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService34
    {
        Task VoidVoidAsync();

        Model34 BasicSync(Model34 model);

        Task<Model34> BasicAsync(Model34 model);

        IAsyncEnumerable<Model34.Model34_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model34.Model34_1> model);

        IAsyncEnumerable<Model34.Model34_0> Duplex(IAsyncEnumerable<Model34.Model34_1> model);
    }

    [ProtoContract]
    public class Model34
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model34_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model34_1> Bars { get; } = new();

        [ProtoContract]
        public class Model34_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model34_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService35 : ITestService35
    {
        Task<Model35> ITestService35.BasicAsync(Model35 model) => throw new NotImplementedException();
        Model35 ITestService35.BasicSync(Model35 model) => throw new NotImplementedException();
        Task ITestService35.ClientStreaming(IAsyncEnumerable<Model35.Model35_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model35.Model35_0> ITestService35.Duplex(IAsyncEnumerable<Model35.Model35_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model35.Model35_0> ITestService35.ServerStreaming() => throw new NotImplementedException();
        Task ITestService35.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService35
    {
        Task VoidVoidAsync();

        Model35 BasicSync(Model35 model);

        Task<Model35> BasicAsync(Model35 model);

        IAsyncEnumerable<Model35.Model35_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model35.Model35_1> model);

        IAsyncEnumerable<Model35.Model35_0> Duplex(IAsyncEnumerable<Model35.Model35_1> model);
    }

    [ProtoContract]
    public class Model35
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model35_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model35_1> Bars { get; } = new();

        [ProtoContract]
        public class Model35_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model35_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService36 : ITestService36
    {
        Task<Model36> ITestService36.BasicAsync(Model36 model) => throw new NotImplementedException();
        Model36 ITestService36.BasicSync(Model36 model) => throw new NotImplementedException();
        Task ITestService36.ClientStreaming(IAsyncEnumerable<Model36.Model36_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model36.Model36_0> ITestService36.Duplex(IAsyncEnumerable<Model36.Model36_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model36.Model36_0> ITestService36.ServerStreaming() => throw new NotImplementedException();
        Task ITestService36.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService36
    {
        Task VoidVoidAsync();

        Model36 BasicSync(Model36 model);

        Task<Model36> BasicAsync(Model36 model);

        IAsyncEnumerable<Model36.Model36_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model36.Model36_1> model);

        IAsyncEnumerable<Model36.Model36_0> Duplex(IAsyncEnumerable<Model36.Model36_1> model);
    }

    [ProtoContract]
    public class Model36
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model36_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model36_1> Bars { get; } = new();

        [ProtoContract]
        public class Model36_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model36_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService37 : ITestService37
    {
        Task<Model37> ITestService37.BasicAsync(Model37 model) => throw new NotImplementedException();
        Model37 ITestService37.BasicSync(Model37 model) => throw new NotImplementedException();
        Task ITestService37.ClientStreaming(IAsyncEnumerable<Model37.Model37_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model37.Model37_0> ITestService37.Duplex(IAsyncEnumerable<Model37.Model37_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model37.Model37_0> ITestService37.ServerStreaming() => throw new NotImplementedException();
        Task ITestService37.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService37
    {
        Task VoidVoidAsync();

        Model37 BasicSync(Model37 model);

        Task<Model37> BasicAsync(Model37 model);

        IAsyncEnumerable<Model37.Model37_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model37.Model37_1> model);

        IAsyncEnumerable<Model37.Model37_0> Duplex(IAsyncEnumerable<Model37.Model37_1> model);
    }

    [ProtoContract]
    public class Model37
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model37_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model37_1> Bars { get; } = new();

        [ProtoContract]
        public class Model37_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model37_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService38 : ITestService38
    {
        Task<Model38> ITestService38.BasicAsync(Model38 model) => throw new NotImplementedException();
        Model38 ITestService38.BasicSync(Model38 model) => throw new NotImplementedException();
        Task ITestService38.ClientStreaming(IAsyncEnumerable<Model38.Model38_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model38.Model38_0> ITestService38.Duplex(IAsyncEnumerable<Model38.Model38_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model38.Model38_0> ITestService38.ServerStreaming() => throw new NotImplementedException();
        Task ITestService38.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService38
    {
        Task VoidVoidAsync();

        Model38 BasicSync(Model38 model);

        Task<Model38> BasicAsync(Model38 model);

        IAsyncEnumerable<Model38.Model38_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model38.Model38_1> model);

        IAsyncEnumerable<Model38.Model38_0> Duplex(IAsyncEnumerable<Model38.Model38_1> model);
    }

    [ProtoContract]
    public class Model38
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model38_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model38_1> Bars { get; } = new();

        [ProtoContract]
        public class Model38_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model38_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService39 : ITestService39
    {
        Task<Model39> ITestService39.BasicAsync(Model39 model) => throw new NotImplementedException();
        Model39 ITestService39.BasicSync(Model39 model) => throw new NotImplementedException();
        Task ITestService39.ClientStreaming(IAsyncEnumerable<Model39.Model39_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model39.Model39_0> ITestService39.Duplex(IAsyncEnumerable<Model39.Model39_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model39.Model39_0> ITestService39.ServerStreaming() => throw new NotImplementedException();
        Task ITestService39.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService39
    {
        Task VoidVoidAsync();

        Model39 BasicSync(Model39 model);

        Task<Model39> BasicAsync(Model39 model);

        IAsyncEnumerable<Model39.Model39_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model39.Model39_1> model);

        IAsyncEnumerable<Model39.Model39_0> Duplex(IAsyncEnumerable<Model39.Model39_1> model);
    }

    [ProtoContract]
    public class Model39
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model39_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model39_1> Bars { get; } = new();

        [ProtoContract]
        public class Model39_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model39_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService40 : ITestService40
    {
        Task<Model40> ITestService40.BasicAsync(Model40 model) => throw new NotImplementedException();
        Model40 ITestService40.BasicSync(Model40 model) => throw new NotImplementedException();
        Task ITestService40.ClientStreaming(IAsyncEnumerable<Model40.Model40_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model40.Model40_0> ITestService40.Duplex(IAsyncEnumerable<Model40.Model40_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model40.Model40_0> ITestService40.ServerStreaming() => throw new NotImplementedException();
        Task ITestService40.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService40
    {
        Task VoidVoidAsync();

        Model40 BasicSync(Model40 model);

        Task<Model40> BasicAsync(Model40 model);

        IAsyncEnumerable<Model40.Model40_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model40.Model40_1> model);

        IAsyncEnumerable<Model40.Model40_0> Duplex(IAsyncEnumerable<Model40.Model40_1> model);
    }

    [ProtoContract]
    public class Model40
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model40_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model40_1> Bars { get; } = new();

        [ProtoContract]
        public class Model40_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model40_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService41 : ITestService41
    {
        Task<Model41> ITestService41.BasicAsync(Model41 model) => throw new NotImplementedException();
        Model41 ITestService41.BasicSync(Model41 model) => throw new NotImplementedException();
        Task ITestService41.ClientStreaming(IAsyncEnumerable<Model41.Model41_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model41.Model41_0> ITestService41.Duplex(IAsyncEnumerable<Model41.Model41_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model41.Model41_0> ITestService41.ServerStreaming() => throw new NotImplementedException();
        Task ITestService41.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService41
    {
        Task VoidVoidAsync();

        Model41 BasicSync(Model41 model);

        Task<Model41> BasicAsync(Model41 model);

        IAsyncEnumerable<Model41.Model41_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model41.Model41_1> model);

        IAsyncEnumerable<Model41.Model41_0> Duplex(IAsyncEnumerable<Model41.Model41_1> model);
    }

    [ProtoContract]
    public class Model41
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model41_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model41_1> Bars { get; } = new();

        [ProtoContract]
        public class Model41_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model41_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService42 : ITestService42
    {
        Task<Model42> ITestService42.BasicAsync(Model42 model) => throw new NotImplementedException();
        Model42 ITestService42.BasicSync(Model42 model) => throw new NotImplementedException();
        Task ITestService42.ClientStreaming(IAsyncEnumerable<Model42.Model42_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model42.Model42_0> ITestService42.Duplex(IAsyncEnumerable<Model42.Model42_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model42.Model42_0> ITestService42.ServerStreaming() => throw new NotImplementedException();
        Task ITestService42.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService42
    {
        Task VoidVoidAsync();

        Model42 BasicSync(Model42 model);

        Task<Model42> BasicAsync(Model42 model);

        IAsyncEnumerable<Model42.Model42_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model42.Model42_1> model);

        IAsyncEnumerable<Model42.Model42_0> Duplex(IAsyncEnumerable<Model42.Model42_1> model);
    }

    [ProtoContract]
    public class Model42
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model42_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model42_1> Bars { get; } = new();

        [ProtoContract]
        public class Model42_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model42_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService43 : ITestService43
    {
        Task<Model43> ITestService43.BasicAsync(Model43 model) => throw new NotImplementedException();
        Model43 ITestService43.BasicSync(Model43 model) => throw new NotImplementedException();
        Task ITestService43.ClientStreaming(IAsyncEnumerable<Model43.Model43_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model43.Model43_0> ITestService43.Duplex(IAsyncEnumerable<Model43.Model43_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model43.Model43_0> ITestService43.ServerStreaming() => throw new NotImplementedException();
        Task ITestService43.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService43
    {
        Task VoidVoidAsync();

        Model43 BasicSync(Model43 model);

        Task<Model43> BasicAsync(Model43 model);

        IAsyncEnumerable<Model43.Model43_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model43.Model43_1> model);

        IAsyncEnumerable<Model43.Model43_0> Duplex(IAsyncEnumerable<Model43.Model43_1> model);
    }

    [ProtoContract]
    public class Model43
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model43_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model43_1> Bars { get; } = new();

        [ProtoContract]
        public class Model43_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model43_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService44 : ITestService44
    {
        Task<Model44> ITestService44.BasicAsync(Model44 model) => throw new NotImplementedException();
        Model44 ITestService44.BasicSync(Model44 model) => throw new NotImplementedException();
        Task ITestService44.ClientStreaming(IAsyncEnumerable<Model44.Model44_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model44.Model44_0> ITestService44.Duplex(IAsyncEnumerable<Model44.Model44_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model44.Model44_0> ITestService44.ServerStreaming() => throw new NotImplementedException();
        Task ITestService44.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService44
    {
        Task VoidVoidAsync();

        Model44 BasicSync(Model44 model);

        Task<Model44> BasicAsync(Model44 model);

        IAsyncEnumerable<Model44.Model44_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model44.Model44_1> model);

        IAsyncEnumerable<Model44.Model44_0> Duplex(IAsyncEnumerable<Model44.Model44_1> model);
    }

    [ProtoContract]
    public class Model44
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model44_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model44_1> Bars { get; } = new();

        [ProtoContract]
        public class Model44_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model44_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService45 : ITestService45
    {
        Task<Model45> ITestService45.BasicAsync(Model45 model) => throw new NotImplementedException();
        Model45 ITestService45.BasicSync(Model45 model) => throw new NotImplementedException();
        Task ITestService45.ClientStreaming(IAsyncEnumerable<Model45.Model45_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model45.Model45_0> ITestService45.Duplex(IAsyncEnumerable<Model45.Model45_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model45.Model45_0> ITestService45.ServerStreaming() => throw new NotImplementedException();
        Task ITestService45.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService45
    {
        Task VoidVoidAsync();

        Model45 BasicSync(Model45 model);

        Task<Model45> BasicAsync(Model45 model);

        IAsyncEnumerable<Model45.Model45_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model45.Model45_1> model);

        IAsyncEnumerable<Model45.Model45_0> Duplex(IAsyncEnumerable<Model45.Model45_1> model);
    }

    [ProtoContract]
    public class Model45
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model45_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model45_1> Bars { get; } = new();

        [ProtoContract]
        public class Model45_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model45_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService46 : ITestService46
    {
        Task<Model46> ITestService46.BasicAsync(Model46 model) => throw new NotImplementedException();
        Model46 ITestService46.BasicSync(Model46 model) => throw new NotImplementedException();
        Task ITestService46.ClientStreaming(IAsyncEnumerable<Model46.Model46_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model46.Model46_0> ITestService46.Duplex(IAsyncEnumerable<Model46.Model46_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model46.Model46_0> ITestService46.ServerStreaming() => throw new NotImplementedException();
        Task ITestService46.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService46
    {
        Task VoidVoidAsync();

        Model46 BasicSync(Model46 model);

        Task<Model46> BasicAsync(Model46 model);

        IAsyncEnumerable<Model46.Model46_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model46.Model46_1> model);

        IAsyncEnumerable<Model46.Model46_0> Duplex(IAsyncEnumerable<Model46.Model46_1> model);
    }

    [ProtoContract]
    public class Model46
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model46_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model46_1> Bars { get; } = new();

        [ProtoContract]
        public class Model46_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model46_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService47 : ITestService47
    {
        Task<Model47> ITestService47.BasicAsync(Model47 model) => throw new NotImplementedException();
        Model47 ITestService47.BasicSync(Model47 model) => throw new NotImplementedException();
        Task ITestService47.ClientStreaming(IAsyncEnumerable<Model47.Model47_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model47.Model47_0> ITestService47.Duplex(IAsyncEnumerable<Model47.Model47_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model47.Model47_0> ITestService47.ServerStreaming() => throw new NotImplementedException();
        Task ITestService47.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService47
    {
        Task VoidVoidAsync();

        Model47 BasicSync(Model47 model);

        Task<Model47> BasicAsync(Model47 model);

        IAsyncEnumerable<Model47.Model47_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model47.Model47_1> model);

        IAsyncEnumerable<Model47.Model47_0> Duplex(IAsyncEnumerable<Model47.Model47_1> model);
    }

    [ProtoContract]
    public class Model47
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model47_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model47_1> Bars { get; } = new();

        [ProtoContract]
        public class Model47_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model47_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService48 : ITestService48
    {
        Task<Model48> ITestService48.BasicAsync(Model48 model) => throw new NotImplementedException();
        Model48 ITestService48.BasicSync(Model48 model) => throw new NotImplementedException();
        Task ITestService48.ClientStreaming(IAsyncEnumerable<Model48.Model48_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model48.Model48_0> ITestService48.Duplex(IAsyncEnumerable<Model48.Model48_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model48.Model48_0> ITestService48.ServerStreaming() => throw new NotImplementedException();
        Task ITestService48.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService48
    {
        Task VoidVoidAsync();

        Model48 BasicSync(Model48 model);

        Task<Model48> BasicAsync(Model48 model);

        IAsyncEnumerable<Model48.Model48_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model48.Model48_1> model);

        IAsyncEnumerable<Model48.Model48_0> Duplex(IAsyncEnumerable<Model48.Model48_1> model);
    }

    [ProtoContract]
    public class Model48
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model48_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model48_1> Bars { get; } = new();

        [ProtoContract]
        public class Model48_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model48_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService49 : ITestService49
    {
        Task<Model49> ITestService49.BasicAsync(Model49 model) => throw new NotImplementedException();
        Model49 ITestService49.BasicSync(Model49 model) => throw new NotImplementedException();
        Task ITestService49.ClientStreaming(IAsyncEnumerable<Model49.Model49_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model49.Model49_0> ITestService49.Duplex(IAsyncEnumerable<Model49.Model49_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model49.Model49_0> ITestService49.ServerStreaming() => throw new NotImplementedException();
        Task ITestService49.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService49
    {
        Task VoidVoidAsync();

        Model49 BasicSync(Model49 model);

        Task<Model49> BasicAsync(Model49 model);

        IAsyncEnumerable<Model49.Model49_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model49.Model49_1> model);

        IAsyncEnumerable<Model49.Model49_0> Duplex(IAsyncEnumerable<Model49.Model49_1> model);
    }

    [ProtoContract]
    public class Model49
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model49_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model49_1> Bars { get; } = new();

        [ProtoContract]
        public class Model49_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model49_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService50 : ITestService50
    {
        Task<Model50> ITestService50.BasicAsync(Model50 model) => throw new NotImplementedException();
        Model50 ITestService50.BasicSync(Model50 model) => throw new NotImplementedException();
        Task ITestService50.ClientStreaming(IAsyncEnumerable<Model50.Model50_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model50.Model50_0> ITestService50.Duplex(IAsyncEnumerable<Model50.Model50_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model50.Model50_0> ITestService50.ServerStreaming() => throw new NotImplementedException();
        Task ITestService50.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService50
    {
        Task VoidVoidAsync();

        Model50 BasicSync(Model50 model);

        Task<Model50> BasicAsync(Model50 model);

        IAsyncEnumerable<Model50.Model50_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model50.Model50_1> model);

        IAsyncEnumerable<Model50.Model50_0> Duplex(IAsyncEnumerable<Model50.Model50_1> model);
    }

    [ProtoContract]
    public class Model50
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model50_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model50_1> Bars { get; } = new();

        [ProtoContract]
        public class Model50_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model50_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService51 : ITestService51
    {
        Task<Model51> ITestService51.BasicAsync(Model51 model) => throw new NotImplementedException();
        Model51 ITestService51.BasicSync(Model51 model) => throw new NotImplementedException();
        Task ITestService51.ClientStreaming(IAsyncEnumerable<Model51.Model51_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model51.Model51_0> ITestService51.Duplex(IAsyncEnumerable<Model51.Model51_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model51.Model51_0> ITestService51.ServerStreaming() => throw new NotImplementedException();
        Task ITestService51.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService51
    {
        Task VoidVoidAsync();

        Model51 BasicSync(Model51 model);

        Task<Model51> BasicAsync(Model51 model);

        IAsyncEnumerable<Model51.Model51_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model51.Model51_1> model);

        IAsyncEnumerable<Model51.Model51_0> Duplex(IAsyncEnumerable<Model51.Model51_1> model);
    }

    [ProtoContract]
    public class Model51
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model51_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model51_1> Bars { get; } = new();

        [ProtoContract]
        public class Model51_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model51_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService52 : ITestService52
    {
        Task<Model52> ITestService52.BasicAsync(Model52 model) => throw new NotImplementedException();
        Model52 ITestService52.BasicSync(Model52 model) => throw new NotImplementedException();
        Task ITestService52.ClientStreaming(IAsyncEnumerable<Model52.Model52_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model52.Model52_0> ITestService52.Duplex(IAsyncEnumerable<Model52.Model52_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model52.Model52_0> ITestService52.ServerStreaming() => throw new NotImplementedException();
        Task ITestService52.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService52
    {
        Task VoidVoidAsync();

        Model52 BasicSync(Model52 model);

        Task<Model52> BasicAsync(Model52 model);

        IAsyncEnumerable<Model52.Model52_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model52.Model52_1> model);

        IAsyncEnumerable<Model52.Model52_0> Duplex(IAsyncEnumerable<Model52.Model52_1> model);
    }

    [ProtoContract]
    public class Model52
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model52_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model52_1> Bars { get; } = new();

        [ProtoContract]
        public class Model52_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model52_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService53 : ITestService53
    {
        Task<Model53> ITestService53.BasicAsync(Model53 model) => throw new NotImplementedException();
        Model53 ITestService53.BasicSync(Model53 model) => throw new NotImplementedException();
        Task ITestService53.ClientStreaming(IAsyncEnumerable<Model53.Model53_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model53.Model53_0> ITestService53.Duplex(IAsyncEnumerable<Model53.Model53_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model53.Model53_0> ITestService53.ServerStreaming() => throw new NotImplementedException();
        Task ITestService53.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService53
    {
        Task VoidVoidAsync();

        Model53 BasicSync(Model53 model);

        Task<Model53> BasicAsync(Model53 model);

        IAsyncEnumerable<Model53.Model53_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model53.Model53_1> model);

        IAsyncEnumerable<Model53.Model53_0> Duplex(IAsyncEnumerable<Model53.Model53_1> model);
    }

    [ProtoContract]
    public class Model53
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model53_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model53_1> Bars { get; } = new();

        [ProtoContract]
        public class Model53_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model53_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService54 : ITestService54
    {
        Task<Model54> ITestService54.BasicAsync(Model54 model) => throw new NotImplementedException();
        Model54 ITestService54.BasicSync(Model54 model) => throw new NotImplementedException();
        Task ITestService54.ClientStreaming(IAsyncEnumerable<Model54.Model54_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model54.Model54_0> ITestService54.Duplex(IAsyncEnumerable<Model54.Model54_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model54.Model54_0> ITestService54.ServerStreaming() => throw new NotImplementedException();
        Task ITestService54.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService54
    {
        Task VoidVoidAsync();

        Model54 BasicSync(Model54 model);

        Task<Model54> BasicAsync(Model54 model);

        IAsyncEnumerable<Model54.Model54_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model54.Model54_1> model);

        IAsyncEnumerable<Model54.Model54_0> Duplex(IAsyncEnumerable<Model54.Model54_1> model);
    }

    [ProtoContract]
    public class Model54
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model54_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model54_1> Bars { get; } = new();

        [ProtoContract]
        public class Model54_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model54_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService55 : ITestService55
    {
        Task<Model55> ITestService55.BasicAsync(Model55 model) => throw new NotImplementedException();
        Model55 ITestService55.BasicSync(Model55 model) => throw new NotImplementedException();
        Task ITestService55.ClientStreaming(IAsyncEnumerable<Model55.Model55_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model55.Model55_0> ITestService55.Duplex(IAsyncEnumerable<Model55.Model55_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model55.Model55_0> ITestService55.ServerStreaming() => throw new NotImplementedException();
        Task ITestService55.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService55
    {
        Task VoidVoidAsync();

        Model55 BasicSync(Model55 model);

        Task<Model55> BasicAsync(Model55 model);

        IAsyncEnumerable<Model55.Model55_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model55.Model55_1> model);

        IAsyncEnumerable<Model55.Model55_0> Duplex(IAsyncEnumerable<Model55.Model55_1> model);
    }

    [ProtoContract]
    public class Model55
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model55_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model55_1> Bars { get; } = new();

        [ProtoContract]
        public class Model55_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model55_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService56 : ITestService56
    {
        Task<Model56> ITestService56.BasicAsync(Model56 model) => throw new NotImplementedException();
        Model56 ITestService56.BasicSync(Model56 model) => throw new NotImplementedException();
        Task ITestService56.ClientStreaming(IAsyncEnumerable<Model56.Model56_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model56.Model56_0> ITestService56.Duplex(IAsyncEnumerable<Model56.Model56_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model56.Model56_0> ITestService56.ServerStreaming() => throw new NotImplementedException();
        Task ITestService56.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService56
    {
        Task VoidVoidAsync();

        Model56 BasicSync(Model56 model);

        Task<Model56> BasicAsync(Model56 model);

        IAsyncEnumerable<Model56.Model56_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model56.Model56_1> model);

        IAsyncEnumerable<Model56.Model56_0> Duplex(IAsyncEnumerable<Model56.Model56_1> model);
    }

    [ProtoContract]
    public class Model56
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model56_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model56_1> Bars { get; } = new();

        [ProtoContract]
        public class Model56_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model56_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService57 : ITestService57
    {
        Task<Model57> ITestService57.BasicAsync(Model57 model) => throw new NotImplementedException();
        Model57 ITestService57.BasicSync(Model57 model) => throw new NotImplementedException();
        Task ITestService57.ClientStreaming(IAsyncEnumerable<Model57.Model57_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model57.Model57_0> ITestService57.Duplex(IAsyncEnumerable<Model57.Model57_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model57.Model57_0> ITestService57.ServerStreaming() => throw new NotImplementedException();
        Task ITestService57.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService57
    {
        Task VoidVoidAsync();

        Model57 BasicSync(Model57 model);

        Task<Model57> BasicAsync(Model57 model);

        IAsyncEnumerable<Model57.Model57_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model57.Model57_1> model);

        IAsyncEnumerable<Model57.Model57_0> Duplex(IAsyncEnumerable<Model57.Model57_1> model);
    }

    [ProtoContract]
    public class Model57
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model57_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model57_1> Bars { get; } = new();

        [ProtoContract]
        public class Model57_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model57_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService58 : ITestService58
    {
        Task<Model58> ITestService58.BasicAsync(Model58 model) => throw new NotImplementedException();
        Model58 ITestService58.BasicSync(Model58 model) => throw new NotImplementedException();
        Task ITestService58.ClientStreaming(IAsyncEnumerable<Model58.Model58_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model58.Model58_0> ITestService58.Duplex(IAsyncEnumerable<Model58.Model58_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model58.Model58_0> ITestService58.ServerStreaming() => throw new NotImplementedException();
        Task ITestService58.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService58
    {
        Task VoidVoidAsync();

        Model58 BasicSync(Model58 model);

        Task<Model58> BasicAsync(Model58 model);

        IAsyncEnumerable<Model58.Model58_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model58.Model58_1> model);

        IAsyncEnumerable<Model58.Model58_0> Duplex(IAsyncEnumerable<Model58.Model58_1> model);
    }

    [ProtoContract]
    public class Model58
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model58_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model58_1> Bars { get; } = new();

        [ProtoContract]
        public class Model58_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model58_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService59 : ITestService59
    {
        Task<Model59> ITestService59.BasicAsync(Model59 model) => throw new NotImplementedException();
        Model59 ITestService59.BasicSync(Model59 model) => throw new NotImplementedException();
        Task ITestService59.ClientStreaming(IAsyncEnumerable<Model59.Model59_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model59.Model59_0> ITestService59.Duplex(IAsyncEnumerable<Model59.Model59_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model59.Model59_0> ITestService59.ServerStreaming() => throw new NotImplementedException();
        Task ITestService59.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService59
    {
        Task VoidVoidAsync();

        Model59 BasicSync(Model59 model);

        Task<Model59> BasicAsync(Model59 model);

        IAsyncEnumerable<Model59.Model59_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model59.Model59_1> model);

        IAsyncEnumerable<Model59.Model59_0> Duplex(IAsyncEnumerable<Model59.Model59_1> model);
    }

    [ProtoContract]
    public class Model59
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model59_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model59_1> Bars { get; } = new();

        [ProtoContract]
        public class Model59_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model59_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService60 : ITestService60
    {
        Task<Model60> ITestService60.BasicAsync(Model60 model) => throw new NotImplementedException();
        Model60 ITestService60.BasicSync(Model60 model) => throw new NotImplementedException();
        Task ITestService60.ClientStreaming(IAsyncEnumerable<Model60.Model60_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model60.Model60_0> ITestService60.Duplex(IAsyncEnumerable<Model60.Model60_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model60.Model60_0> ITestService60.ServerStreaming() => throw new NotImplementedException();
        Task ITestService60.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService60
    {
        Task VoidVoidAsync();

        Model60 BasicSync(Model60 model);

        Task<Model60> BasicAsync(Model60 model);

        IAsyncEnumerable<Model60.Model60_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model60.Model60_1> model);

        IAsyncEnumerable<Model60.Model60_0> Duplex(IAsyncEnumerable<Model60.Model60_1> model);
    }

    [ProtoContract]
    public class Model60
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model60_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model60_1> Bars { get; } = new();

        [ProtoContract]
        public class Model60_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model60_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService61 : ITestService61
    {
        Task<Model61> ITestService61.BasicAsync(Model61 model) => throw new NotImplementedException();
        Model61 ITestService61.BasicSync(Model61 model) => throw new NotImplementedException();
        Task ITestService61.ClientStreaming(IAsyncEnumerable<Model61.Model61_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model61.Model61_0> ITestService61.Duplex(IAsyncEnumerable<Model61.Model61_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model61.Model61_0> ITestService61.ServerStreaming() => throw new NotImplementedException();
        Task ITestService61.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService61
    {
        Task VoidVoidAsync();

        Model61 BasicSync(Model61 model);

        Task<Model61> BasicAsync(Model61 model);

        IAsyncEnumerable<Model61.Model61_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model61.Model61_1> model);

        IAsyncEnumerable<Model61.Model61_0> Duplex(IAsyncEnumerable<Model61.Model61_1> model);
    }

    [ProtoContract]
    public class Model61
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model61_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model61_1> Bars { get; } = new();

        [ProtoContract]
        public class Model61_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model61_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService62 : ITestService62
    {
        Task<Model62> ITestService62.BasicAsync(Model62 model) => throw new NotImplementedException();
        Model62 ITestService62.BasicSync(Model62 model) => throw new NotImplementedException();
        Task ITestService62.ClientStreaming(IAsyncEnumerable<Model62.Model62_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model62.Model62_0> ITestService62.Duplex(IAsyncEnumerable<Model62.Model62_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model62.Model62_0> ITestService62.ServerStreaming() => throw new NotImplementedException();
        Task ITestService62.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService62
    {
        Task VoidVoidAsync();

        Model62 BasicSync(Model62 model);

        Task<Model62> BasicAsync(Model62 model);

        IAsyncEnumerable<Model62.Model62_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model62.Model62_1> model);

        IAsyncEnumerable<Model62.Model62_0> Duplex(IAsyncEnumerable<Model62.Model62_1> model);
    }

    [ProtoContract]
    public class Model62
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model62_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model62_1> Bars { get; } = new();

        [ProtoContract]
        public class Model62_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model62_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService63 : ITestService63
    {
        Task<Model63> ITestService63.BasicAsync(Model63 model) => throw new NotImplementedException();
        Model63 ITestService63.BasicSync(Model63 model) => throw new NotImplementedException();
        Task ITestService63.ClientStreaming(IAsyncEnumerable<Model63.Model63_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model63.Model63_0> ITestService63.Duplex(IAsyncEnumerable<Model63.Model63_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model63.Model63_0> ITestService63.ServerStreaming() => throw new NotImplementedException();
        Task ITestService63.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService63
    {
        Task VoidVoidAsync();

        Model63 BasicSync(Model63 model);

        Task<Model63> BasicAsync(Model63 model);

        IAsyncEnumerable<Model63.Model63_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model63.Model63_1> model);

        IAsyncEnumerable<Model63.Model63_0> Duplex(IAsyncEnumerable<Model63.Model63_1> model);
    }

    [ProtoContract]
    public class Model63
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model63_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model63_1> Bars { get; } = new();

        [ProtoContract]
        public class Model63_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model63_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService64 : ITestService64
    {
        Task<Model64> ITestService64.BasicAsync(Model64 model) => throw new NotImplementedException();
        Model64 ITestService64.BasicSync(Model64 model) => throw new NotImplementedException();
        Task ITestService64.ClientStreaming(IAsyncEnumerable<Model64.Model64_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model64.Model64_0> ITestService64.Duplex(IAsyncEnumerable<Model64.Model64_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model64.Model64_0> ITestService64.ServerStreaming() => throw new NotImplementedException();
        Task ITestService64.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService64
    {
        Task VoidVoidAsync();

        Model64 BasicSync(Model64 model);

        Task<Model64> BasicAsync(Model64 model);

        IAsyncEnumerable<Model64.Model64_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model64.Model64_1> model);

        IAsyncEnumerable<Model64.Model64_0> Duplex(IAsyncEnumerable<Model64.Model64_1> model);
    }

    [ProtoContract]
    public class Model64
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model64_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model64_1> Bars { get; } = new();

        [ProtoContract]
        public class Model64_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model64_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService65 : ITestService65
    {
        Task<Model65> ITestService65.BasicAsync(Model65 model) => throw new NotImplementedException();
        Model65 ITestService65.BasicSync(Model65 model) => throw new NotImplementedException();
        Task ITestService65.ClientStreaming(IAsyncEnumerable<Model65.Model65_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model65.Model65_0> ITestService65.Duplex(IAsyncEnumerable<Model65.Model65_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model65.Model65_0> ITestService65.ServerStreaming() => throw new NotImplementedException();
        Task ITestService65.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService65
    {
        Task VoidVoidAsync();

        Model65 BasicSync(Model65 model);

        Task<Model65> BasicAsync(Model65 model);

        IAsyncEnumerable<Model65.Model65_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model65.Model65_1> model);

        IAsyncEnumerable<Model65.Model65_0> Duplex(IAsyncEnumerable<Model65.Model65_1> model);
    }

    [ProtoContract]
    public class Model65
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model65_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model65_1> Bars { get; } = new();

        [ProtoContract]
        public class Model65_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model65_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService66 : ITestService66
    {
        Task<Model66> ITestService66.BasicAsync(Model66 model) => throw new NotImplementedException();
        Model66 ITestService66.BasicSync(Model66 model) => throw new NotImplementedException();
        Task ITestService66.ClientStreaming(IAsyncEnumerable<Model66.Model66_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model66.Model66_0> ITestService66.Duplex(IAsyncEnumerable<Model66.Model66_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model66.Model66_0> ITestService66.ServerStreaming() => throw new NotImplementedException();
        Task ITestService66.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService66
    {
        Task VoidVoidAsync();

        Model66 BasicSync(Model66 model);

        Task<Model66> BasicAsync(Model66 model);

        IAsyncEnumerable<Model66.Model66_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model66.Model66_1> model);

        IAsyncEnumerable<Model66.Model66_0> Duplex(IAsyncEnumerable<Model66.Model66_1> model);
    }

    [ProtoContract]
    public class Model66
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model66_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model66_1> Bars { get; } = new();

        [ProtoContract]
        public class Model66_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model66_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService67 : ITestService67
    {
        Task<Model67> ITestService67.BasicAsync(Model67 model) => throw new NotImplementedException();
        Model67 ITestService67.BasicSync(Model67 model) => throw new NotImplementedException();
        Task ITestService67.ClientStreaming(IAsyncEnumerable<Model67.Model67_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model67.Model67_0> ITestService67.Duplex(IAsyncEnumerable<Model67.Model67_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model67.Model67_0> ITestService67.ServerStreaming() => throw new NotImplementedException();
        Task ITestService67.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService67
    {
        Task VoidVoidAsync();

        Model67 BasicSync(Model67 model);

        Task<Model67> BasicAsync(Model67 model);

        IAsyncEnumerable<Model67.Model67_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model67.Model67_1> model);

        IAsyncEnumerable<Model67.Model67_0> Duplex(IAsyncEnumerable<Model67.Model67_1> model);
    }

    [ProtoContract]
    public class Model67
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model67_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model67_1> Bars { get; } = new();

        [ProtoContract]
        public class Model67_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model67_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService68 : ITestService68
    {
        Task<Model68> ITestService68.BasicAsync(Model68 model) => throw new NotImplementedException();
        Model68 ITestService68.BasicSync(Model68 model) => throw new NotImplementedException();
        Task ITestService68.ClientStreaming(IAsyncEnumerable<Model68.Model68_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model68.Model68_0> ITestService68.Duplex(IAsyncEnumerable<Model68.Model68_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model68.Model68_0> ITestService68.ServerStreaming() => throw new NotImplementedException();
        Task ITestService68.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService68
    {
        Task VoidVoidAsync();

        Model68 BasicSync(Model68 model);

        Task<Model68> BasicAsync(Model68 model);

        IAsyncEnumerable<Model68.Model68_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model68.Model68_1> model);

        IAsyncEnumerable<Model68.Model68_0> Duplex(IAsyncEnumerable<Model68.Model68_1> model);
    }

    [ProtoContract]
    public class Model68
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model68_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model68_1> Bars { get; } = new();

        [ProtoContract]
        public class Model68_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model68_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService69 : ITestService69
    {
        Task<Model69> ITestService69.BasicAsync(Model69 model) => throw new NotImplementedException();
        Model69 ITestService69.BasicSync(Model69 model) => throw new NotImplementedException();
        Task ITestService69.ClientStreaming(IAsyncEnumerable<Model69.Model69_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model69.Model69_0> ITestService69.Duplex(IAsyncEnumerable<Model69.Model69_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model69.Model69_0> ITestService69.ServerStreaming() => throw new NotImplementedException();
        Task ITestService69.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService69
    {
        Task VoidVoidAsync();

        Model69 BasicSync(Model69 model);

        Task<Model69> BasicAsync(Model69 model);

        IAsyncEnumerable<Model69.Model69_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model69.Model69_1> model);

        IAsyncEnumerable<Model69.Model69_0> Duplex(IAsyncEnumerable<Model69.Model69_1> model);
    }

    [ProtoContract]
    public class Model69
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model69_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model69_1> Bars { get; } = new();

        [ProtoContract]
        public class Model69_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model69_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService70 : ITestService70
    {
        Task<Model70> ITestService70.BasicAsync(Model70 model) => throw new NotImplementedException();
        Model70 ITestService70.BasicSync(Model70 model) => throw new NotImplementedException();
        Task ITestService70.ClientStreaming(IAsyncEnumerable<Model70.Model70_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model70.Model70_0> ITestService70.Duplex(IAsyncEnumerable<Model70.Model70_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model70.Model70_0> ITestService70.ServerStreaming() => throw new NotImplementedException();
        Task ITestService70.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService70
    {
        Task VoidVoidAsync();

        Model70 BasicSync(Model70 model);

        Task<Model70> BasicAsync(Model70 model);

        IAsyncEnumerable<Model70.Model70_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model70.Model70_1> model);

        IAsyncEnumerable<Model70.Model70_0> Duplex(IAsyncEnumerable<Model70.Model70_1> model);
    }

    [ProtoContract]
    public class Model70
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model70_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model70_1> Bars { get; } = new();

        [ProtoContract]
        public class Model70_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model70_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService71 : ITestService71
    {
        Task<Model71> ITestService71.BasicAsync(Model71 model) => throw new NotImplementedException();
        Model71 ITestService71.BasicSync(Model71 model) => throw new NotImplementedException();
        Task ITestService71.ClientStreaming(IAsyncEnumerable<Model71.Model71_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model71.Model71_0> ITestService71.Duplex(IAsyncEnumerable<Model71.Model71_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model71.Model71_0> ITestService71.ServerStreaming() => throw new NotImplementedException();
        Task ITestService71.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService71
    {
        Task VoidVoidAsync();

        Model71 BasicSync(Model71 model);

        Task<Model71> BasicAsync(Model71 model);

        IAsyncEnumerable<Model71.Model71_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model71.Model71_1> model);

        IAsyncEnumerable<Model71.Model71_0> Duplex(IAsyncEnumerable<Model71.Model71_1> model);
    }

    [ProtoContract]
    public class Model71
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model71_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model71_1> Bars { get; } = new();

        [ProtoContract]
        public class Model71_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model71_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService72 : ITestService72
    {
        Task<Model72> ITestService72.BasicAsync(Model72 model) => throw new NotImplementedException();
        Model72 ITestService72.BasicSync(Model72 model) => throw new NotImplementedException();
        Task ITestService72.ClientStreaming(IAsyncEnumerable<Model72.Model72_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model72.Model72_0> ITestService72.Duplex(IAsyncEnumerable<Model72.Model72_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model72.Model72_0> ITestService72.ServerStreaming() => throw new NotImplementedException();
        Task ITestService72.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService72
    {
        Task VoidVoidAsync();

        Model72 BasicSync(Model72 model);

        Task<Model72> BasicAsync(Model72 model);

        IAsyncEnumerable<Model72.Model72_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model72.Model72_1> model);

        IAsyncEnumerable<Model72.Model72_0> Duplex(IAsyncEnumerable<Model72.Model72_1> model);
    }

    [ProtoContract]
    public class Model72
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model72_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model72_1> Bars { get; } = new();

        [ProtoContract]
        public class Model72_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model72_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService73 : ITestService73
    {
        Task<Model73> ITestService73.BasicAsync(Model73 model) => throw new NotImplementedException();
        Model73 ITestService73.BasicSync(Model73 model) => throw new NotImplementedException();
        Task ITestService73.ClientStreaming(IAsyncEnumerable<Model73.Model73_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model73.Model73_0> ITestService73.Duplex(IAsyncEnumerable<Model73.Model73_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model73.Model73_0> ITestService73.ServerStreaming() => throw new NotImplementedException();
        Task ITestService73.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService73
    {
        Task VoidVoidAsync();

        Model73 BasicSync(Model73 model);

        Task<Model73> BasicAsync(Model73 model);

        IAsyncEnumerable<Model73.Model73_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model73.Model73_1> model);

        IAsyncEnumerable<Model73.Model73_0> Duplex(IAsyncEnumerable<Model73.Model73_1> model);
    }

    [ProtoContract]
    public class Model73
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model73_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model73_1> Bars { get; } = new();

        [ProtoContract]
        public class Model73_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model73_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService74 : ITestService74
    {
        Task<Model74> ITestService74.BasicAsync(Model74 model) => throw new NotImplementedException();
        Model74 ITestService74.BasicSync(Model74 model) => throw new NotImplementedException();
        Task ITestService74.ClientStreaming(IAsyncEnumerable<Model74.Model74_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model74.Model74_0> ITestService74.Duplex(IAsyncEnumerable<Model74.Model74_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model74.Model74_0> ITestService74.ServerStreaming() => throw new NotImplementedException();
        Task ITestService74.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService74
    {
        Task VoidVoidAsync();

        Model74 BasicSync(Model74 model);

        Task<Model74> BasicAsync(Model74 model);

        IAsyncEnumerable<Model74.Model74_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model74.Model74_1> model);

        IAsyncEnumerable<Model74.Model74_0> Duplex(IAsyncEnumerable<Model74.Model74_1> model);
    }

    [ProtoContract]
    public class Model74
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model74_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model74_1> Bars { get; } = new();

        [ProtoContract]
        public class Model74_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model74_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService75 : ITestService75
    {
        Task<Model75> ITestService75.BasicAsync(Model75 model) => throw new NotImplementedException();
        Model75 ITestService75.BasicSync(Model75 model) => throw new NotImplementedException();
        Task ITestService75.ClientStreaming(IAsyncEnumerable<Model75.Model75_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model75.Model75_0> ITestService75.Duplex(IAsyncEnumerable<Model75.Model75_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model75.Model75_0> ITestService75.ServerStreaming() => throw new NotImplementedException();
        Task ITestService75.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService75
    {
        Task VoidVoidAsync();

        Model75 BasicSync(Model75 model);

        Task<Model75> BasicAsync(Model75 model);

        IAsyncEnumerable<Model75.Model75_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model75.Model75_1> model);

        IAsyncEnumerable<Model75.Model75_0> Duplex(IAsyncEnumerable<Model75.Model75_1> model);
    }

    [ProtoContract]
    public class Model75
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model75_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model75_1> Bars { get; } = new();

        [ProtoContract]
        public class Model75_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model75_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService76 : ITestService76
    {
        Task<Model76> ITestService76.BasicAsync(Model76 model) => throw new NotImplementedException();
        Model76 ITestService76.BasicSync(Model76 model) => throw new NotImplementedException();
        Task ITestService76.ClientStreaming(IAsyncEnumerable<Model76.Model76_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model76.Model76_0> ITestService76.Duplex(IAsyncEnumerable<Model76.Model76_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model76.Model76_0> ITestService76.ServerStreaming() => throw new NotImplementedException();
        Task ITestService76.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService76
    {
        Task VoidVoidAsync();

        Model76 BasicSync(Model76 model);

        Task<Model76> BasicAsync(Model76 model);

        IAsyncEnumerable<Model76.Model76_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model76.Model76_1> model);

        IAsyncEnumerable<Model76.Model76_0> Duplex(IAsyncEnumerable<Model76.Model76_1> model);
    }

    [ProtoContract]
    public class Model76
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model76_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model76_1> Bars { get; } = new();

        [ProtoContract]
        public class Model76_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model76_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService77 : ITestService77
    {
        Task<Model77> ITestService77.BasicAsync(Model77 model) => throw new NotImplementedException();
        Model77 ITestService77.BasicSync(Model77 model) => throw new NotImplementedException();
        Task ITestService77.ClientStreaming(IAsyncEnumerable<Model77.Model77_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model77.Model77_0> ITestService77.Duplex(IAsyncEnumerable<Model77.Model77_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model77.Model77_0> ITestService77.ServerStreaming() => throw new NotImplementedException();
        Task ITestService77.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService77
    {
        Task VoidVoidAsync();

        Model77 BasicSync(Model77 model);

        Task<Model77> BasicAsync(Model77 model);

        IAsyncEnumerable<Model77.Model77_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model77.Model77_1> model);

        IAsyncEnumerable<Model77.Model77_0> Duplex(IAsyncEnumerable<Model77.Model77_1> model);
    }

    [ProtoContract]
    public class Model77
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model77_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model77_1> Bars { get; } = new();

        [ProtoContract]
        public class Model77_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model77_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService78 : ITestService78
    {
        Task<Model78> ITestService78.BasicAsync(Model78 model) => throw new NotImplementedException();
        Model78 ITestService78.BasicSync(Model78 model) => throw new NotImplementedException();
        Task ITestService78.ClientStreaming(IAsyncEnumerable<Model78.Model78_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model78.Model78_0> ITestService78.Duplex(IAsyncEnumerable<Model78.Model78_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model78.Model78_0> ITestService78.ServerStreaming() => throw new NotImplementedException();
        Task ITestService78.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService78
    {
        Task VoidVoidAsync();

        Model78 BasicSync(Model78 model);

        Task<Model78> BasicAsync(Model78 model);

        IAsyncEnumerable<Model78.Model78_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model78.Model78_1> model);

        IAsyncEnumerable<Model78.Model78_0> Duplex(IAsyncEnumerable<Model78.Model78_1> model);
    }

    [ProtoContract]
    public class Model78
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model78_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model78_1> Bars { get; } = new();

        [ProtoContract]
        public class Model78_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model78_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService79 : ITestService79
    {
        Task<Model79> ITestService79.BasicAsync(Model79 model) => throw new NotImplementedException();
        Model79 ITestService79.BasicSync(Model79 model) => throw new NotImplementedException();
        Task ITestService79.ClientStreaming(IAsyncEnumerable<Model79.Model79_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model79.Model79_0> ITestService79.Duplex(IAsyncEnumerable<Model79.Model79_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model79.Model79_0> ITestService79.ServerStreaming() => throw new NotImplementedException();
        Task ITestService79.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService79
    {
        Task VoidVoidAsync();

        Model79 BasicSync(Model79 model);

        Task<Model79> BasicAsync(Model79 model);

        IAsyncEnumerable<Model79.Model79_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model79.Model79_1> model);

        IAsyncEnumerable<Model79.Model79_0> Duplex(IAsyncEnumerable<Model79.Model79_1> model);
    }

    [ProtoContract]
    public class Model79
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model79_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model79_1> Bars { get; } = new();

        [ProtoContract]
        public class Model79_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model79_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService80 : ITestService80
    {
        Task<Model80> ITestService80.BasicAsync(Model80 model) => throw new NotImplementedException();
        Model80 ITestService80.BasicSync(Model80 model) => throw new NotImplementedException();
        Task ITestService80.ClientStreaming(IAsyncEnumerable<Model80.Model80_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model80.Model80_0> ITestService80.Duplex(IAsyncEnumerable<Model80.Model80_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model80.Model80_0> ITestService80.ServerStreaming() => throw new NotImplementedException();
        Task ITestService80.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService80
    {
        Task VoidVoidAsync();

        Model80 BasicSync(Model80 model);

        Task<Model80> BasicAsync(Model80 model);

        IAsyncEnumerable<Model80.Model80_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model80.Model80_1> model);

        IAsyncEnumerable<Model80.Model80_0> Duplex(IAsyncEnumerable<Model80.Model80_1> model);
    }

    [ProtoContract]
    public class Model80
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model80_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model80_1> Bars { get; } = new();

        [ProtoContract]
        public class Model80_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model80_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService81 : ITestService81
    {
        Task<Model81> ITestService81.BasicAsync(Model81 model) => throw new NotImplementedException();
        Model81 ITestService81.BasicSync(Model81 model) => throw new NotImplementedException();
        Task ITestService81.ClientStreaming(IAsyncEnumerable<Model81.Model81_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model81.Model81_0> ITestService81.Duplex(IAsyncEnumerable<Model81.Model81_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model81.Model81_0> ITestService81.ServerStreaming() => throw new NotImplementedException();
        Task ITestService81.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService81
    {
        Task VoidVoidAsync();

        Model81 BasicSync(Model81 model);

        Task<Model81> BasicAsync(Model81 model);

        IAsyncEnumerable<Model81.Model81_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model81.Model81_1> model);

        IAsyncEnumerable<Model81.Model81_0> Duplex(IAsyncEnumerable<Model81.Model81_1> model);
    }

    [ProtoContract]
    public class Model81
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model81_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model81_1> Bars { get; } = new();

        [ProtoContract]
        public class Model81_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model81_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService82 : ITestService82
    {
        Task<Model82> ITestService82.BasicAsync(Model82 model) => throw new NotImplementedException();
        Model82 ITestService82.BasicSync(Model82 model) => throw new NotImplementedException();
        Task ITestService82.ClientStreaming(IAsyncEnumerable<Model82.Model82_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model82.Model82_0> ITestService82.Duplex(IAsyncEnumerable<Model82.Model82_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model82.Model82_0> ITestService82.ServerStreaming() => throw new NotImplementedException();
        Task ITestService82.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService82
    {
        Task VoidVoidAsync();

        Model82 BasicSync(Model82 model);

        Task<Model82> BasicAsync(Model82 model);

        IAsyncEnumerable<Model82.Model82_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model82.Model82_1> model);

        IAsyncEnumerable<Model82.Model82_0> Duplex(IAsyncEnumerable<Model82.Model82_1> model);
    }

    [ProtoContract]
    public class Model82
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model82_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model82_1> Bars { get; } = new();

        [ProtoContract]
        public class Model82_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model82_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService83 : ITestService83
    {
        Task<Model83> ITestService83.BasicAsync(Model83 model) => throw new NotImplementedException();
        Model83 ITestService83.BasicSync(Model83 model) => throw new NotImplementedException();
        Task ITestService83.ClientStreaming(IAsyncEnumerable<Model83.Model83_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model83.Model83_0> ITestService83.Duplex(IAsyncEnumerable<Model83.Model83_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model83.Model83_0> ITestService83.ServerStreaming() => throw new NotImplementedException();
        Task ITestService83.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService83
    {
        Task VoidVoidAsync();

        Model83 BasicSync(Model83 model);

        Task<Model83> BasicAsync(Model83 model);

        IAsyncEnumerable<Model83.Model83_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model83.Model83_1> model);

        IAsyncEnumerable<Model83.Model83_0> Duplex(IAsyncEnumerable<Model83.Model83_1> model);
    }

    [ProtoContract]
    public class Model83
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model83_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model83_1> Bars { get; } = new();

        [ProtoContract]
        public class Model83_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model83_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService84 : ITestService84
    {
        Task<Model84> ITestService84.BasicAsync(Model84 model) => throw new NotImplementedException();
        Model84 ITestService84.BasicSync(Model84 model) => throw new NotImplementedException();
        Task ITestService84.ClientStreaming(IAsyncEnumerable<Model84.Model84_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model84.Model84_0> ITestService84.Duplex(IAsyncEnumerable<Model84.Model84_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model84.Model84_0> ITestService84.ServerStreaming() => throw new NotImplementedException();
        Task ITestService84.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService84
    {
        Task VoidVoidAsync();

        Model84 BasicSync(Model84 model);

        Task<Model84> BasicAsync(Model84 model);

        IAsyncEnumerable<Model84.Model84_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model84.Model84_1> model);

        IAsyncEnumerable<Model84.Model84_0> Duplex(IAsyncEnumerable<Model84.Model84_1> model);
    }

    [ProtoContract]
    public class Model84
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model84_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model84_1> Bars { get; } = new();

        [ProtoContract]
        public class Model84_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model84_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService85 : ITestService85
    {
        Task<Model85> ITestService85.BasicAsync(Model85 model) => throw new NotImplementedException();
        Model85 ITestService85.BasicSync(Model85 model) => throw new NotImplementedException();
        Task ITestService85.ClientStreaming(IAsyncEnumerable<Model85.Model85_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model85.Model85_0> ITestService85.Duplex(IAsyncEnumerable<Model85.Model85_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model85.Model85_0> ITestService85.ServerStreaming() => throw new NotImplementedException();
        Task ITestService85.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService85
    {
        Task VoidVoidAsync();

        Model85 BasicSync(Model85 model);

        Task<Model85> BasicAsync(Model85 model);

        IAsyncEnumerable<Model85.Model85_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model85.Model85_1> model);

        IAsyncEnumerable<Model85.Model85_0> Duplex(IAsyncEnumerable<Model85.Model85_1> model);
    }

    [ProtoContract]
    public class Model85
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model85_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model85_1> Bars { get; } = new();

        [ProtoContract]
        public class Model85_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model85_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService86 : ITestService86
    {
        Task<Model86> ITestService86.BasicAsync(Model86 model) => throw new NotImplementedException();
        Model86 ITestService86.BasicSync(Model86 model) => throw new NotImplementedException();
        Task ITestService86.ClientStreaming(IAsyncEnumerable<Model86.Model86_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model86.Model86_0> ITestService86.Duplex(IAsyncEnumerable<Model86.Model86_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model86.Model86_0> ITestService86.ServerStreaming() => throw new NotImplementedException();
        Task ITestService86.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService86
    {
        Task VoidVoidAsync();

        Model86 BasicSync(Model86 model);

        Task<Model86> BasicAsync(Model86 model);

        IAsyncEnumerable<Model86.Model86_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model86.Model86_1> model);

        IAsyncEnumerable<Model86.Model86_0> Duplex(IAsyncEnumerable<Model86.Model86_1> model);
    }

    [ProtoContract]
    public class Model86
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model86_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model86_1> Bars { get; } = new();

        [ProtoContract]
        public class Model86_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model86_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService87 : ITestService87
    {
        Task<Model87> ITestService87.BasicAsync(Model87 model) => throw new NotImplementedException();
        Model87 ITestService87.BasicSync(Model87 model) => throw new NotImplementedException();
        Task ITestService87.ClientStreaming(IAsyncEnumerable<Model87.Model87_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model87.Model87_0> ITestService87.Duplex(IAsyncEnumerable<Model87.Model87_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model87.Model87_0> ITestService87.ServerStreaming() => throw new NotImplementedException();
        Task ITestService87.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService87
    {
        Task VoidVoidAsync();

        Model87 BasicSync(Model87 model);

        Task<Model87> BasicAsync(Model87 model);

        IAsyncEnumerable<Model87.Model87_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model87.Model87_1> model);

        IAsyncEnumerable<Model87.Model87_0> Duplex(IAsyncEnumerable<Model87.Model87_1> model);
    }

    [ProtoContract]
    public class Model87
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model87_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model87_1> Bars { get; } = new();

        [ProtoContract]
        public class Model87_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model87_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService88 : ITestService88
    {
        Task<Model88> ITestService88.BasicAsync(Model88 model) => throw new NotImplementedException();
        Model88 ITestService88.BasicSync(Model88 model) => throw new NotImplementedException();
        Task ITestService88.ClientStreaming(IAsyncEnumerable<Model88.Model88_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model88.Model88_0> ITestService88.Duplex(IAsyncEnumerable<Model88.Model88_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model88.Model88_0> ITestService88.ServerStreaming() => throw new NotImplementedException();
        Task ITestService88.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService88
    {
        Task VoidVoidAsync();

        Model88 BasicSync(Model88 model);

        Task<Model88> BasicAsync(Model88 model);

        IAsyncEnumerable<Model88.Model88_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model88.Model88_1> model);

        IAsyncEnumerable<Model88.Model88_0> Duplex(IAsyncEnumerable<Model88.Model88_1> model);
    }

    [ProtoContract]
    public class Model88
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model88_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model88_1> Bars { get; } = new();

        [ProtoContract]
        public class Model88_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model88_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService89 : ITestService89
    {
        Task<Model89> ITestService89.BasicAsync(Model89 model) => throw new NotImplementedException();
        Model89 ITestService89.BasicSync(Model89 model) => throw new NotImplementedException();
        Task ITestService89.ClientStreaming(IAsyncEnumerable<Model89.Model89_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model89.Model89_0> ITestService89.Duplex(IAsyncEnumerable<Model89.Model89_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model89.Model89_0> ITestService89.ServerStreaming() => throw new NotImplementedException();
        Task ITestService89.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService89
    {
        Task VoidVoidAsync();

        Model89 BasicSync(Model89 model);

        Task<Model89> BasicAsync(Model89 model);

        IAsyncEnumerable<Model89.Model89_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model89.Model89_1> model);

        IAsyncEnumerable<Model89.Model89_0> Duplex(IAsyncEnumerable<Model89.Model89_1> model);
    }

    [ProtoContract]
    public class Model89
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model89_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model89_1> Bars { get; } = new();

        [ProtoContract]
        public class Model89_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model89_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService90 : ITestService90
    {
        Task<Model90> ITestService90.BasicAsync(Model90 model) => throw new NotImplementedException();
        Model90 ITestService90.BasicSync(Model90 model) => throw new NotImplementedException();
        Task ITestService90.ClientStreaming(IAsyncEnumerable<Model90.Model90_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model90.Model90_0> ITestService90.Duplex(IAsyncEnumerable<Model90.Model90_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model90.Model90_0> ITestService90.ServerStreaming() => throw new NotImplementedException();
        Task ITestService90.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService90
    {
        Task VoidVoidAsync();

        Model90 BasicSync(Model90 model);

        Task<Model90> BasicAsync(Model90 model);

        IAsyncEnumerable<Model90.Model90_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model90.Model90_1> model);

        IAsyncEnumerable<Model90.Model90_0> Duplex(IAsyncEnumerable<Model90.Model90_1> model);
    }

    [ProtoContract]
    public class Model90
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model90_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model90_1> Bars { get; } = new();

        [ProtoContract]
        public class Model90_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model90_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService91 : ITestService91
    {
        Task<Model91> ITestService91.BasicAsync(Model91 model) => throw new NotImplementedException();
        Model91 ITestService91.BasicSync(Model91 model) => throw new NotImplementedException();
        Task ITestService91.ClientStreaming(IAsyncEnumerable<Model91.Model91_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model91.Model91_0> ITestService91.Duplex(IAsyncEnumerable<Model91.Model91_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model91.Model91_0> ITestService91.ServerStreaming() => throw new NotImplementedException();
        Task ITestService91.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService91
    {
        Task VoidVoidAsync();

        Model91 BasicSync(Model91 model);

        Task<Model91> BasicAsync(Model91 model);

        IAsyncEnumerable<Model91.Model91_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model91.Model91_1> model);

        IAsyncEnumerable<Model91.Model91_0> Duplex(IAsyncEnumerable<Model91.Model91_1> model);
    }

    [ProtoContract]
    public class Model91
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model91_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model91_1> Bars { get; } = new();

        [ProtoContract]
        public class Model91_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model91_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService92 : ITestService92
    {
        Task<Model92> ITestService92.BasicAsync(Model92 model) => throw new NotImplementedException();
        Model92 ITestService92.BasicSync(Model92 model) => throw new NotImplementedException();
        Task ITestService92.ClientStreaming(IAsyncEnumerable<Model92.Model92_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model92.Model92_0> ITestService92.Duplex(IAsyncEnumerable<Model92.Model92_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model92.Model92_0> ITestService92.ServerStreaming() => throw new NotImplementedException();
        Task ITestService92.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService92
    {
        Task VoidVoidAsync();

        Model92 BasicSync(Model92 model);

        Task<Model92> BasicAsync(Model92 model);

        IAsyncEnumerable<Model92.Model92_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model92.Model92_1> model);

        IAsyncEnumerable<Model92.Model92_0> Duplex(IAsyncEnumerable<Model92.Model92_1> model);
    }

    [ProtoContract]
    public class Model92
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model92_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model92_1> Bars { get; } = new();

        [ProtoContract]
        public class Model92_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model92_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService93 : ITestService93
    {
        Task<Model93> ITestService93.BasicAsync(Model93 model) => throw new NotImplementedException();
        Model93 ITestService93.BasicSync(Model93 model) => throw new NotImplementedException();
        Task ITestService93.ClientStreaming(IAsyncEnumerable<Model93.Model93_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model93.Model93_0> ITestService93.Duplex(IAsyncEnumerable<Model93.Model93_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model93.Model93_0> ITestService93.ServerStreaming() => throw new NotImplementedException();
        Task ITestService93.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService93
    {
        Task VoidVoidAsync();

        Model93 BasicSync(Model93 model);

        Task<Model93> BasicAsync(Model93 model);

        IAsyncEnumerable<Model93.Model93_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model93.Model93_1> model);

        IAsyncEnumerable<Model93.Model93_0> Duplex(IAsyncEnumerable<Model93.Model93_1> model);
    }

    [ProtoContract]
    public class Model93
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model93_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model93_1> Bars { get; } = new();

        [ProtoContract]
        public class Model93_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model93_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService94 : ITestService94
    {
        Task<Model94> ITestService94.BasicAsync(Model94 model) => throw new NotImplementedException();
        Model94 ITestService94.BasicSync(Model94 model) => throw new NotImplementedException();
        Task ITestService94.ClientStreaming(IAsyncEnumerable<Model94.Model94_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model94.Model94_0> ITestService94.Duplex(IAsyncEnumerable<Model94.Model94_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model94.Model94_0> ITestService94.ServerStreaming() => throw new NotImplementedException();
        Task ITestService94.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService94
    {
        Task VoidVoidAsync();

        Model94 BasicSync(Model94 model);

        Task<Model94> BasicAsync(Model94 model);

        IAsyncEnumerable<Model94.Model94_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model94.Model94_1> model);

        IAsyncEnumerable<Model94.Model94_0> Duplex(IAsyncEnumerable<Model94.Model94_1> model);
    }

    [ProtoContract]
    public class Model94
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model94_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model94_1> Bars { get; } = new();

        [ProtoContract]
        public class Model94_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model94_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService95 : ITestService95
    {
        Task<Model95> ITestService95.BasicAsync(Model95 model) => throw new NotImplementedException();
        Model95 ITestService95.BasicSync(Model95 model) => throw new NotImplementedException();
        Task ITestService95.ClientStreaming(IAsyncEnumerable<Model95.Model95_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model95.Model95_0> ITestService95.Duplex(IAsyncEnumerable<Model95.Model95_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model95.Model95_0> ITestService95.ServerStreaming() => throw new NotImplementedException();
        Task ITestService95.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService95
    {
        Task VoidVoidAsync();

        Model95 BasicSync(Model95 model);

        Task<Model95> BasicAsync(Model95 model);

        IAsyncEnumerable<Model95.Model95_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model95.Model95_1> model);

        IAsyncEnumerable<Model95.Model95_0> Duplex(IAsyncEnumerable<Model95.Model95_1> model);
    }

    [ProtoContract]
    public class Model95
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model95_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model95_1> Bars { get; } = new();

        [ProtoContract]
        public class Model95_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model95_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService96 : ITestService96
    {
        Task<Model96> ITestService96.BasicAsync(Model96 model) => throw new NotImplementedException();
        Model96 ITestService96.BasicSync(Model96 model) => throw new NotImplementedException();
        Task ITestService96.ClientStreaming(IAsyncEnumerable<Model96.Model96_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model96.Model96_0> ITestService96.Duplex(IAsyncEnumerable<Model96.Model96_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model96.Model96_0> ITestService96.ServerStreaming() => throw new NotImplementedException();
        Task ITestService96.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService96
    {
        Task VoidVoidAsync();

        Model96 BasicSync(Model96 model);

        Task<Model96> BasicAsync(Model96 model);

        IAsyncEnumerable<Model96.Model96_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model96.Model96_1> model);

        IAsyncEnumerable<Model96.Model96_0> Duplex(IAsyncEnumerable<Model96.Model96_1> model);
    }

    [ProtoContract]
    public class Model96
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model96_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model96_1> Bars { get; } = new();

        [ProtoContract]
        public class Model96_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model96_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService97 : ITestService97
    {
        Task<Model97> ITestService97.BasicAsync(Model97 model) => throw new NotImplementedException();
        Model97 ITestService97.BasicSync(Model97 model) => throw new NotImplementedException();
        Task ITestService97.ClientStreaming(IAsyncEnumerable<Model97.Model97_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model97.Model97_0> ITestService97.Duplex(IAsyncEnumerable<Model97.Model97_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model97.Model97_0> ITestService97.ServerStreaming() => throw new NotImplementedException();
        Task ITestService97.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService97
    {
        Task VoidVoidAsync();

        Model97 BasicSync(Model97 model);

        Task<Model97> BasicAsync(Model97 model);

        IAsyncEnumerable<Model97.Model97_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model97.Model97_1> model);

        IAsyncEnumerable<Model97.Model97_0> Duplex(IAsyncEnumerable<Model97.Model97_1> model);
    }

    [ProtoContract]
    public class Model97
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model97_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model97_1> Bars { get; } = new();

        [ProtoContract]
        public class Model97_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model97_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService98 : ITestService98
    {
        Task<Model98> ITestService98.BasicAsync(Model98 model) => throw new NotImplementedException();
        Model98 ITestService98.BasicSync(Model98 model) => throw new NotImplementedException();
        Task ITestService98.ClientStreaming(IAsyncEnumerable<Model98.Model98_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model98.Model98_0> ITestService98.Duplex(IAsyncEnumerable<Model98.Model98_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model98.Model98_0> ITestService98.ServerStreaming() => throw new NotImplementedException();
        Task ITestService98.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService98
    {
        Task VoidVoidAsync();

        Model98 BasicSync(Model98 model);

        Task<Model98> BasicAsync(Model98 model);

        IAsyncEnumerable<Model98.Model98_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model98.Model98_1> model);

        IAsyncEnumerable<Model98.Model98_0> Duplex(IAsyncEnumerable<Model98.Model98_1> model);
    }

    [ProtoContract]
    public class Model98
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model98_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model98_1> Bars { get; } = new();

        [ProtoContract]
        public class Model98_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model98_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    public class TestService99 : ITestService99
    {
        Task<Model99> ITestService99.BasicAsync(Model99 model) => throw new NotImplementedException();
        Model99 ITestService99.BasicSync(Model99 model) => throw new NotImplementedException();
        Task ITestService99.ClientStreaming(IAsyncEnumerable<Model99.Model99_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model99.Model99_0> ITestService99.Duplex(IAsyncEnumerable<Model99.Model99_1> model) => throw new NotImplementedException();
        IAsyncEnumerable<Model99.Model99_0> ITestService99.ServerStreaming() => throw new NotImplementedException();
        Task ITestService99.VoidVoidAsync() => throw new NotImplementedException();
    }

    [ServiceContract]
    public interface ITestService99
    {
        Task VoidVoidAsync();

        Model99 BasicSync(Model99 model);

        Task<Model99> BasicAsync(Model99 model);

        IAsyncEnumerable<Model99.Model99_0> ServerStreaming();

        Task ClientStreaming(IAsyncEnumerable<Model99.Model99_1> model);

        IAsyncEnumerable<Model99.Model99_0> Duplex(IAsyncEnumerable<Model99.Model99_1> model);
    }

    [ProtoContract]
    public class Model99
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string? Name { get; set; }
        [ProtoMember(3)]
        public DateTime? When { get; set; }
        [ProtoMember(4)]
        public List<Model99_0> Foos { get; } = new();
        [ProtoMember(5)]
        public List<Model99_1> Bars { get; } = new();

        [ProtoContract]
        public class Model99_0
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
        [ProtoContract]
        public class Model99_1
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string? Name { get; set; }
            [ProtoMember(3)]
            public DateTime? When { get; set; }
        }
    }
    #endregion
}
#endif