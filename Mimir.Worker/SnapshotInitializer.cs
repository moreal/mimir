using Bencodex.Types;
using HeadlessGQL;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Mimir.Worker.Constants;
using Mimir.Worker.Handler;
using Mimir.Worker.Models;
using Mimir.Worker.Scrapper;
using Mimir.Worker.Services;
using Mimir.Worker.Util;
using Nekoyume;
using Nekoyume.Model.State;

namespace Mimir.Worker;

public class SnapshotInitializer
{
    private readonly DiffMongoDbService _store;
    private readonly ILogger<SnapshotInitializer> _logger;
    private readonly string _chainStorePath;

    public SnapshotInitializer(
        ILogger<SnapshotInitializer> logger,
        DiffMongoDbService store,
        string chainStorePath
    )
    {
        _logger = logger;
        _store = store;
        _chainStorePath = chainStorePath;
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var started = DateTime.UtcNow;

        (BlockChain blockChain, IStore store, IStateStore stateStore) = ChainUtil.LoadBlockChain(
            _chainStorePath
        );

        foreach (var (address, handler) in AddressHandlerMappings.HandlerMappings)
        {
            await ProcessByAccountAddress(
                blockChain,
                store,
                stateStore,
                address,
                handler,
                stoppingToken
            );

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        await _store.UpdateLatestBlockIndex(blockChain.Tip.Index);

        _logger.LogInformation(
            "Finished SnapshotInitializer. Elapsed {TotalElapsedMinutes} minutes",
            DateTime.UtcNow.Subtract(started).Minutes
        );
    }

    private async Task ProcessByAccountAddress(
        BlockChain blockChain,
        IStore store,
        IStateStore stateStore,
        Address accountAddress,
        IStateHandler<StateData> handler,
        CancellationToken stoppingToken
    )
    {
        int predicateLength = Address.Size * 2;

        ITrie worldTrie = ChainUtil.GetWorldTrie(blockChain);
        IWorldState world = new WorldBaseState(worldTrie, stateStore);
        IAccountState account = world.GetAccountState(accountAddress);
        ITrie accountTrie = account.Trie;
        _logger.LogInformation(
            "Iterating over trie with state root hash {StateRootHash}",
            accountTrie.Hash
        );

        long addressCount = 0;
        string? currentAddress = null;

        foreach ((KeyBytes keyBytes, IValue value) in accountTrie.IterateValues())
        {
            if (keyBytes.Length == predicateLength)
            {
                addressCount++;
                Address address = ChainUtil.ToAddress(keyBytes);
                currentAddress = ByteUtil.Hex(address.ByteArray);
            }

            if (currentAddress is string hex)
            {
                var stateData = handler.ConvertToStateData(
                    new()
                    {
                        Address = new Address(currentAddress),
                        RawState = value,
                    }
                );
                if (
                    CollectionNames.CollectionMappings.TryGetValue(
                        stateData.State.GetType(),
                        out var collectionName
                    )
                )
                {
                    await _store.UpsertStateDataAsync(stateData, collectionName);
                }

                _logger.LogInformation($"Address: {currentAddress}, address count: {addressCount}");
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Total address count: {AddressCount}", addressCount);

        store.Dispose();
        stateStore.Dispose();
    }
}
