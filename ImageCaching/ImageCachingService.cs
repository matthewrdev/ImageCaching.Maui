using System;
using System.Net.Http;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using ImageCaching;

namespace ImageCaching
{
    public class ImageCachingService
	{
        public static readonly ImageCachingService Instance = new ImageCachingService();

        private const string imageCacheRegistryFileExtension = ".json";

        private const string cacheFolderName = "ImageCache";
        public string CacheFolderPath { get; } = Path.Combine(FileSystem.Current.CacheDirectory, cacheFolderName);

        private HttpClientHandler httpClientHandler;
        private HttpClient httpClient;
        private TimeSpan imageCacheRetentionLimit;

        private readonly object imageDownloadingMapLock = new object();
        private readonly Dictionary<string, string> imageDownloadingMap = new Dictionary<string, string>();

        private readonly List<WeakReference<IImageEventsCallback>> imageEventReferences = new List<WeakReference<IImageEventsCallback>>();

        public event EventHandler<ImageStateChangedEventArgs> CachedImageStateChanged;

        public event EventHandler<ImageDownloadEventArgs> DownloadStarted;
        public event EventHandler<ImageDownloadEventArgs> DownloadCompleted;
        public event EventHandler<ImageDownloadEventArgs> DownloadFailed;

        public ImageCachingService()
		{
        }

        public void RegisterEventsCallback(IImageEventsCallback callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var exists = imageEventReferences.Any(r => r.TryGetTarget(out var innerReference) && innerReference == callback);
            if (exists)
            {
                return;
            }

            imageEventReferences.Add(new WeakReference<IImageEventsCallback>(callback));

            CleanupDetachedCallbackReferences();
        }

        private void CleanupDetachedCallbackReferences()
        {
            var toRemove = new List<WeakReference<IImageEventsCallback>>();

            foreach (var reference in imageEventReferences)
            {
                if (!reference.TryGetTarget(out _))
                {
                    toRemove.Add(reference);
                }
            }

            if (toRemove.Any())
            {
                foreach (var reference in toRemove)
                {
                    imageEventReferences.Remove(reference);
                }
            }
        }

        public void Initialise(HttpClientHandler httpClientHandler = null,
                               TimeSpan? imageCacheRetentionLimit = null)
        {
            this.httpClientHandler = httpClientHandler ?? new HttpClientHandler();
            this.httpClient = new HttpClient(this.httpClientHandler);

            if (!Directory.Exists(CacheFolderPath))
            {
                Directory.CreateDirectory(CacheFolderPath);
            }

            this.imageCacheRetentionLimit = imageCacheRetentionLimit ?? TimeSpan.FromDays(3);

            Task.Run(() => CleanupRegistry());
        }

        private void CleanupRegistry(bool deleteAll =  false)
        {
            var registryEntries = Directory.GetFiles(CacheFolderPath, "*" + imageCacheRegistryFileExtension);
            if (registryEntries == null || registryEntries.Length == 0)
            {
                return;
            }

            foreach (var imageRegistryFilePath in registryEntries)
            {
                ImageRegistry registry = null;
                try
                {
                    var fileContents = File.ReadAllText(imageRegistryFilePath);
                    registry = JsonConvert.DeserializeObject<ImageRegistry>(fileContents);
                }
                catch { }

                if (registry == null
                    || registry.IsExpired
                    || deleteAll)
                {
                    var imageId = Path.GetFileNameWithoutExtension(imageRegistryFilePath);

                    DeleteRegistryEntry(imageId);
                    if (registry != null)
                    {
                        NotifyCachingStateChanged(new Uri(registry.Url), ImageState.Cached, ImageState.Uncached);
                    }
                }
            }
        }

