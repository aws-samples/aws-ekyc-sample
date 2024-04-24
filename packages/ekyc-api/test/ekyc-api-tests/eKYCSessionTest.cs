using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Rekognition;
using Amazon.S3;
using ekyc_api.Controllers;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions;
using ekyc_api.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace ekyc_api_tests;

[TestFixture]
public class eKYCSessionTest : TestBase
{
    [SetUp]
    public void SetUp()
    {
        // put other startup code here
        _logger = TestHost.Services.GetService<ILogger<LivenessController>>();
        _documentControllerLogger = TestHost.Services.GetService<ILogger<DocumentController>>();
        _sessionControllerLogger = TestHost.Services.GetService<ILogger<SessionController>>();
        _amazonDynamoDb = TestHost.Services.GetService<IAmazonDynamoDB>();
        _dbContext = new DynamoDBContext(_amazonDynamoDb);
        _amazonRekognition = TestHost.Services.GetService<IAmazonRekognition>();
        _config = TestHost.Services.GetService<IConfiguration>();
        _livenessChecker = TestHost.Services.GetService<ILivenessChecker>();
        _documentDefinitionFactory = TestHost.Services.GetService<IDocumentDefinitionFactory>();
        _documentChecker = TestHost.Services.GetService<IDocumentChecker>();
        _s3Client = TestHost.Services.GetService<IAmazonS3>();
        _livenessController =
            new LivenessController(_config, _s3Client, _amazonDynamoDb, _amazonRekognition, _livenessChecker, _logger);
        _sessionController =
            new SessionController(_config, _s3Client, _amazonDynamoDb, _livenessChecker, _documentChecker,
                _sessionControllerLogger);
        _documentController = new DocumentController(_config, _documentDefinitionFactory, _s3Client,
            _amazonDynamoDb, _documentChecker, _documentControllerLogger);
    }

    private ILogger<LivenessController> _logger;

    private ILogger<SessionController> _sessionControllerLogger;

    private ILogger<DocumentController> _documentControllerLogger;

    private IConfiguration _config;

    private LivenessController _livenessController;

    private IAmazonS3 _s3Client;

    private IAmazonDynamoDB _amazonDynamoDb;

    private IAmazonRekognition _amazonRekognition;

    private IDocumentDefinitionFactory _documentDefinitionFactory;

    private IDocumentChecker _documentChecker;

    private ILivenessChecker _livenessChecker;

    private SessionController _sessionController;

    private DocumentController _documentController;

    private DynamoDBContext _dbContext;


    [Test]
    public async Task TestDrawRectangle()
    {
        var sessionId = "fad57c75-bc21-4a19-a7e8-33d2294cdd5a";

        await DrawRectangleOnSelfie(sessionId);
    }

    public async Task DrawRectangleOnSelfie(string sessionId)
    {
        // var sessionId = "fad57c75-bc21-4a19-a7e8-33d2294cdd5a";

        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var session = await _dbContext.LoadAsync<SessionObject>(sessionId, config);


        Assert.NotNull(session.nosePointAreaLeft);

        Assert.NotNull(session.nosePointAreaTop);

        Image img;
        await using (var fileStream =
                     new FileStream("../../../SampleData/nose-point.jpg", FileMode.Open, FileAccess.Read))
        {
            img = await Image.LoadAsync(fileStream);
        }

        var x = Convert.ToDouble(img.Width) * session.nosePointAreaLeft.Value;
        var y = Convert.ToDouble(img.Height) * session.nosePointAreaTop.Value;
        var width = Convert.ToDouble(img.Width) * Globals.NosePointAreaDimensions;
        var height = Convert.ToDouble(img.Height) * Globals.NosePointAreaDimensions;


        var rect = new RectangleF(Convert.ToSingle(x), Convert.ToSingle(y), Convert.ToSingle(width),
            Convert.ToSingle(height));

        img = img.Clone(x => x.Draw(Color.Red, 3f, rect));

        img.SaveAsJpeg($"../../../SampleData/nosepoint/nose-point-{session.Id}.jpg");
    }

