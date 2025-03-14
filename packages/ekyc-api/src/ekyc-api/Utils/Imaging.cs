using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.XRay.Recorder.Core;
using ekyc_api.DataDefinitions;
using ekyc_api.DocumentDefinitions;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using BoundingBox = ekyc_api.DataDefinitions.BoundingBox;
using Image = SixLabors.ImageSharp.Image;
using S3Object = Amazon.Rekognition.Model.S3Object;

namespace ekyc_api.Utils;

public class Imaging
{
    private readonly IAmazonS3 _amazonS3;
    private readonly IDocumentChecker _documentChecker;
    protected readonly IAmazonRekognition _rekognition;

    public Imaging(IAmazonS3 amazonS3, IDocumentChecker documentChecker)
    {
        _amazonS3 = amazonS3;
        _documentChecker = documentChecker;
    }

    public static async Task<Image> CloneCropImage(Image img, float top, float left, float width, float height)
    {
        var newImg = img.Clone(a => a.Resize(img.Width, img.Height));
        var x = Convert.ToInt32(left * Convert.ToSingle(newImg.Width));
        var y = Convert.ToInt32(top * Convert.ToSingle(newImg.Height));
        var widthAbs = Convert.ToInt32(width * Convert.ToSingle(newImg.Width));
        if (widthAbs + x > img.Width)
            widthAbs = img.Width - x;
        var heightAbs = Convert.ToInt32(height * Convert.ToSingle(newImg.Height));
        if (heightAbs + y > img.Height)
            heightAbs = img.Height - y;
        newImg.Mutate(i => { i.Crop(new Rectangle(x, y, widthAbs, heightAbs)); });

        return newImg;
    }

    public static async Task<string> CorrectBluriness(string inputImagePath, string outputImagePath)
    {
        using var image = new UMat(inputImagePath, ImreadModes.Color);
        var laplacian = new UMat();
        CvInvoke.Laplacian(image, laplacian, DepthType.Cv64F);

        var mean = new MCvScalar();
        var stdDev = new MCvScalar();
        CvInvoke.MeanStdDev(laplacian, ref mean, ref stdDev);

        var focusMeasure = stdDev.V0 * stdDev.V0;

        // Adjust this threshold value as required.
        var focusThreshold = 100.0;

        if (focusMeasure < focusThreshold)
        {
            // Image is considered blurry
            Console.WriteLine("Image is blurry. Attempting to sharpen...");

            var kernel = new UMat(3, 3, DepthType.Cv32F, 1);
            kernel.SetTo(new float[]
            {
                -1, -1, -1,
                -1, 9, -1,
                -1, -1, -1
            });

            CvInvoke.Filter2D(image, image, kernel, new System.Drawing.Point(-1, -1));
            image.Save(outputImagePath);
            Console.WriteLine($"Image saved to: {outputImagePath}");
            return outputImagePath;
        }

        return inputImagePath;
    }

    public static double ConvertRadiansToDegrees(double radians)
    {
        var degrees = 180 / Math.PI * radians;
        return degrees;
    }

