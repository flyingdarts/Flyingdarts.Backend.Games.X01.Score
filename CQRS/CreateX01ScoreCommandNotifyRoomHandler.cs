using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Lambdas.Shared;
using Flyingdarts.Persistence;
using Flyingdarts.Shared;
using MediatR.Pipeline;
using Microsoft.Extensions.Options;
public record CreateX01ScoreCommandNotifyRoomHandler(IDynamoDBContext DbContext, IOptions<ApplicationOptions> ApplicationOptions, IAmazonApiGatewayManagementApi ApiGatewayClient) : IRequestPostProcessor<CreateX01ScoreCommand, APIGatewayProxyResponse>
{
    public async Task Process(CreateX01ScoreCommand request, APIGatewayProxyResponse response, CancellationToken cancellationToken)
    {
        var socketMessage = new SocketMessage<CreateX01ScoreCommand>
        {
            Message = request,
            Action = "v2/games/x01/score"
        };

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(socketMessage)));

        foreach (var user in request.Users)
        {
            if (!string.IsNullOrEmpty(user.ConnectionId))
            {
                var connectionId = user.UserId == request.PlayerId
                    ? request.ConnectionId : user.ConnectionId;

                var postConnectionRequest = new PostToConnectionRequest
                {
                    ConnectionId = connectionId,
                    Data = stream
                };

                stream.Position = 0;

                await ApiGatewayClient.PostToConnectionAsync(postConnectionRequest, cancellationToken);
            }
        }
    }

    private async Task<List<GamePlayer>> GetGamePlayersAsync(long gameId, CancellationToken cancellationToken)
    {
        var gamePlayers = await DbContext.FromQueryAsync<GamePlayer>(QueryConfig(gameId.ToString()), ApplicationOptions.Value.ToOperationConfig())
            .GetRemainingAsync(cancellationToken);
        return gamePlayers;
    }

    private static QueryOperationConfig QueryConfig(string gameId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.GamePlayer);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, gameId);
        return new QueryOperationConfig { Filter = queryFilter };
    }
}