
using Newtonsoft.Json;

namespace ImageCaching
{
    public class ImageRegistry
    {
        public string Url { get; set; }

        [JsonIgnore]
        public string Id => SHA1Helper.FromString(Url);

        /// <summary>
        /// The date/time this image entry was downloaded at in UTC time
        /// </summary>
        public DateTime DownloadedAtUtc { get; set; }

        /// <summary>
        /// How long is this registry entry valid from in minutes?
        /// </summary>
        public double ValidityMinutes { get; set; }

        /// <summary>
        /// The file extension of this image asset.
        /// </summary>
        public string Extension { get; set; }

        [JsonIgnore]
        public bool IsExpired => !IsValid;

        [JsonIgnore]
        public bool IsValid => (DateTime.UtcNow - DownloadedAtUtc).TotalMinutes < ValidityMinutes;
    }
}

