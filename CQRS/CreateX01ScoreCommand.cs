using System;
using System.Collections.Generic;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
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
    internal ILambdaContext LambdaContext { get;set;}
    public Dictionary<string, int[]> History { get; set; }
}