    public static async Task<string> CropDocumentByLabel(string S3Key, string rekognitionLabel)
    {
        var _amazonS3 = new AmazonS3Client();
        var _rekognition = new AmazonRekognitionClient();

        var getObjectResponse = await _amazonS3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = Globals.StorageBucket,
            Key = S3Key
        });

        var originalImage = Image.Load(getObjectResponse.ResponseStream);

        var detectLabelsResponse = await _rekognition.DetectLabelsAsync(new DetectLabelsRequest
        {
            Image = new Amazon.Rekognition.Model.Image
            {
                S3Object = new S3Object
                {
                    Bucket = Globals.StorageBucket,
                    Name = S3Key
                }
            },
            MinConfidence = Convert.ToSingle(Globals.GetMinimumConfidence())
        });


        var matchingLabel = detectLabelsResponse.Labels
            .Where(a => a.Name.ToLower() == rekognitionLabel.ToLower())
            .SelectMany(a => a.Instances).MaxBy(a => a.BoundingBox.Height + a.BoundingBox.Width);

        if (matchingLabel == null)
        {
            Console.WriteLine(
                $"No label {rekognitionLabel} found in image, cannot crop. Returning original image key.");
            return S3Key;
        }

        // Crop the image and upload to S3
        originalImage.Mutate(i =>
        {
            i.Crop(new Rectangle(Convert.ToInt32(
                    matchingLabel.BoundingBox.Left * Convert.ToSingle(originalImage.Width)),
                Convert.ToInt32(matchingLabel.BoundingBox.Top * Convert.ToSingle(originalImage.Height)),
                Convert.ToInt32(matchingLabel.BoundingBox.Width * Convert.ToSingle(originalImage.Width)),
                Convert.ToInt32(matchingLabel.BoundingBox.Height * Convert.ToSingle(originalImage.Height))));
        });

        var outStream = new MemoryStream();
        await originalImage.SaveAsPngAsync(outStream);
        outStream.Seek(0, SeekOrigin.Begin);
        var newKey = "cropped/" + Guid.NewGuid() + "/image.png";

        await _amazonS3.PutObjectAsync(new PutObjectRequest
            { InputStream = outStream, BucketName = Globals.StorageBucket, Key = newKey });

        if (!Globals.IsRunningOnLambda) await originalImage.SaveAsPngAsync("cropped.png");

        return newKey;
    }


    public static async Task<string> CropDocument(string S3Key, string RekognitionCustomLabelsProjectArn,
        DocumentTypes documentType)
    {
        var _amazonS3 = new AmazonS3Client();
        var _rekognition = new AmazonRekognitionClient();

        var getObjectResponse = await _amazonS3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = Globals.StorageBucket,
            Key = S3Key
        });

        var originalImage = Image.Load(getObjectResponse.ResponseStream);

        var detectLabelsResponse = await _rekognition.DetectCustomLabelsAsync(new DetectCustomLabelsRequest
        {
            Image = new Amazon.Rekognition.Model.Image
            {
                S3Object = new S3Object
                {
                    Bucket = Globals.StorageBucket,
                    Name = S3Key
                }
            },
            ProjectVersionArn = RekognitionCustomLabelsProjectArn
        });

        var docType = documentType.ToString().ToLower();

        if (docType == "thai_id_front")
            docType = "thai_id";

        var matchingLabel = detectLabelsResponse.CustomLabels
            .Where(a => a.Name.ToLower() == docType && a.Confidence > 20)
            .OrderByDescending(a => a.Geometry.BoundingBox.Height + a.Geometry.BoundingBox.Width).FirstOrDefault();

        if (matchingLabel == null)
        {
            Console.WriteLine(
                $"No label {documentType.ToString()} found in image, cannot crop. Returning original image key.");
            return S3Key;
        }

        // Crop the image and upload to S3
        originalImage.Mutate(i =>
        {
            i.Crop(new Rectangle(Convert.ToInt32(
                    matchingLabel.Geometry.BoundingBox.Left * Convert.ToSingle(originalImage.Width)),
                Convert.ToInt32(matchingLabel.Geometry.BoundingBox.Top * Convert.ToSingle(originalImage.Height)),
                Convert.ToInt32(matchingLabel.Geometry.BoundingBox.Width * Convert.ToSingle(originalImage.Width)),
                Convert.ToInt32(matchingLabel.Geometry.BoundingBox.Height * Convert.ToSingle(originalImage.Height))));
        });

        var outStream = new MemoryStream();
        await originalImage.SaveAsPngAsync(outStream);
        outStream.Seek(0, SeekOrigin.Begin);
        var newKey = "cropped/" + Guid.NewGuid() + "/image.png";

        await _amazonS3.PutObjectAsync(new PutObjectRequest
            { InputStream = outStream, BucketName = Globals.StorageBucket, Key = newKey });

        return newKey;
    }

    public async Task<Image> GetImageFromStorage(string s3Key)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("Utils::GetImageFromStorage");

        if (string.IsNullOrEmpty(s3Key))
        {
            var ex = new Exception("S3 key must be provided.");
            AWSXRayRecorder.Instance.AddException(ex);
            throw ex;
        }


        var response = await _amazonS3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = Globals.StorageBucket,
            Key = s3Key
        });

        var img = Image.Load(response.ResponseStream);

        AWSXRayRecorder.Instance.EndSubsegment();

        return img;
    }

    public async Task<(DocumentTypes documentType, MemoryStream CroppedImage, BoundingBox DocumentBoundingBox)?>
        DetectAndCropDocument(
            string sourceS3Key)
    {
        AWSXRayRecorder.Instance.BeginSubsegment("Utils::DetectAndCropDocument");

        var documentType = await _documentChecker.GetDocumentType(sourceS3Key);

        if (documentType == null || documentType.Type == null)
            return null;

        // Check to make sure document isn't smaller than the minimum size

        if (documentType.BoundingBox.Height < Globals.MinDocumentHeight ||
            documentType.BoundingBox.Width < Globals.MinDocumentWidth)
            throw new Exception("Document does not meet minimum size requirements.");

        // Crop the image

        var getResponse = await _amazonS3.GetObjectAsync(new GetObjectRequest
            { BucketName = Globals.StorageBucket, Key = sourceS3Key });

        var img = Image.Load(getResponse.ResponseStream);

        var top = Convert.ToInt32(documentType.BoundingBox.Top * Convert.ToDouble(img.Height));
        var height = Convert.ToInt32(documentType.BoundingBox.Height * Convert.ToDouble(img.Height));
        height = Math.Min(height, img.Height - top);
        var left = Convert.ToInt32(documentType.BoundingBox.Left * Convert.ToDouble(img.Width));
        var width = Convert.ToInt32(documentType.BoundingBox.Width * Convert.ToDouble(img.Width));
        width = Math.Min(width, img.Width - left);

        img.Mutate(i => i.Crop(new Rectangle(left, top, width, height)));

        var ms = new MemoryStream();

        img.SaveAsPng(ms);

        AWSXRayRecorder.Instance.EndSubsegment();

        var returnVal = (documentType: documentType.Type.Value, CroppedImage: ms,
            DocumentBoundingBox: documentType.BoundingBox);

        return returnVal;
    }
}