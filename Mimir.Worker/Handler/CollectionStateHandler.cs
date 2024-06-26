using Bencodex.Types;
using Libplanet.Crypto;
using Mimir.Worker.Models;
using Mimir.Worker.Services;

namespace Mimir.Worker.Handler;

public class CollectionStateHandler : IStateHandler<StateData>
{
    public StateData ConvertToStateData(StateDiffContext context) =>
        new(context.Address, ConvertToState(context.Address, context.RawState));

    private static CollectionState ConvertToState(Address address, IValue state)
    {
        if (state is not List value)
        {
            throw new InvalidCastException(
                $"{nameof(state)} Invalid state type. Expected {nameof(List)}, got {state.GetType().Name}."
            );
        }

        var collectionState = new Nekoyume.Model.State.CollectionState(value);
        return new CollectionState(address, collectionState);
    }

    public async Task StoreStateData(MongoDbService store, StateData stateData)
    {
        await store.UpsertStateDataAsyncWithLinkAvatar(stateData);
    }
}
