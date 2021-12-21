using System.Threading.Tasks;
using SixLabors.ImageSharp;

namespace ekyc_api.Utils
{
    public interface ILivenessChecker
    {
        /// <summary>
        ///     Get the number of faces in the target image that match the source.
        /// </summary>
        /// <param name="sourceKey">The key of the source image in S3.</param>
        /// <param name="targetKey">The key of the target image to compare with in S3.</param>
        /// <returns></returns>
        Task<(bool IsMatch, float Confidence)> CompareFaces(string sourceKey, string targetKey);

        /// <summary>
        ///     Verifies that an image meets the minimum size for liveness verification.
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        Task<bool> VerifyImageSize(Image img);

        /// <summary>
        ///     Verifies liveness of the face in the document being checked. To be called after all image upload methods are
        ///     successful.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        Task<string> VerifyImageLiveness(string sessionId);

        /// <summary>
        ///     Verifies that the face in an image has open eyes.
        /// </summary>
        /// <param name="S3Key"></param>
        /// <returns></returns>
        Task<bool> VerifyEyesOpen(string S3Key);

        /// <summary>
        ///     Verifies the pose of the selfie that is submitted.
        /// </summary>
        /// <param name="s3Key"></param>
        /// <returns></returns>
        Task<string> VerifySelfieFacePose(string s3Key);

        /// <summary>
        ///     Checks if the face is in the centre of an image.
        /// </summary>
        /// <param name="s3Key"></param>
        /// <returns></returns>
        Task<bool> VerifyFaceIsInCentre(string s3Key);
    }
}