    private async Task<Mat> CaptureImage()
    {
        using var capture = new VideoCapture(0);
        if (!capture.IsOpened())
            return null;

        capture.FrameWidth = 1920;
        capture.FrameHeight = 1280;
        capture.AutoFocus = true;

        const int sleepTime = 10;

        using var window = new Window("capture");
        var image = new Mat();

        while (true)
        {
            capture.Read(image);
            if (image.Empty())
                break;

            window.ShowImage(image);
            var c = Cv2.WaitKey(sleepTime);
            if (c >= 0) break;
        }

        return image;
    }

    public async Task UpdateSessionNosePointCoordinates(string sessionId, double top, double left)
    {
        var config = new DynamoDBOperationConfig
        {
            OverrideTableName = Globals.SessionTableName
        };

        var session = await _dbContext.LoadAsync<SessionObject>(sessionId, config);

        // Need to set the coordinates of the nose point rectangle so that the test can go through
        session.nosePointAreaLeft = left;
        session.nosePointAreaTop = top;

        await _dbContext.SaveAsync(session, config);

        Assert.NotNull(session.nosePointAreaLeft);

        Assert.NotNull(session.nosePointAreaTop);
    }

    private async Task<LivenessController.VerifyLivenessResponse> EndToEndTest(string selfieFileName,
        string documentFileName, string eyesClosedFileName, string nosePointFileName,
        double nosePointTop = 0.55d, double nosePointLeft = 0.45d)
    {
        Console.WriteLine($"Selfie: {selfieFileName}");

        Console.WriteLine($"Document File Name: {documentFileName}");

        Console.WriteLine($"Eyes Closed File Name: {eyesClosedFileName}");

        Console.WriteLine($"Nose Point File Name: {nosePointFileName}");

        SessionController.NewSessionResponse session;

        session = await _sessionController.StartNewSession();

        Assert.IsNotNull(session);

        Assert.IsNotNull(session.Id);

        Console.WriteLine($"New session started: {session.Id}");

        session.noseBoundsTop = nosePointTop;

        session.noseBoundsLeft = nosePointLeft;

        await UpdateSessionNosePointCoordinates(session.Id, nosePointTop, nosePointLeft);


        Console.WriteLine(
            $"Nose point dimensions: X - {session.noseBoundsLeft} Y - {session.noseBoundsTop} - Width - {session.noseBoundsWidth} - Height - {session.noseBoundsHeight}");

        await DrawRectangleOnSelfie(session.Id);

        // Document

        var strDocumentName = documentFileName; //"passport.jpg";

        var url = await _sessionController.GetPresignedPutUrl(session.Id, strDocumentName);

        await PutObjectAtUrl(url, $"../../../SampleData/{strDocumentName}");

        await _sessionController.SubmitDocumentForVerification(session.Id, strDocumentName, "AU_Passport");


        // Selfie

        strDocumentName = selfieFileName; // "selfie-yaw.jpg";

        url = await _sessionController.GetPresignedPutUrl(session.Id, strDocumentName);

        await PutObjectAtUrl(url, $"../../../SampleData/{strDocumentName}");

        await _sessionController.SubmitSelfie(session.Id, strDocumentName);

        // Eyes closed

        strDocumentName = eyesClosedFileName; // "selfie-eyesclosed.jpg";

        url = await _sessionController.GetPresignedPutUrl(session.Id, strDocumentName);

        await PutObjectAtUrl(url, $"../../../SampleData/{strDocumentName}");

        await _sessionController.SubmitEyesClosedFace(session.Id, strDocumentName);

        // Nose pointing

        strDocumentName = nosePointFileName; // "nose-point.jpg";

        url = await _sessionController.GetPresignedPutUrl(session.Id, strDocumentName);

        await PutObjectAtUrl(url, $"../../../SampleData/{strDocumentName}");

        await _sessionController.SubmitFaceWithNosePointing(session.Id, strDocumentName);

        var resp = await _livenessController.VerifyLiveness(session.Id);


        Console.WriteLine(JsonConvert.SerializeObject(resp));

        return resp;
    }

