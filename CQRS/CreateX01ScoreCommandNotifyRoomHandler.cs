using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Lambdas.Shared;
using Flyingdarts.Persistence;
using Flyingdarts.Shared;
using MediatR.Pipeline;
using Microsoft.Extensions.Options;

namespace Flyingdarts.Backend.Games.X01.Join.CQRS
{
    public class CreateX01ScoreCommandNotifyRoomHandler : IRequestPostProcessor<CreateX01ScoreCommand, APIGatewayProxyResponse>
    {
        private readonly IDynamoDBContext _dbContext;
        private readonly ApplicationOptions _applicationOptions;
        private readonly IAmazonApiGatewayManagementApi _apiGatewayClient;

        public CreateX01ScoreCommandNotifyRoomHandler(IDynamoDBContext dbContext, IOptions<ApplicationOptions> applicationOptions, IAmazonApiGatewayManagementApi apiGatewayClient)
        {
            _dbContext = dbContext;
            _applicationOptions = applicationOptions.Value;
            _apiGatewayClient = apiGatewayClient;
        }

        public async Task Process(CreateX01ScoreCommand request, APIGatewayProxyResponse response, CancellationToken cancellationToken)
        {        
            request.History = new();
            request.Players.ForEach(p =>
            {
                request.History.Add(p.PlayerId, new());
                request.History[p.PlayerId].Score = request.Darts.OrderByDescending(x=>x.CreatedAt).First().GameScore;
                request.History[p.PlayerId].History = request.Darts.OrderBy(x=>x.CreatedAt).Where(x=>x.PlayerId == p.PlayerId).Select(x=> x.Score).ToList();
            });

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

                    await _apiGatewayClient.PostToConnectionAsync(postConnectionRequest, cancellationToken);
                }
            }
        }

        private async Task<List<GamePlayer>> GetGamePlayersAsync(long gameId, CancellationToken cancellationToken)
        {
            var gamePlayers = await _dbContext.FromQueryAsync<GamePlayer>(QueryConfig(gameId.ToString()), _applicationOptions.ToOperationConfig())
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
}