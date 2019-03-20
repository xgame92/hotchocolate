using System.Linq;
using System;
using System.Collections.Generic;
using HotChocolate.Resolvers;
using HotChocolate.Types.Descriptors;
using HotChocolate.Utilities;
using HotChocolate.Language;
using HotChocolate.Types;

namespace HotChocolate
{
    internal class TypeInitializer
    {
        private static readonly TypeInspector _typeInspector =
            new TypeInspector();
        private readonly List<InitializationContext> _initContexts =
            new List<InitializationContext>();
        private readonly Dictionary<RegisteredType, CompletionContext> _cmpCtx =
            new Dictionary<RegisteredType, CompletionContext>();
        private readonly Dictionary<ITypeReference, RegisteredType> _types =
            new Dictionary<ITypeReference, RegisteredType>();
        private readonly Dictionary<ITypeReference, ITypeReference> _clrTypes =
            new Dictionary<ITypeReference, ITypeReference>();
        private readonly Dictionary<NameString, ITypeReference> _named =
            new Dictionary<NameString, ITypeReference>();
        private readonly Dictionary<ITypeReference, ITypeReference> _depsLup =
            new Dictionary<ITypeReference, ITypeReference>();
        private readonly Dictionary<FieldReference, RegisteredResolver> _res =
            new Dictionary<FieldReference, RegisteredResolver>();
        private readonly List<FieldMiddleware> _globalComps =
            new List<FieldMiddleware>();
        private readonly List<ISchemaError> _errors =
            new List<ISchemaError>();

        private readonly IServiceProvider _services;
        private readonly List<ITypeReference> _initialTypes;
        private readonly Func<TypeSystemObjectBase, bool> _isQueryType;

        public TypeInitializer(
            IServiceProvider services,
            IEnumerable<ITypeReference> initialTypes,
            Func<TypeSystemObjectBase, bool> isQueryType)
        {
            if (initialTypes == null)
            {
                throw new ArgumentNullException(nameof(initialTypes));
            }

            _services = services
                ?? throw new ArgumentNullException(nameof(services));
            _isQueryType = isQueryType
                ?? throw new ArgumentNullException(nameof(isQueryType));
            _initialTypes = initialTypes.ToList();
        }

        private bool RegisterTypes()
        {
            var typeRegistrar = new TypeRegistrar_new(_initialTypes, _services);
            if (typeRegistrar.Complete())
            {
                foreach (InitializationContext context in
                    typeRegistrar.InitializationContexts)
                {
                    foreach (FieldReference reference in context.Resolvers.Keys)
                    {
                        if (!_res.ContainsKey(reference))
                        {
                            _res[reference] = context.Resolvers[reference];
                        }
                    }
                    _initContexts.Add(context);
                }

                foreach (ITypeReference key in typeRegistrar.Registerd.Keys)
                {
                    _types[key] = typeRegistrar.Registerd[key];
                }

                foreach (ITypeReference key in typeRegistrar.ClrTypes.Keys)
                {
                    _clrTypes[key] = typeRegistrar.ClrTypes[key];
                }
                return true;
            }

            _errors.AddRange(typeRegistrar.Errors);
            return false;
        }

        private void CompileResolvers() =>
            ResolverCompiler.Compile(_res);

        private bool CompleteNames()
        {
            bool success = CompleteTypes(TypeDependencyKind.Named,
                registeredType =>
                {
                    InitializationContext initializationContext =
                        _initContexts.First(t =>
                            t.Type == registeredType.Type);
                    var completionContext = new CompletionContext(
                        initializationContext, _globalComps,
                        _depsLup, _types);
                    _cmpCtx[registeredType] = completionContext;

                    registeredType.Type.CompleteName(completionContext);

                    if (_named.ContainsKey(registeredType.Type.Name))
                    {
                        // TODO : resources
                        _errors.Add(SchemaErrorBuilder.New()
                            .SetMessage("Duplicate name!")
                            .SetTypeSystemObject(registeredType.Type)
                            .Build());
                        return false;
                    }

                    _named[registeredType.Type.Name] = registeredType.Reference;
                    return true;
                });

            if (success)
            {
                UpdateDependencyLookup();
            }

            return success;
        }

        private void UpdateDependencyLookup()
        {
            foreach (RegisteredType registeredType in _types.Values)
            {
                TryNormalizeDependencies(
                    registeredType,
                    registeredType.Dependencies
                        .Select(t => t.TypeReference),
                    out _);
            }
        }

