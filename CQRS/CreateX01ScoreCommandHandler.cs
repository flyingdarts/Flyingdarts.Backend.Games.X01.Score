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
using System;

public record CreateX01ScoreCommandHandler(IDynamoDbService DynamoDbService) : IRequestHandler<CreateX01ScoreCommand, APIGatewayProxyResponse>
{
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


        // begin calculate sets and legs possibly close game
        var currentSet = request.Darts.Select(x=>x.Set).DefaultIfEmpty(1).Max();
        var currentLeg = request.Darts.Select(x=>x.Leg).DefaultIfEmpty(1).Max();

        if (request.Darts.Any() && request.Darts.OrderBy(x=>x.CreatedAt).Last().Score == 0) {
            currentLeg++;
            if (currentLeg > request.Game.X01.Legs) {
                currentLeg = 1;
                currentSet++;
            }
        }

        var gameDart = GameDart.Create(request.Game.GameId, request.PlayerId, request.Input, request.Score, currentSet, currentLeg);

        await DynamoDbService.WriteGameDartAsync(gameDart, cancellationToken);

        request.Game = await DynamoDbService.ReadGameAsync(long.Parse(request.GameId), cancellationToken);
        request.Players = await DynamoDbService.ReadGamePlayersAsync(long.Parse(request.GameId), cancellationToken);
        request.Users = await DynamoDbService.ReadUsersAsync(request.Players.Select(x => x.PlayerId).ToArray(), cancellationToken);
        request.Darts = await DynamoDbService.ReadGameDartsAsync(long.Parse(request.GameId), cancellationToken);

        Metadata data = new Metadata();

        if (request.Game is not null)
        {
            data.Game = new GameDto
            {
                Id = request.Game.GameId.ToString(),
                PlayerCount = request.Game.PlayerCount,
                Status = (GameStatusDto)(int)request.Game.Status,
                Type = (GameTypeDto)(int)request.Game.Type,
                X01 = new X01GameSettingsDto
                {
                    DoubleIn = request.Game.X01.DoubleIn,
                    DoubleOut = request.Game.X01.DoubleOut,
                    Legs = request.Game.X01.Legs,
                    Sets = request.Game.X01.Sets,
                    StartingScore = request.Game.X01.StartingScore
                }
            };
        }

        if (request.Darts is not null)
        {
            data.Darts = new();
            request.Players.ForEach(p =>
            {
                data.Darts.Add(p.PlayerId, new());
                data.Darts[p.PlayerId] = request.Darts.OrderBy(x => x.CreatedAt).Where(x => x.PlayerId == p.PlayerId).Select(x => new DartDto { Id = x.Id, Score = x.Score, GameScore = x.GameScore }).ToList();
            });
        }

        if (request.Players is not null)
        {
            var orderedPlayers = request.Players.Select(x =>
            {
                return new PlayerDto
                {
                    PlayerId = x.PlayerId,
                    PlayerName = request.Users.Single(y => y.UserId == x.PlayerId).Profile.UserName
                };
            }).OrderBy(x => x.CreatedAt);

            data.Players = orderedPlayers;
        }

        socketMessage.Metadata = data.toDictionary();

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(socketMessage)
        };
    }
}