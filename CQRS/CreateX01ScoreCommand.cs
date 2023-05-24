using System;
using Amazon.Lambda.APIGatewayEvents;
using MediatR;

public class CreateX01ScoreCommand : IRequest<APIGatewayProxyResponse>
{
    public string GameId { get; set; }
    public long PlayerId { get; set; }
    public int Score { get; set; }
    public int Input { get; set; }
}