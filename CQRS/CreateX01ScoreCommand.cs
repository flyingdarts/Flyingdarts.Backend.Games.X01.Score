using System;
using System.Collections.Generic;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Persistence;
using MediatR;

public class CreateX01ScoreCommand : IRequest<APIGatewayProxyResponse>
{
    public string GameId { get; set; }
    public string PlayerId { get; set; }
    public int Score { get; set; }
    public int Input { get; set; }

    internal string ConnectionId { get; set; }
    internal Game Game { get; set; }
    internal List<GamePlayer> Players { get; set; }
    internal List<GameDart> Darts { get; set; }
    internal List<User> Users { get; set; }

    public Dictionary<string, ScoreboardRecord> History { get; set; }
    public string NextToThrow { get; set; }
}


public class ScoreboardRecord
{
    public int Score { get; set; }
    public List<int> History { get; set; }
}