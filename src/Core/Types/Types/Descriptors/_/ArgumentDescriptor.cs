﻿using System.Reflection.Emit;
using System;
using HotChocolate.Utilities;
using HotChocolate.Language;
using HotChocolate.Types.Descriptors.Definitions;

namespace HotChocolate.Types.Descriptors
{
    public class ArgumentDescriptor
        : ArgumentDescriptorBase<ArgumentDefinition>
        , IArgumentDescriptor
    {
        public ArgumentDescriptor(string argumentName, Type argumentType)
            : this(argumentName)
        {
            if (argumentType == null)
            {
                throw new ArgumentNullException(nameof(argumentType));
            }

            Definition.Name = argumentName;
            Definition.Type = argumentType.GetInputType();
            Definition.DefaultValue = NullValueNode.Default;
        }

        public ArgumentDescriptor(NameString argumentName)
        {
            Definition.Name = argumentName.EnsureNotEmpty(nameof(argumentName));
            Definition.DefaultValue = NullValueNode.Default;
        }

        public new IArgumentDescriptor SyntaxNode(
            InputValueDefinitionNode inputValueDefinition)
        {
            base.SyntaxNode(inputValueDefinition);
            return this;
        }

        public new IArgumentDescriptor Description(string value)
        {
            base.Description(value);
            return this;
        }

        public new IArgumentDescriptor Type<TInputType>()
            where TInputType : IInputType
        {
            base.Type<TInputType>();
            return this;
        }

        public new IArgumentDescriptor Type<TInputType>(
            TInputType inputType)
            where TInputType : class, IInputType
        {
            base.Type<TInputType>(inputType);
            return this;
        }

        public new IArgumentDescriptor Type(
            ITypeNode typeNode)
        {
            base.Type(typeNode);
            return this;
        }

        public new IArgumentDescriptor DefaultValue(IValueNode value)
        {
            base.DefaultValue(value);
            return this;
        }

        public new IArgumentDescriptor DefaultValue(object value)
        {
            base.DefaultValue(value);
            return this;
        }

        public new IArgumentDescriptor Directive<TDirective>(
            TDirective directiveInstance)
            where TDirective : class
        {
            base.Directive(directiveInstance);
            return this;
        }

        public new IArgumentDescriptor Directive<TDirective>()
            where TDirective : class, new()
        {
            base.Directive<TDirective>();
            return this;
        }

        public new IArgumentDescriptor Directive(
            NameString name,
            params ArgumentNode[] arguments)
        {
            base.Directive(name, arguments);
            return this;
        }
    }
}
