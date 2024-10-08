using HotChocolate.Types;
using Lib9c.Models.AttachmentActionResults;

namespace Lib9c.GraphQL.Types.AttachmentActionResults;

public class CombinationConsumable5ResultType : ObjectType<CombinationConsumable5Result>
{
    protected override void Configure(IObjectTypeDescriptor<CombinationConsumable5Result> descriptor)
    {
        descriptor.Implements<AttachmentActionResultInterfaceType>();
    }
}
