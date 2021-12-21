namespace ekyc_api.DataDefinitions
{
    public class DocumentDefinitionsContainer
    {
        public DocumentDefinitionDTO[] DocumentTypes { get; set; }
    }

    public class DocumentDefinitionDTO
    {
        public NamedBoundingBox[] Landmarks { get; set; }

        public NamedBoundingBox[] DataFields { get; set; }

        public bool LivenessSupported { get; set; }

        public bool FaceExtractionSupported { get; set; }

        public bool SignatureExtractionSupported { get; set; }

        public string Name { get; set; }
    }
}