using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <typeparam name="TService">The service type to generate schema for.</typeparam>
        /// <remarks>This API is considered experimental and may change slightly</remarks>
        public string GetSchema<TService>()
            => GetSchema(typeof(TService));

        /// <summary>
        /// Get the .proto schema associated with a service contract
        /// </summary>
        /// <param name="contractType">The service type to generate schema for.</param>
        /// <remarks>This API is considered experimental and may change slightly.
        /// ATTENTION! although the 'GetSchema(params Type[] contractTypes)' covers also a case of 'GetSchema(Type contractType)',
        /// this method need to remain for backward compatibility for client which will get this updated version, without recompilation.
        /// Thus, this method mustn't be deleted.</remarks>
        public string GetSchema(Type contractType)
            => GetSchema([contractType]);

        /// <summary>
        /// Get the .proto schema associated with multiple service contracts
        /// </summary>
        /// <param name="contractTypes">Array (or params syntax) of service types to generate schema for.</param>
        /// <remarks>This API is considered experimental and may change slightly
        /// All types will be generated into single schema.
        /// All the shared classes the services use will be generated only once for all of them.</remarks>
        public string GetSchema(params Type[] contractTypes)
        {
            string globalPackage = "";
            List<Service> services = new List<Service>();  
            var binderConfiguration = BinderConfiguration ?? BinderConfiguration.Default;
            var binder = binderConfiguration.Binder;
            foreach (var contractType in contractTypes)
            {
                if (!binder.IsServiceContract(contractType, out var name))
                {
                    throw new ArgumentException($"Type '{contractType.Name}' is not a service contract",
                        nameof(contractTypes));
                }

                name = ServiceBinder.GetNameParts(name, contractType, out var package);
                // currently we allow only services from same package, to be output to single proto file
                if (!string.IsNullOrEmpty(globalPackage)
                    && package != globalPackage)
                {
                    throw new ArgumentException(
                        $"All services must be of the same package! '{contractType.Name}' is from package '{package}' while previous package: {globalPackage}",
                        nameof(contractTypes));
                }
                globalPackage = package;
                
                var service = new Service
                {
                    Name = name
                };
            
                var ops = GetMethodsRecursively(binder, contractType);
                foreach (var method in ops)
                {
                    if (method.DeclaringType == typeof(object))
                    {
                        /* skip */
                    }
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
                services.Add(service);
            }

            var options = new SchemaGenerationOptions
            {
                Syntax = ProtoSyntax,
                Package = globalPackage,
            };
            options.Services.AddRange(services);

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

        private static MethodInfo[] GetMethodsRecursively(ServiceBinder serviceBinder, Type contractType)
        {
            var includingInheritedInterfaces = ContractOperation.ExpandWithInterfacesMarkedAsSubService(serviceBinder, contractType);

            var inheritedMethods = includingInheritedInterfaces
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .ToArray();
            
            return inheritedMethods;
        }
    }
}