    [Test]
    public async Task TestCompareSelfie()
    {
        SessionController.NewSessionResponse session;

        session = await _sessionController.StartNewSession();

        Assert.IsNotNull(session);

        Assert.IsNotNull(session.Id);

        Console.WriteLine($"New session started: {session.Id}");

        // Document

        var strDocumentName = "passport.jpg"; //"passport.jpg";

        var url = await _sessionController.GetPresignedPutUrl(session.Id, strDocumentName);

        await PutObjectAtUrl(url, $"../../../SampleData/{strDocumentName}");

        await _sessionController.SubmitDocumentForVerification(session.Id, strDocumentName, "AU_Passport");


        // Selfie

        strDocumentName = "selfie.jpg"; // "selfie-yaw.jpg";

        url = await _sessionController.GetPresignedPutUrl(session.Id, strDocumentName);

        await PutObjectAtUrl(url, $"../../../SampleData/{strDocumentName}");

        await _sessionController.SubmitSelfie(session.Id, strDocumentName);

        var response = await _sessionController.CompareDocumentWithSelfie(session.Id);

        Assert.IsTrue(response.IsSimilar);

        // Wrong document

        strDocumentName = "passport_wrong.jpg"; //"passport.jpg";

        url = await _sessionController.GetPresignedPutUrl(session.Id, strDocumentName);

        await PutObjectAtUrl(url, $"../../../SampleData/{strDocumentName}");

        await _sessionController.SubmitDocumentForVerification(session.Id, strDocumentName, "AU_Passport");

        response = await _sessionController.CompareDocumentWithSelfie(session.Id);

        Assert.IsFalse(response.IsSimilar);

        // Reset document

        strDocumentName = "passport.jpg"; //"passport.jpg";

        url = await _sessionController.GetPresignedPutUrl(session.Id, strDocumentName);

        await PutObjectAtUrl(url, $"../../../SampleData/{strDocumentName}");

        await _sessionController.SubmitDocumentForVerification(session.Id, strDocumentName, "AU_Passport");

        // Wrong selfie

        strDocumentName = "selfie-wrong.jpg"; // "selfie-yaw.jpg";

        url = await _sessionController.GetPresignedPutUrl(session.Id, strDocumentName);

        await PutObjectAtUrl(url, $"../../../SampleData/{strDocumentName}");

        await _sessionController.SubmitSelfie(session.Id, strDocumentName);

        response = await _sessionController.CompareDocumentWithSelfie(session.Id);

        Assert.IsFalse(response.IsSimilar);
    }

    [Test]
    public async Task TestCheckDocumentType()
    {
        var strFilesToCheck = new[]
        {
            "KTP/Picture 11.jpg", "KTP/Picture 12.jpg", "KTP/Picture 14.jpg", "myKAD/345.jpg", "myKAD/front_25.jpg"
        };

        foreach (var fileToCheck in strFilesToCheck)
        {
            var session = await _sessionController.StartNewSession();

            var fi = new FileInfo($"../../../SampleData/{fileToCheck}");

            var s3Key = Guid.NewGuid() + fi.Extension;

            var presignedUrl = await _sessionController.GetPresignedPutUrl(session.Id, s3Key);

            var httpRequest = WebRequest.Create(presignedUrl) as HttpWebRequest;
            httpRequest.Method = "PUT";
            using (var dataStream = httpRequest.GetRequestStream())
            {
                var buffer = new byte[8000];
                await using (var fileStream = new FileStream(fi.FullName, FileMode.Open,
                                 FileAccess.Read))
                {
                    var bytesRead = 0;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                        await dataStream.WriteAsync(buffer, 0, bytesRead);
                }
            }

            var response = httpRequest.GetResponse() as HttpWebResponse;

            if (response != null) Console.WriteLine("Upload response: " + response.StatusCode);

            var detectResponse = await _documentController.DetectAndSetDocumentType(session.Id, s3Key);

            if (detectResponse != null)
                Console.WriteLine($"Detected {fileToCheck} as {detectResponse}");
            else
                Console.WriteLine($"Unable to detect {fileToCheck}");
        }
    }

