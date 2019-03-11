using System;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Language;
using HotChocolate.Properties;
using HotChocolate.Resolvers;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using HotChocolate.Utilities;

namespace HotChocolate.Types
{
    public class DirectiveType<TDirective>
        : DirectiveType
        where TDirective : class
    {
        private readonly Action<IDirectiveTypeDescriptor> _configure;

        protected DirectiveType()
        {
            _configure = Configure;
        }

        public DirectiveType(Action<IDirectiveTypeDescriptor> configure)
        {
            _configure = configure
                ?? throw new ArgumentNullException(nameof(configure));
        }

        protected override DirectiveTypeDefinition CreateDefinition(
            IInitializationContext context)
        {
            DirectiveTypeDescriptor<TDirective> descriptor =
                DirectiveTypeDescriptor.New<TDirective>(
                    DescriptorContext.Create(context.Services));
            _configure(descriptor);
            return descriptor.CreateDefinition();
        }

        protected virtual void Configure(
            IDirectiveTypeDescriptor<TDirective> descriptor)
        {

        }

        protected sealed override void Configure(
            IDirectiveTypeDescriptor descriptor)
        {
            // TODO : resources
            throw new NotSupportedException();
        }
    }
}