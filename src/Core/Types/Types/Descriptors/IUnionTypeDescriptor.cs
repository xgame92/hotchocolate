﻿using HotChocolate.Language;

namespace HotChocolate.Types
{
    public interface IUnionTypeDescriptor
        : IFluent
    {
        IUnionTypeDescriptor SyntaxNode(UnionTypeDefinitionNode syntaxNode);

        IUnionTypeDescriptor Name(NameString name);

        IUnionTypeDescriptor Description(string description);

        IUnionTypeDescriptor Type<TObjectType>()
            where TObjectType : ObjectType;

        IUnionTypeDescriptor Type<TObjectType>(TObjectType type)
            where TObjectType : ObjectType;

        IUnionTypeDescriptor Type(NamedTypeNode objectType);

        IUnionTypeDescriptor ResolveAbstractType(
            ResolveAbstractType resolveAbstractType);

        IUnionTypeDescriptor Directive<T>(T directive)
            where T : class;

        IUnionTypeDescriptor Directive<T>()
            where T : class, new();

        IUnionTypeDescriptor Directive(
            NameString name,
            params ArgumentNode[] arguments);
    }
}