        private bool CompleteTypes()
        {
            return CompleteTypes(TypeDependencyKind.Completed, registeredType =>
            {
                CompletionContext context = _cmpCtx[registeredType];
                context.IsQueryType = _isQueryType.Invoke(registeredType.Type);
                registeredType.Type.CompleteType(context);
                return true;
            });
        }

        private bool CompleteTypes(
            TypeDependencyKind kind,
            Func<RegisteredType, bool> action)
        {
            var processed = new HashSet<ITypeReference>();
            var batch = new List<RegisteredType>(
                GetInitialBatch(kind));

            while (processed.Count < _types.Count && batch.Count > 0)
            {
                foreach (RegisteredType registeredType in batch)
                {
                    if (!action(registeredType))
                    {
                        return false;
                    }
                    processed.Add(registeredType.Reference);
                }

                batch.Clear();
                batch.AddRange(GetNextBatch(processed, kind));
            }

            // TODO : resources
            if (processed.Count < _types.Count)
            {
                _errors.Add(SchemaErrorBuilder.New()
                    .SetMessage("Unable to resolve dependencies")
                    .Build());
                return false;
            }

            return _errors.Count == 0;
        }

        private IEnumerable<RegisteredType> GetInitialBatch(
            TypeDependencyKind kind)
        {
            return _types.Values.Where(t =>
                t.Dependencies.All(d => d.Kind != kind));
        }

        private IEnumerable<RegisteredType> GetNextBatch(
            ISet<ITypeReference> processed,
            TypeDependencyKind kind)
        {
            foreach (RegisteredType type in _types.Values)
            {
                if (!processed.Contains(type.Reference))
                {
                    IEnumerable<ITypeReference> references =
                        type.Dependencies.Where(t => t.Kind == kind)
                            .Select(t => t.TypeReference);

                    if (TryNormalizeDependencies(
                        type,
                        references,
                        out IReadOnlyList<ITypeReference> normalized))
                    {
                        yield return type;
                    }
                }
            }
        }

        private bool TryNormalizeDependencies(
            RegisteredType registeredType,
            IEnumerable<ITypeReference> dependencies,
            out IReadOnlyList<ITypeReference> normalized)
        {
            var n = new List<ITypeReference>();

            foreach (ITypeReference reference in dependencies)
            {
                if (!_depsLup.TryGetValue(reference, out ITypeReference nr)
                    && !TryNormalizeReference(reference, out nr))
                {
                    normalized = null;
                    return false;
                }
                _depsLup[reference] = nr;
                n.Add(nr);
            }

            normalized = n;
            return true;
        }

        private bool TryNormalizeReference(
            ITypeReference typeReference,
            out ITypeReference normalized)
        {
            switch (typeReference)
            {
                case IClrTypeReference r:
                    if (TryNormalizeClrReference(
                        r, out IClrTypeReference cnr))
                    {
                        normalized = cnr;
                        return true;
                    }
                    break;

                case ISchemaTypeReference r:
                    var internalReference = new ClrTypeReference(
                        r.Type.GetType(), r.Context);
                    normalized = internalReference;
                    return true;

                case ISyntaxTypeReference r:
                    if (_named.TryGetValue(
                        r.Type.NamedType().Name.Value,
                        out ITypeReference nr))
                    {
                        normalized = nr;
                        return true;
                    }
                    break;
            }

            normalized = null;
            return false;
        }

        private bool TryNormalizeClrReference(
            IClrTypeReference typeReference,
            out IClrTypeReference normalized)
        {
            if (!BaseTypes.IsNonGenericBaseType(typeReference.Type)
                && _typeInspector.TryCreate(typeReference.Type,
                    out TypeInfo typeInfo))
            {
                if (IsTypeSystemObject(typeInfo.ClrType))
                {
                    normalized = new ClrTypeReference(
                        typeInfo.ClrType,
                        typeReference.Context);
                    return true;
                }
                else
                {
                    normalized = new ClrTypeReference(
                        typeInfo.ClrType,
                        typeReference.Context);

                    if (_clrTypes.TryGetValue(normalized, out ITypeReference r)
                        && r is IClrTypeReference cr)
                    {
                        normalized = cr;
                        return true;
                    }
                }
            }

            normalized = null;
            return false;
        }

        private static bool IsTypeSystemObject(Type type) =>
            typeof(TypeSystemObjectBase).IsAssignableFrom(type);
    }
}