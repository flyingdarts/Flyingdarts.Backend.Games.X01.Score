using Amazon.DynamoDBv2.DataModel;
using Flyingdarts.Shared;
using Microsoft.Extensions.Options;
using MediatR.Pipeline;
using System.Threading.Tasks;
using Flyingdarts.Persistence;
using System.Threading;
using Amazon.DynamoDBv2.DocumentModel;
using System.Linq;

public class CreateX01ScoreCommandConnectionIdUpdater : IRequestPreProcessor<CreateX01ScoreCommand>
{
    private readonly IDynamoDBContext _dbContext;
    private readonly IOptions<ApplicationOptions> _options;

    public CreateX01ScoreCommandConnectionIdUpdater(IDynamoDBContext DbContext, IOptions<ApplicationOptions> ApplicationOptions)
    {
        _dbContext = DbContext;
        _options = ApplicationOptions;
    }

    public async Task Process(CreateX01ScoreCommand request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(request.PlayerId, cancellationToken);

        await UpdateUserAsync(user, request.ConnectionId, cancellationToken);
    }

    private async Task UpdateUserAsync(User user, string connectionId, CancellationToken cancellationToken)
    {
        var userWrite = _dbContext.CreateBatchWrite<User>(_options.Value.ToOperationConfig());

        user.ConnectionId = connectionId;

        userWrite.AddPutItem(user);

        await userWrite.ExecuteAsync(cancellationToken);
    }
    
    private async Task<User> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var results = await _dbContext.FromQueryAsync<User>(QueryConfig(userId)).GetRemainingAsync(cancellationToken);

        return results.Single();
    }

    private static QueryOperationConfig QueryConfig(string userId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.User);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, userId);
        return new QueryOperationConfig { Filter = queryFilter };
    }
}