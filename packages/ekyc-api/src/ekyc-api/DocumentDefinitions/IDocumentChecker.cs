using System.Collections.Generic;
using System.Threading.Tasks;
using ekyc_api.DataDefinitions;

namespace ekyc_api.DocumentDefinitions
{
    public interface IDocumentChecker
    {
        Task<DocumentTypeCheckResponse> GetDocumentType(string s3Key);

        Task<List<Landmark>> GetLandmarks(string s3Key, IDocumentDefinition documentDefinition);
    }
}