        public void ScheduleImageDownload(Uri imageUrl, bool force = false)
        {
            if (imageUrl is null)
            {
                throw new ArgumentNullException(nameof(imageUrl));
            }

            if (IsImageDownloading(imageUrl))
            {
                Console.WriteLine($"The image '{imageUrl}' is already downloading");
                return;
            }

            var registry = GetImageRegistry(imageUrl);
            if (!force && registry != null)
            {
                if (registry.IsExpired)
                {
                    DeleteRegistryEntry(imageUrl);
                    registry = null;
                }
                else // valid cached image, just return.
                {
                    return;
                }
            }

            var imageId = GetImageId(imageUrl);
            var registryFilePath = Path.Combine(CacheFolderPath, imageId + imageCacheRegistryFileExtension);
            var previousState = registry != null ? ImageState.Cached : ImageState.Uncached;
            registry = new ImageRegistry()
            {
                DownloadedAtUtc = DateTime.UtcNow,
                Extension = ".png",
                Url = imageUrl.ToString(),
                ValidityMinutes = imageCacheRetentionLimit.TotalMinutes,
            };

            Task.Run(async () =>
            {
                await DownloadImage(imageUrl, registry, registryFilePath, previousState);
            });
        }

        private async Task DownloadImage(Uri imageUrl,
                                         ImageRegistry registry,
                                         string registryFilePath,
                                         ImageState previousState)
        {
            try
            {
                lock (imageDownloadingMapLock)
                {
                    imageDownloadingMap[imageUrl.ToString()] = imageUrl.ToString();
                }


                NotifyCachingStateChanged(imageUrl, previousState, ImageState.Downloading);
                NotifyDownloadStarted(imageUrl);

                using (var response = await httpClient.GetAsync(imageUrl))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var content = response.Content;
                        var imageOutputPath = Path.Combine(CacheFolderPath, registry.Id + registry.Extension);
                        using (var fileStream = File.OpenWrite(imageOutputPath))
                        {
                            await content.CopyToAsync(fileStream);
                        }

                        File.WriteAllText(registryFilePath, JsonConvert.SerializeObject(registry));

                        NotifyCachingStateChanged(imageUrl, ImageState.Downloading, ImageState.Cached);
                        NotifyDownloadCompleted(imageUrl, response.StatusCode);
                    }
                    else
                    {
                        NotifyCachingStateChanged(imageUrl, ImageState.Downloading, ImageState.DownloadFailed);
                        NotifyDownloadFailed(imageUrl, response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                lock (imageDownloadingMapLock)
                {
                    if (imageDownloadingMap.ContainsKey(imageUrl.ToString()))
                    {
                        imageDownloadingMap.Remove(imageUrl.ToString());
                    }
                }
            }
        }

        private void NotifyCachingStateChanged(Uri imageUrl, ImageState oldState, ImageState newState)
        {
            try
            {
                this.CachedImageStateChanged?.Invoke(this, new ImageStateChangedEventArgs(imageUrl, oldState, newState));

                foreach (var reference in imageEventReferences)
                {
                    if (reference.TryGetTarget(out var callback) && callback != null)
                    {
                        callback.CachedImageStateChanged(imageUrl, oldState, newState);
                    }
                }

                CleanupDetachedCallbackReferences();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void NotifyDownloadStarted(Uri imageUrl)
        {
            try
            {
                this.DownloadStarted?.Invoke(this, new ImageDownloadEventArgs(imageUrl));

                foreach (var reference in imageEventReferences)
                {
                    if (reference.TryGetTarget(out var callback) && callback != null)
                    {
                        callback.DownloadStarted(imageUrl);
                    }
                }

                CleanupDetachedCallbackReferences();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void NotifyDownloadCompleted(Uri imageUrl, System.Net.HttpStatusCode statusCode)
        {
            try
            {
                this.DownloadCompleted?.Invoke(this, new ImageDownloadEventArgs(imageUrl, statusCode));

                foreach (var reference in imageEventReferences)
                {
                    if (reference.TryGetTarget(out var callback) && callback != null)
                    {
                        callback.DownloadCompleted(imageUrl, statusCode);
                    }
                }

                CleanupDetachedCallbackReferences();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void NotifyDownloadFailed(Uri imageUrl, System.Net.HttpStatusCode statusCode)
        {
            try
            {
                this.DownloadFailed?.Invoke(this, new ImageDownloadEventArgs(imageUrl, statusCode));

                foreach (var reference in imageEventReferences)
                {
                    if (reference.TryGetTarget(out var callback) && callback != null)
                    {
                        callback.DownloadFailed(imageUrl, statusCode);
                    }
                }

                CleanupDetachedCallbackReferences();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public bool IsImageDownloading(Uri imageUrl)
        {
            if (imageUrl is null)
            {
                throw new ArgumentNullException(nameof(imageUrl));
            }

            var imageId = GetImageId(imageUrl);

            lock (imageDownloadingMapLock)
            {
                return imageDownloadingMap.ContainsKey(imageId);
            }
        }

        public ImageState GetImageState(Uri imageUrl)
        {
            if (imageUrl is null)
            {
                throw new ArgumentNullException(nameof(imageUrl));
            }

            if (IsImageDownloading(imageUrl))
            {
                return ImageState.Downloading;
            }

            var imageRegistry = GetImageRegistry(imageUrl);
            if (imageRegistry is null)
            {
                return ImageState.Uncached;
            }

            var imageId = GetImageId(imageUrl);
            var cachedImageFilePath = Path.Combine(CacheFolderPath, imageId + imageRegistry.Extension);

            // If a registry file exists, however, no file, then the state is failed.
            if (!File.Exists(cachedImageFilePath))
            {
                return ImageState.DownloadFailed;
            }

            if (imageRegistry.IsExpired)
            {
                DeleteRegistryEntry(imageId);
                return ImageState.Uncached;
            }

            return ImageState.Cached;
        }

        public bool IsImageCached(Uri imageUrl)
        {
            if (imageUrl is null)
            {
                throw new ArgumentNullException(nameof(imageUrl));
            }

            return GetImageState(imageUrl) == ImageState.Cached;
        }

        public bool IsImageExpired(Uri imageUrl)
        {
            if (imageUrl is null)
            {
                throw new ArgumentNullException(nameof(imageUrl));
            }

            var registry = GetImageRegistry(imageUrl);
            if (registry is null)
            {
                // Consider uncached images always expired
                return true;
            }

                return registry.IsExpired;
        }

        private void DeleteRegistryEntry(Uri imageUri)
        {
            if (imageUri is null)
            {
                throw new ArgumentNullException(nameof(imageUri));
            }

            var imageId = GetImageId(imageUri);
            DeleteRegistryEntry(imageId);
        }

        private void DeleteRegistryEntry(string imageId)
        {
            if (string.IsNullOrWhiteSpace(imageId))
            {
                throw new ArgumentException($"'{nameof(imageId)}' cannot be null or whitespace.", nameof(imageId));
            }

            Console.WriteLine($"Deleting expired cached image '{imageId}'");

            var imageRegistryFilePath = Path.Combine(CacheFolderPath, imageId + imageCacheRegistryFileExtension);
            if (File.Exists(imageRegistryFilePath))
            {
                File.Delete(imageRegistryFilePath);
            }

            var cachedImageFilePath = Path.Combine(CacheFolderPath, imageId + ".png"); // TODO: Consider the extension here?
            if (File.Exists(cachedImageFilePath))
            {
                File.Delete(cachedImageFilePath);
            }
        }

        public ImageRegistry GetImageRegistry(Uri imageUrl)
        {
            if (imageUrl is null)
            {
                throw new ArgumentNullException(nameof(imageUrl));
            }

            var imageId = GetImageId(imageUrl);
            var imageRegistryFilePath = Path.Combine(CacheFolderPath, imageId + imageCacheRegistryFileExtension);
            if (!File.Exists(imageRegistryFilePath))
            {
                return null;
            }

            ImageRegistry imageRegistry = null;
            try
            {
                var fileContents = File.ReadAllText(imageRegistryFilePath);
                imageRegistry = JsonConvert.DeserializeObject<ImageRegistry>(fileContents);
            }
            catch { }

            if (imageRegistry == null)
            {
                // Malformed image registry, kill it.
                DeleteRegistryEntry(imageId);
            }

            return imageRegistry;
        }

        public string GetImageId(Uri imageUrl)
        {
            if (imageUrl is null)
            {
                throw new ArgumentNullException(nameof(imageUrl));
            }

            return SHA1Helper.FromString(imageUrl.ToString());
        }

        public string GetImageFilePath(Uri imageUrl)
        {
            if (imageUrl is null)
            {
                throw new ArgumentNullException(nameof(imageUrl));
            }

            var registry = this.GetImageRegistry(imageUrl);
            if (registry == null)
            {
                return string.Empty;
            }

            var imagePath = Path.Combine(CacheFolderPath, registry.Id + registry.Extension);
            return imagePath;
        }

        public void ClearCache()
        {
            CleanupRegistry(deleteAll: true);
        }
    }
}

