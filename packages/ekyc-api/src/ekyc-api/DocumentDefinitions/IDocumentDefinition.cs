using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ekyc_api.DataDefinitions;

namespace ekyc_api.DocumentDefinitions
{
    public interface IDocumentDefinition
    {
        bool SignatureExtractionSupported { get; set; }

        bool FaceExtractionSupported { get; set; }

        bool LivenessSupported { get; set; }

        string Name { get; set; }

        NamedBoundingBox[] Landmarks { get; set; }

        NamedBoundingBox[] DataFields { get; set; }

        string RekognitionCustomLabelsProjectArn { get; set; }

        Task<Dictionary<string, string>> PostProcessFieldData(Dictionary<string, string> Values);

        Task<Dictionary<string, string>> GetFieldData(string S3Key, DocumentTypes docType);

        Task<MemoryStream> GetFace(string S3Key);
    }
}