using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Flyingdarts.Persistence;
using Flyingdarts.Shared;
using MediatR.Pipeline;

public record CreateX01ScoreCommandGameFetcher(IDynamoDBContext DbContext, ApplicationOptions ApplicationOptions) : IRequestPreProcessor<CreateX01ScoreCommand>
{
    public async Task Process(CreateX01ScoreCommand request, CancellationToken cancellationToken)
    {
        request.Game = await GetGameAsync(long.Parse(request.GameId), cancellationToken);
        request.Players = await GetGamePlayersAsync(request.Game.GameId, cancellationToken);
        request.Darts = await GetGameDartsAsync(request.Game.GameId, cancellationToken);
        request.Users = await GetUsersAsync(request.Players.Select(x => x.PlayerId).ToArray(), cancellationToken);
    }

    private async Task<Game> GetGameAsync(long gameId, CancellationToken cancellationToken)
    {
        var games = await DbContext.FromQueryAsync<Game>(QueryGameConfig(gameId.ToString()), ApplicationOptions.ToOperationConfig())
            .GetRemainingAsync(cancellationToken);
        return games.Where(x => x.Status == GameStatus.Qualifying).ToList().Single();
    }

    private async Task<List<GamePlayer>> GetGamePlayersAsync(long gameId, CancellationToken cancellationToken)
    {
        var gamePlayers = await DbContext.FromQueryAsync<GamePlayer>(QueryGamePlayersConfig(gameId.ToString()), ApplicationOptions.ToOperationConfig())
            .GetRemainingAsync(cancellationToken);
        return gamePlayers.ToList();
    }

    private async Task<List<GameDart>> GetGameDartsAsync(long gameId, CancellationToken cancellationToken)
    {
        var gameDarts = await DbContext.FromQueryAsync<GameDart>(QueryGameDartsConfig(gameId.ToString()), ApplicationOptions.ToOperationConfig())
            .GetRemainingAsync(cancellationToken);
        return gameDarts.ToList();
    }

    private async Task<List<User>> GetUsersAsync(string[] userIds, CancellationToken cancellationToken)
    {
        List<User> users = new List<User>();
        for (var i = 0; i < userIds.Length; i++)
        {
            var resultSet = await DbContext.FromQueryAsync<User>(QueryUserConfig(userIds[i]), ApplicationOptions.ToOperationConfig()).GetRemainingAsync(cancellationToken);
            var user = resultSet.Single();
            users.Add(user);
        }
        return users;
    }

    private static QueryOperationConfig QueryUserConfig(string userId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.User);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, userId);
        return new QueryOperationConfig { Filter = queryFilter };
    }

    private static QueryOperationConfig QueryGameConfig(string gameId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.Game);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, gameId);
        return new QueryOperationConfig { Filter = queryFilter };
    }

    private static QueryOperationConfig QueryGamePlayersConfig(string gameId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.GamePlayer);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, gameId);
        return new QueryOperationConfig { Filter = queryFilter };
    }

    private static QueryOperationConfig QueryGameDartsConfig(string gameId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.GameDart);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, gameId);
        return new QueryOperationConfig { Filter = queryFilter };
    }
}