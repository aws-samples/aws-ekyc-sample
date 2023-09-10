using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Textract;
using Amazon.Textract.Model;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ekyc_api.DocumentDefinitions.THAI_ID;

public class Thai_ID_FieldValueExtractor : IFieldValueExtractor
{
    
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    private readonly IAmazonTextract _textractClient;

    private List<Block> Blocks;

    public Thai_ID_FieldValueExtractor(IAmazonTextract textractClient, IConfiguration config,
        ILogger logger)
    {
        _config = config;
        _logger = logger;
        _textractClient = textractClient;
    }

    
    public async Task<Dictionary<string, string>> GetFieldValues(string s3Key)
    {
        
        // Step 1. Use bounding box model to extract ID

        
        
        // Step 2. Use Textract to obtain bounding boxes of known “anchor” points

        var detectTextResponse = await _textractClient.DetectDocumentTextAsync(new DetectDocumentTextRequest()
        {
            Document = new Document()
            {
                S3Object = new S3Object()
                {
                    Bucket = Globals.StorageBucket,
                    Name = s3Key
                }
            }
        });
        

        // Step 3. Using anchor points, we can derive adjustment for:
        // - Rotation
        // - Perspective
        // - Size
        
        // Step 4. – Once ID perspective is adjusted, we can use fixed coordinates to extract bounding boxes to feed into Tesseract


        
        throw new System.NotImplementedException();
    }
}