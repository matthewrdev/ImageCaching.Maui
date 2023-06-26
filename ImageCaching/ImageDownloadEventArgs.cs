using System.Net;

namespace ImageCaching
{
    public class ImageDownloadEventArgs : EventArgs
    {
        public ImageDownloadEventArgs(Uri imageUri, HttpStatusCode? httpStatusCode = null)
        {
            ImageUri = imageUri ?? throw new ArgumentNullException(nameof(imageUri));
            HttpStatusCode = httpStatusCode;
        }

        /// <summary>
        /// The <see cref="Uri"/> of the image.
        /// </summary>
        public Uri ImageUri { get; }

        /// <summary>
        /// An optional http status code of the download operation.
        /// </summary>
        public HttpStatusCode? HttpStatusCode { get; }
    }
}