    [Test]
    public async Task TestSingleSession()
    {
    }

    [Test]
    public async Task TestEndToEnd()
    {
        var existingSession = false;

        var response = new LivenessController.VerifyLivenessResponse();

        Console.WriteLine("Testing for selfie nose too high case");
        // Nose too high
        try
        {
            response = await EndToEndTest("selfie-pitch.jpg", "passport.jpg", "selfie-eyesclosed.jpg",
                "nose-point.jpg");
        }
        catch (Exception ex)
        {
            response.IsLive = false;
            response.Error = ex.Message;
        }

        Assert.IsFalse(response.IsLive);

        Console.WriteLine(response.Error);

        // Head tilt
        Console.WriteLine("Testing for selfie head pitching case");

        try
        {
            response = await EndToEndTest("selfie-roll.jpg", "passport.jpg", "selfie-eyesclosed.jpg",
                "nose-point.jpg");
        }
        catch (Exception ex)
        {
            response.IsLive = false;
            response.Error = ex.Message;
        }

        Assert.IsFalse(response.IsLive);

        Console.WriteLine(response.Error);

        // Head turned
        Console.WriteLine("Testing for selfie head turn case");

        try
        {
            response = await EndToEndTest("selfie-yaw.jpg", "passport.jpg", "selfie-eyesclosed.jpg",
                "nose-point.jpg");
        }
        catch (Exception ex)
        {
            response.IsLive = false;
            response.Error = ex.Message;
        }

        Assert.IsFalse(response.IsLive);

        Console.WriteLine(response.Error);

        Console.WriteLine("Testing for selfie eyes open case");
        // Eyes open
        try
        {
            response = await EndToEndTest("selfie.jpg", "passport.jpg", "selfie.jpg", "nose-point.jpg");
        }
        catch (Exception ex)
        {
            response.IsLive = false;
            response.Error = ex.Message;
        }

        Assert.IsFalse(response.IsLive);

        Console.WriteLine(response.Error);

        Console.WriteLine("Testing for wrong face on document case");

        // Passport face wrong

        try
        {
            response = await EndToEndTest("selfie.jpg", "passport_wrong.jpg", "selfie-eyesclosed.jpg",
                "nose-point.jpg");
        }
        catch (Exception ex)
        {
            response.IsLive = false;
            response.Error = ex.Message;
        }

        Assert.IsFalse(response.IsLive);

        Console.WriteLine(response.Error);

        // Wrong nose point pose case
        Console.WriteLine("Testing for nose pointing at area case");

        try
        {
            response = await EndToEndTest("selfie.jpg", "passport.jpg", "selfie-eyesclosed.jpg",
                "nosepoint-wrong.jpg", 0.6, 0.4);
        }
        catch (Exception ex)
        {
            response.IsLive = false;
            response.Error = ex.Message;
        }

        Assert.IsFalse(response.IsLive);

        Console.WriteLine(response.Error);


        // Positive case
        Console.WriteLine("Testing for positive case");

        try
        {
            response = await EndToEndTest("selfie.jpg", "passport.jpg", "selfie-eyesclosed.jpg", "nose-point.jpg",
                0.7, 0.65);
        }
        catch (Exception ex)
        {
            response.IsLive = false;
            response.Error = ex.Message;
        }

        Assert.IsTrue(response.IsLive);

        Console.WriteLine(response.Error);
    }
}