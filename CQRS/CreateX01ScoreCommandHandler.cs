using System.Linq;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Lambdas.Shared;
using Flyingdarts.Persistence;
using Flyingdarts.Shared;
using MediatR;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class CreateX01ScoreCommandHandler : IRequestHandler<CreateX01ScoreCommand, APIGatewayProxyResponse>
{
    private readonly IDynamoDBContext _dbContext;
    private readonly ApplicationOptions _applicationOptions;
    public CreateX01ScoreCommandHandler(IDynamoDBContext dbContext, IOptions<ApplicationOptions> applicationOptions)
    {
        _dbContext = dbContext;
        _applicationOptions = applicationOptions.Value;
    }
    public async Task<APIGatewayProxyResponse> Handle(CreateX01ScoreCommand request, CancellationToken cancellationToken)
    {
        var socketMessage = new SocketMessage<CreateX01ScoreCommand>();
        socketMessage.Message = request;
        socketMessage.Action = "v2/games/x01/score";

        if (await GetGame(request.GameId) == null)
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(socketMessage)
            };

        var gameDart = GameDart.Create(request.GameId, request.PlayerId, request.Score, request.Input);
        var write = _dbContext.CreateBatchWrite<GameDart>(_applicationOptions.ToOperationConfig());
        write.AddPutItem(gameDart);
        await write.ExecuteAsync(cancellationToken);

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(socketMessage)
        };
    }
    private async Task<Game> GetGame(long gameId)
    {
        var result = await _dbContext.FromQueryAsync<Game>(
                X01GamesQueryConfig(gameId),
                _applicationOptions.ToOperationConfig())
            .GetRemainingAsync(CancellationToken.None);
        return result.SingleOrDefault();
    }
    private static QueryOperationConfig X01GamesQueryConfig(long gameId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.Game);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, $"{gameId}#");
        return new QueryOperationConfig { Filter = queryFilter };
    }
}