using System;
using System.Collections.Generic;

namespace ekyc_api.DataDefinitions;

public class Landmark
{
    public string Name { get; set; }
    public BoundingBox BoundingBox { get; set; }
}

public class DocumentTypeCheckResponse
{
    /// <summary>
    ///     The type of document that has been detected.
    /// </summary>
    public DocumentTypes? Type { get; set; }

    /// <summary>
    ///     The bounding box of the image w.r.t. the entire image.
    /// </summary>
    public BoundingBox BoundingBox { get; set; }
}

public class HistoryItems
{
    public DateTime Time { get; set; }

    public string SessionId { get; set; }

    public string DocumentType { get; set; }

    public bool IsSuccessful { get; set; }

    public string Error { get; set; }

    public string Client { get; set; }
}

public class CreateDataRequestResponse
{
    public string RequestId { get; set; }
}

public class GetImageUrlsForSessionResponse
{
    public string SelfieUrl { get; set; }
    public string DocumentUrl { get; set; }
    public string SessionId { get; set; }
}

public class CompareDocumentWithSelfie
{
    public bool? IsSimilar { get; set; }

    public float Similarity { get; set; }
}

public enum DocumentTypes
{
    ID_KTP,
    MY_NRIC,
    AU_PASSPORT,
    KH_IC,
    PRC_PASSPORT,
    SG_PASSPORT,
    THAI_ID_FRONT,
    THAI_ID_BACK,
    GENERIC
}

public class GetFullDocumentResponse
{
    public GetFacesResponse Faces { get; set; }

    public GetLandmarksResponse Landmarks { get; set; }

    public GetFieldValuesResponse FieldValues { get; set; }
}

public class GetFieldValuesResponse
{
    public Dictionary<string, string> FieldValues { get; set; }
}

public class GetFacesResponse
{
    public string Data { get; set; }
}

public class GetLandmarksResponse
{
    public string Data { get; set; }
}

public class NamedBoundingBox
{
    public string Name { get; set; }

    public string Language { get; set; }

    public BoundingBox ExpectedBoundingBox { get; set; }

    public string RegexExpression { get; set; }
}

public class PostcodeCsvRow
{
    public string PostCode { get; set; }
    public string City { get; set; }
    public string State { get; set; }
}

public class MalaysianState
{
    public string name { get; set; }
}