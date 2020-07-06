using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using ProtoBuf.Meta;
using System;
using System.Reflection;

namespace ProtoBuf.Grpc.Reflection
{
    /// <summary>
    /// Allows creation of .proto schemas from service contracts
    /// </summary>
    public sealed class SchemaGenerator
    {
        /// <summary>
        /// Gets or sets the syntax version
        /// </summary>
        public ProtoSyntax ProtoSyntax { get; set; } = ProtoSyntax.Proto3;

        /// <summary>
        /// Gets or sets the binder configuration (the default configuration is used if omitted)
        /// </summary>
        public BinderConfiguration? BinderConfiguration { get; set; }

        /// <summary>
        /// Get the .proto schema associated with a service contract
        /// </summary>
        /// <remarks>This API is considered experimental and may change slightly</remarks>
        public string GetSchema<TService>()
            => GetSchema(typeof(TService));

        /// <summary>
        /// Get the .proto schema associated with a service contract
        /// </summary>
        /// <remarks>This API is considered experimental and may change slightly</remarks>
        public string GetSchema(Type contractType)
        {
            var binderConfiguration = BinderConfiguration ?? BinderConfiguration.Default;
            var binder = binderConfiguration.Binder;
            if (!binder.IsServiceContract(contractType, out var name))
            {
                throw new ArgumentException($"Type '{contractType.Name}' is not a service contract", nameof(contractType));
            }

            name = ServiceBinder.GetNameParts(name, contractType, out var package);
            var service = new Service
            {
                Name = name
            };
            var ops = contractType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in ops)
            {
                if (method.DeclaringType == typeof(object))
                { /* skip */ }
                else if (ContractOperation.TryIdentifySignature(method, binderConfiguration, out var op, null))
                {
                    service.Methods.Add(
                        new ServiceMethod
                        {
                            Name = op.Name,
                            InputType = ApplySubstitutes(op.From),
                            OutputType = ApplySubstitutes(op.To),
                            ClientStreaming = op.MethodType switch
                            {
                                MethodType.ClientStreaming => true,
                                MethodType.DuplexStreaming => true,
                                _ => false,
                            },
                            ServerStreaming = op.MethodType switch
                            {
                                MethodType.ServerStreaming => true,
                                MethodType.DuplexStreaming => true,
                                _ => false,
                            },
                        }
                    );
                }
            }
            service.Methods.Sort((x, y) => string.Compare(x.Name, y.Name)); // make it predictable
            var options = new SchemaGenerationOptions
            {
                Syntax = ProtoSyntax,
                Package = package,
                Services =
                {
                    service
                }
            };

            var model = binderConfiguration.MarshallerCache.TryGetFactory<ProtoBufMarshallerFactory>()?.Model ?? RuntimeTypeModel.Default;
            return model.GetSchema(options);

            static Type ApplySubstitutes(Type type)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                if (type == typeof(Empty)) return typeof(WellKnownTypes.Empty);
#pragma warning restore CS0618 // Type or member is obsolete
                if (type == typeof(DateTime)) return typeof(WellKnownTypes.Timestamp);
                if (type == typeof(TimeSpan)) return typeof(WellKnownTypes.Duration);
                return type;
            }
        }
    }
}
