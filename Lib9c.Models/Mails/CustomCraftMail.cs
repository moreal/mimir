using Bencodex.Types;
using Lib9c.Models.Exceptions;
using Lib9c.Models.Items;
using ValueKind = Bencodex.Types.ValueKind;

namespace Lib9c.Models.Mails;

public record CustomCraftMail : Mail
{
    public Equipment Equipment { get; init; }

    public override IValue Bencoded => ((Dictionary)base.Bencoded)
        .Add("equipment", Equipment.Bencoded);

    public CustomCraftMail(IValue bencoded) : base(bencoded)
    {
        if (bencoded is not Dictionary d)
        {
            throw new UnsupportedArgumentTypeException<ValueKind>(
                nameof(bencoded),
                new[] { ValueKind.Dictionary },
                bencoded.Kind);
        }

        Equipment = new Equipment(d["equipment"]);
    }
}
