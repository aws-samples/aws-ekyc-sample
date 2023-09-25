using System.Collections.Generic;
using System.Threading.Tasks;
using ekyc_api.DataDefinitions;

namespace ekyc_api.DocumentDefinitions;

/// <summary>
///     Gets fields from an image stored in S3.
/// </summary>
public interface IFieldValueExtractor
{
    Task<Dictionary<string, string>> GetFieldValues(string s3Key, string RekognitionCustomLabelsProjectArn,
        DocumentTypes documentType);
}