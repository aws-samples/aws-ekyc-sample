using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.Utils
{

    /// <summary>
    /// Manages storage operations for Amazon S3.
    /// </summary>
    public class StorageManager
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly ILogger _logger;
        private IConfiguration config;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageManager"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="config">The configuration instance.</param>
        /// <param name="s3">The Amazon S3 client instance.</param>
        public StorageManager(ILogger logger, IConfiguration config, IAmazonS3 s3)
        {
            this.config = config;
            _amazonS3 = s3;
            _logger = logger;
        }

        /// <summary>
        /// Deletes an object from Amazon S3.
        /// </summary>
        /// <param name="s3Key">The key of the object to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DeleteObjectFromS3(string s3Key)
        {
            await _amazonS3.DeleteObjectAsync(new DeleteObjectRequest()
            {
                BucketName = Globals.StorageBucket,
                Key = s3Key
            });
        }

        /// <summary>
        /// Checks if an object exists in Amazon S3.
        /// </summary>
        /// <param name="s3Key">The key of the object to check.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a boolean value indicating if the object exists.</returns>
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