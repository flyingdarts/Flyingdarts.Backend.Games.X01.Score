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

        if (request.Game == null)
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(socketMessage)
            };

        var gameDart = GameDart.Create(request.Game.GameId, request.PlayerId, request.Input, request.Score);

        request.Darts.Add(gameDart);
        request.History = new();
        request.Players.ForEach(p =>
        {
            request.History.Add(p.PlayerId, new());
            request.History[p.PlayerId].Score = request.Darts.OrderByDescending(x => x.CreatedAt).First().GameScore;
            request.History[p.PlayerId].History = request.Darts.OrderBy(x => x.CreatedAt).Where(x => x.PlayerId == p.PlayerId).Select(x => x.Score).ToList();
        });

        request.NextToThrow = request.Players.First(x=>x.PlayerId != request.PlayerId).PlayerId;
        
        var write = _dbContext.CreateBatchWrite<GameDart>(_applicationOptions.ToOperationConfig());

        write.AddPutItem(gameDart);

        await write.ExecuteAsync(cancellationToken);


        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(socketMessage)
        };
    }
}