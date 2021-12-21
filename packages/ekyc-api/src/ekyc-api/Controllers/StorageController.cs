using Amazon.S3;
using Microsoft.AspNetCore.Mvc;

namespace ekyc_api.Controllers
{
    [ApiController]
    [Route("api/storage")]
    public class StorageController : ControllerBase
    {
        private readonly IAmazonS3 _s3;

        public StorageController(IAmazonS3 s3)
        {
            _s3 = s3;
        }
    }
}