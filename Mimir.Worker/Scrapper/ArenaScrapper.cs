using Mimir.Worker.Services;
using Mimir.Worker.Models;
using Libplanet.Crypto;

namespace Mimir.Worker.Scrapper;

public class ArenaScrapper(ILogger<ArenaScrapper> logger, IStateService service, MongoDbStore store)
{
    private readonly ILogger<ArenaScrapper> _logger = logger;

    private readonly IStateService _stateService = service;
    private readonly MongoDbStore _store = store;

    public async Task ExecuteAsync(long blockIndex, CancellationToken cancellationToken)
    {
        var stateGetter = _stateService.At(blockIndex);
        var roundData = await stateGetter.GetArenaRoundData(blockIndex);
        var arenaParticipants = await stateGetter.GetArenaParticipantsState(roundData.ChampionshipId, roundData.Round);

        var buffer = new List<(Address AvatarAddress, ArenaData Arena, AvatarData Avatar)>();
        const int maxBufferSize = 10;
        async Task FlushBufferAsync()
        {
            await _store.BulkUpsertArenaDataAsync(buffer.Select(x => x.Arena).ToList());
            await _store.BulkUpsertAvatarDataAsync(buffer.Select(x => x.Avatar).ToList());
            foreach (var pair in buffer)
            {
                await _store.LinkAvatarWithArenaAsync(pair.AvatarAddress);
            }
            
            buffer.Clear();
        }

        foreach (var avatarAddress in arenaParticipants.AvatarAddresses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var arenaData = await stateGetter.GetArenaData(roundData, avatarAddress);
            var avatarData = await stateGetter.GetAvatarData(avatarAddress);

            if (arenaData != null && avatarData != null)
            {
                buffer.Add((avatarAddress, arenaData, avatarData));
            }

            if (buffer.Count >= maxBufferSize)
            {
                await FlushBufferAsync();
            }
        }

        if (buffer.Count > 0)
        {
            await FlushBufferAsync();
        }
    }
}
