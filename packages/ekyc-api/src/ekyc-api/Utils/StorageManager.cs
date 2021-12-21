using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.Utils
{
    public class StorageManager
    {
        private readonly IAmazonS3 _amazonS3;

        private readonly ILogger _logger;

        private IConfiguration config;


        public StorageManager(ILogger logger, IConfiguration config, IAmazonS3 s3)
        {
            this.config = config;
            _amazonS3 = s3;
            _logger = logger;
        }

        public async Task DeleteObjectFromS3(string s3Key)
        {
            await _amazonS3.DeleteObjectAsync(new DeleteObjectRequest()
            {
                BucketName = Globals.StorageBucket,
                Key = s3Key
            });
        }

        public async Task<bool> ObjectExistsInS3(string s3Key)
        {
            try
            {
                var objectMeta = await _amazonS3.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = Globals.StorageBucket,
                    Key = s3Key
                });
                return true;
            }
            catch (AmazonS3Exception)
            {
                return false;
            }
        }
    }
}