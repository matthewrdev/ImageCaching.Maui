using System;
using System.Net;

namespace ImageCaching
{
	public interface IImageEventsCallback
    {
        void CachedImageStateChanged(Uri imageUri, ImageState oldState, ImageState newState);
        void DownloadStarted(Uri imageUri);
        void DownloadCompleted(Uri imageUri, HttpStatusCode httpStatusCode);
        void DownloadFailed(Uri imageUri, HttpStatusCode httpStatusCode);
    }
}

