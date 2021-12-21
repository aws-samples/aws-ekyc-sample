using System;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;

namespace ekyc_api.Utils
{
    public static class S3Utils
    {
        public static IServiceProvider ServiceProvider;


        public static async Task DeleteFromS3(string key)
        {
            var _s3 = ServiceProvider.GetRequiredService<IAmazonS3>();

            await _s3.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = Globals.StorageBucket,
                Key = key
            });
        }
    }
}