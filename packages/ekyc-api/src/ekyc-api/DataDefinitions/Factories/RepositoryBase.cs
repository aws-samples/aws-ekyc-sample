using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

namespace ekyc_api.DataDefinitions.Factories
{
    public abstract class RepositoryBase<T>
    {
        private readonly DynamoDBContext _dbContext;
        private readonly IAmazonDynamoDB _dynamoDb;

        public RepositoryBase(IAmazonDynamoDB dynamoDb)
        {
            _dynamoDb = dynamoDb;
            _dbContext = new DynamoDBContext(_dynamoDb);
        }

        public async Task<T> GetObjectById(object hashKey)
        {
            var item = await _dbContext.LoadAsync<T>(hashKey);

            return item;
        }

        public async Task DeleteObject(object hashKey)
        {
            await _dbContext.DeleteAsync<T>(hashKey);
        }

        public async Task SaveObject(T item)
        {
            await _dbContext.SaveAsync(item);
        }
    }
}