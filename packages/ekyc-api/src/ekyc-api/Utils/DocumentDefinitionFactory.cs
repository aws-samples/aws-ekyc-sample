using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions;

namespace ekyc_api.Utils;

public class DocumentDefinitionFactory : IDocumentDefinitionFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DocumentDefinitionFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }


    public async Task<IDocumentDefinition> GetDocumentDefinition(DocumentTypes docType)
    {
        var docDefinition = await GetDocumentDefinitionByType(docType);

        if (docDefinition == null)
            throw new ArgumentOutOfRangeException(nameof(DocumentTypes), docType,
                $"Document type {docType} is not supported.");

        return docDefinition;

        /* switch (docType)
         {
             case DocumentTypes.ID_KTP:
                 return (IDocumentDefinition) _serviceProvider.GetService(typeof(ID_KTP_DocumentDefinition));
             case DocumentTypes.MY_NRIC:
                 return (IDocumentDefinition) _serviceProvider.GetService(typeof(MY_NRIC_DocumentDefinition));
             case DocumentTypes.AU_PASSPORT:
                 return (IDocumentDefinition) _serviceProvider.GetService(typeof(AU_Passport_DocumentDefinition));
             case DocumentTypes.KH_IC:
                 return (IDocumentDefinition) _serviceProvider.GetService(typeof(KH_IC_DocumentDefinition));
             case DocumentTypes.PRC_PASSPORT:
                 return (IDocumentDefinition) _serviceProvider.GetService(typeof(PRC_Passport_DocumentDefinition));
             default:
                 throw new ArgumentOutOfRangeException(nameof(DocumentTypes), docType,
                     $"Document type {docType} is not supported.");
         }*/
    }

    [return: MaybeNull]
    public async Task<DocumentDefinitionBase> GetDocumentDefinitionByType(DocumentTypes documentType)
    {
        var jsonPath = "./DocumentDefinitions/documentdefinitions.json";

        if (!File.Exists(jsonPath))
            throw new Exception("Document definition Json file not found.");

        string strJson;

        using (var sr = File.OpenText(jsonPath))
        {
            strJson = await sr.ReadToEndAsync();
        }

        var definitions = JsonSerializer.Deserialize<DocumentDefinitionsContainer>(strJson);

        if (definitions == null)
            throw new Exception("Error deserializing document definitions.");

        var definitionInstances = new List<DocumentDefinitionBase>();

        foreach (var T in
                 Assembly.GetAssembly(typeof(DocumentDefinitionBase)).GetTypes()
                     .Where(myType =>
                         myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(DocumentDefinitionBase))))
        {
            var dd = (DocumentDefinitionBase)_serviceProvider.GetService(T);
            if (dd == null)
                throw new Exception(
                    $"Could not instantiate service {T} - did you add it to ConfigServices?");
            definitionInstances.Add(dd);
        }

        var documentDefinition = definitionInstances.FirstOrDefault(a => a.DocumentType == documentType);

        if (documentDefinition == null)
            throw new Exception($"Document type {documentType} is not supported.");

        var documentTypeDTO = definitions.DocumentTypes
            .FirstOrDefault(a =>
                string.Equals(a.Name, documentType.ToString(), StringComparison.CurrentCultureIgnoreCase));

        documentDefinition.Landmarks = documentTypeDTO.Landmarks;
        documentDefinition.Name = documentTypeDTO.Name;
        documentDefinition.DataFields = documentTypeDTO.DataFields;
        documentDefinition.LivenessSupported = documentTypeDTO.LivenessSupported;
        documentDefinition.FaceExtractionSupported = documentTypeDTO.FaceExtractionSupported;
        documentDefinition.SignatureExtractionSupported = documentTypeDTO.SignatureExtractionSupported;

        return documentDefinition;
    }
}

public interface IDocumentDefinitionFactory
{
    public Task<IDocumentDefinition> GetDocumentDefinition(DocumentTypes docType);
}