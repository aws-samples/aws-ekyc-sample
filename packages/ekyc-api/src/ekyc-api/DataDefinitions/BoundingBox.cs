namespace ekyc_api.DataDefinitions
{
    /// <summary>
    ///  Generic bounding box for use across solution.
    /// </summary>
    public class BoundingBox
    {
        public BoundingBox()
        {
        }

        public BoundingBox(double Top, double Left, double Width, double Height)
        {
            this.Top = Top;
            this.Left = Left;
            this.Width = Width;
            this.Height = Height;
        }

        public double Top { get; set; }

        public double Left { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        /// <summary>
        ///  Converts a Rekognition bounding box to a generic one.
        /// </summary>
        /// <param name="boundingBox"></param>
        /// <returns></returns>
        public static BoundingBox GetBoundingBoxFromRekognition(Amazon.Rekognition.Model.BoundingBox boundingBox)
        {
            var bb = new BoundingBox
            {
                Top = boundingBox.Top,
                Left = boundingBox.Left,
                Width = boundingBox.Width,
                Height = boundingBox.Height
            };

            return bb;
        }
    }
}