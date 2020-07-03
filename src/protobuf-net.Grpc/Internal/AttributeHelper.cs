using System;
using System.Collections.Generic;
using System.Reflection;

namespace ProtoBuf.Grpc.Internal
{
    internal readonly struct AttributeHelper
    {
        private readonly IList<CustomAttributeData>? _attribs;
        public static AttributeHelper For(Type type, bool inherit)
        {
            IList<CustomAttributeData>? attribs = null;
            while (type is object)
            {
                Append(ref attribs, type.GetCustomAttributesData());
                type = (inherit ? type.BaseType : null)!;
            }
            
            return new AttributeHelper(attribs);
        }

        private static void Append(ref IList<CustomAttributeData>? attribs, IList<CustomAttributeData> local)
        {
            if (local is null || local.Count == 0) return;
            
            if (attribs is null)
            {
                attribs = local;
            }
            else if (attribs is List<CustomAttributeData> hardList)
            {
                hardList.AddRange(local);
            }
            else
            {
                var newList = new List<CustomAttributeData>(attribs.Count + local.Count);
                newList.AddRange(attribs);
                newList.AddRange(local);
                attribs = newList;
            }
        }
        private AttributeHelper(IList<CustomAttributeData>? attribs)
        {
            if (attribs is object && attribs.Count == 0)
            {
                attribs = null;
            }
            _attribs = attribs;
        }

        public static AttributeHelper For(MethodInfo method, bool inherit)
        {
            IList<CustomAttributeData>? attribs = null;
            while (method is object)
            {
                Append(ref attribs, method.GetCustomAttributesData());
                method = (inherit ? GetAncestor(method) : null)!;
            }
            return new AttributeHelper(attribs);
        }

        static MethodInfo? GetAncestor(MethodInfo method)
        {
            if (method is null || method.IsStatic) return null;
            var baseMethod = method.GetBaseDefinition();
            var type = method.DeclaringType;
            if (type is null || type == baseMethod.DeclaringType) return null;

            var parentMethods = type.BaseType?.GetMethods((method.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) | BindingFlags.Instance)
                ?? Array.Empty<MethodInfo>();
            foreach (var parentMethod in parentMethods)
            {
                if (parentMethod.GetBaseDefinition() == baseMethod) return parentMethod;
            }
            return null;
        }

        public bool TryGetNamedArgument(string typeName, string name, out CustomAttributeTypedArgument value)
            => TryGet(typeName, name, true, false, out value);
        public bool TryGetConstructorParameter(string typeName, string name, out CustomAttributeTypedArgument value)
            => TryGet(typeName, name, false, true, out value);
        public bool TryGetAny(string typeName, string name, out CustomAttributeTypedArgument value)
            => TryGet(typeName, name, true, true, out value);

        internal bool TryGetAnyNonWhitespaceString(string typeName, string name, out string value)
        {
            if (TryGetAny(typeName, name, out var cata) && cata.Value is string typed)
            {
                value = typed;
                return !string.IsNullOrWhiteSpace(value);
            }
            value = "";
            return false;
        }


        public bool IsDefined(string typeName)
        {
            foreach (var attrib in _attribs ?? Array.Empty<CustomAttributeData>())
            {
                if (attrib.AttributeType.FullName == typeName) return true;
            }
            return false;
        }

        private bool TryGet(string typeName, string name, bool tryNamedArgument, bool tryConstructorParam, out CustomAttributeTypedArgument value)
        {
            foreach (var attrib in _attribs ?? Array.Empty<CustomAttributeData>())
            {
                if (attrib.AttributeType.FullName == typeName)
                {
                    if (tryNamedArgument)
                    {
                        foreach (var named in attrib.NamedArguments)
                        {
                            if (string.Equals(named.MemberName, name, StringComparison.OrdinalIgnoreCase))
                            {
                                value = named.TypedValue;
                                return true;
                            }
                        }
                    }

                    if (tryConstructorParam)
                    {
                        var ctorParams = attrib.Constructor.GetParameters();
                        for (int i = 0; i < ctorParams.Length; i++)
                        {
                            if (string.Equals(ctorParams[i].Name, name, StringComparison.OrdinalIgnoreCase))
                            {
                                value = attrib.ConstructorArguments[i];
                                return true;
                            }
                        }
                    }
                }
            }
            value = default;
            return false;
        }
    }
}