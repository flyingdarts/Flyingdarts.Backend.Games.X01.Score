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
using System.Collections.Generic;

public record CreateX01ScoreCommandHandler(IDynamoDbService DynamoDbService) : IRequestHandler<CreateX01ScoreCommand, APIGatewayProxyResponse>
{
    public async Task<APIGatewayProxyResponse> Handle(CreateX01ScoreCommand request, CancellationToken cancellationToken)
    {
        var socketMessage = new SocketMessage<CreateX01ScoreCommand>();
        socketMessage.Message = request;
        socketMessage.Action = "v2/games/x01/score";

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

       

        request.Game = await DynamoDbService.ReadGameAsync(long.Parse(request.GameId), cancellationToken);
        request.Players = await DynamoDbService.ReadGamePlayersAsync(long.Parse(request.GameId), cancellationToken);
        request.Users = await DynamoDbService.ReadUsersAsync(request.Players.Select(x => x.PlayerId).ToArray(), cancellationToken);
        request.Darts = await DynamoDbService.ReadGameDartsAsync(long.Parse(request.GameId), cancellationToken);
        var gameDart = GameDart.Create(request.Game.GameId, request.PlayerId, request.Input, request.Score, currentSet, currentLeg);

        await DynamoDbService.WriteGameDartAsync(gameDart, cancellationToken);
        request.Darts.Add(gameDart);
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
                    PlayerName = request.Users.Single(y => y.UserId == x.PlayerId).Profile.UserName,
                    Country = request.Users.Single(y => y.UserId == x.PlayerId).Profile.Country.ToLower(),
                    CreatedAt = long.Parse(x.PlayerId)
                };
            }).OrderBy(x => x.CreatedAt);

            data.Players = orderedPlayers;
        }

        DetermineNextPlayer(data);

        socketMessage.Metadata = data.toDictionary();

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(socketMessage)
        };
    }
    public void DetermineNextPlayer(Metadata metadata)
    {
        Dictionary<string, List<DartDto>> darts = metadata.Darts;

        // Create a dictionary to keep track of the number of darts thrown by each player.
        Dictionary<string, int> dartsThrownByPlayer = new Dictionary<string, int>();

        // Initialize the dartsThrownByPlayer dictionary with 0 darts for each player.
        foreach (var player in metadata.Players)
        {
            dartsThrownByPlayer[player.PlayerId] = 0;
        }

        // Calculate the total number of darts thrown by each player.
        foreach (var playerDarts in darts.Values)
        {
            foreach (var dart in playerDarts)
            {
                if (dartsThrownByPlayer.ContainsKey(dart.PlayerId))
                {
                    dartsThrownByPlayer[dart.PlayerId]++;
                }
            }
        }

        // Find the player with the lowest number of darts thrown.
        string nextPlayer = dartsThrownByPlayer.OrderBy(x => x.Value).FirstOrDefault().Key;

        // Set the NextPlayer property in the Metadata class to the player with the lowest darts thrown.
        metadata.NextPlayer = long.Parse(nextPlayer); // Assuming NextPlayer is of type long
    }

}