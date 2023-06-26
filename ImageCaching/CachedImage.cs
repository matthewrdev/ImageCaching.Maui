using System.Net;
using Microsoft.Maui.Controls;

namespace ImageCaching;

public class CachedImage : ContentView, IImageEventsCallback
{
    public static readonly BindableProperty SourceImageProperty = BindableProperty.Create(nameof(SourceImage), typeof(ImageSource), typeof(CachedImage), default(ImageSource), propertyChanged: OnSourceImageChanged);
    public ImageSource SourceImage
    {
        get => (ImageSource)GetValue(SourceImageProperty);
        set => SetValue(SourceImageProperty, value);
    }

    static void OnSourceImageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CachedImage control)
        {
            control.Apply(newValue as ImageSource, control.PlaceholderImage, control.ErrorImage);
        }
    }

    private void Apply(ImageSource sourceImage, ImageSource placeholderImage, ImageSource errorImage)
    {
        if (sourceImage is UriImageSource uriImageSource)
        {
            var imageUri = uriImageSource.Uri;
            var state = ImageCachingService.Instance.GetImageState(imageUri);
            ApplyImageState(imageUri, state, placeholderImage, errorImage);

            if (state == ImageState.Uncached)
            {
                ImageCachingService.Instance.ScheduleImageDownload(imageUri);
            }
        }
        else
        {
            this.ImageView.Source = sourceImage;
        }
    }

    public static readonly BindableProperty PlaceholderImageProperty = BindableProperty.Create(nameof(PlaceholderImage), typeof(ImageSource), typeof(CachedImage), default(ImageSource), propertyChanged: OnPlaceholderImageChanged);
    public ImageSource PlaceholderImage
    {
        get => (ImageSource)GetValue(PlaceholderImageProperty);
        set => SetValue(PlaceholderImageProperty, value);
    }

    static void OnPlaceholderImageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CachedImage control)
        {
            control.Apply(control.SourceImage,
                          newValue as ImageSource,
                          control.ErrorImage);
        }
    }    public static readonly BindableProperty ErrorImageProperty = BindableProperty.Create(nameof(ErrorImage), typeof(ImageSource), typeof(CachedImage), default(ImageSource), propertyChanged: OnErrorImageChanged);
    public ImageSource ErrorImage
    {
        get => (ImageSource)GetValue(ErrorImageProperty);
        set => SetValue(ErrorImageProperty, value);
    }

    static void OnErrorImageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CachedImage control)
        {
            control.Apply(control.SourceImage,
                          control.PlaceholderImage,
                          newValue as ImageSource);
        }
    }

    public void CachedImageStateChanged(Uri imageUri, ImageState oldState, ImageState newState)
    {
        if (ImageUri != imageUri)
        {
            return;
        }

        ApplyImageState(imageUri, newState, this.PlaceholderImage, this.ErrorImage);
    }

    private void ApplyImageState(Uri imageUri,
                                 ImageState imageState,
                                 ImageSource placeholderImage,
                                 ImageSource errorImage)
    {
        switch (imageState)
        {
            case ImageState.Cached:
                var imagePath = ImageCachingService.Instance.GetImageFilePath(imageUri);
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    // TODO: Log here?
                    this.ImageView.Source = errorImage;
                }
                else
                {
                    this.ImageView.Source = FileImageSource.FromFile(imagePath);
                }
                break;
            case ImageState.Uncached:
                this.ImageView.Source = placeholderImage;
                break;
            case ImageState.Downloading:
                this.ImageView.Source = placeholderImage;
                break;
            case ImageState.DownloadFailed:
                this.ImageView.Source = errorImage;
                break;
        }
    }

    public void DownloadStarted(Uri imageUri)
    {
    }

    public void DownloadCompleted(Uri imageUri, HttpStatusCode httpStatusCode)
    {
    }

    public void DownloadFailed(Uri imageUri, HttpStatusCode httpStatusCode)
    {
    }

    protected Image ImageView => Content as Image;

    public Uri ImageUri => (this.SourceImage as UriImageSource)?.Uri;

    public CachedImage()
	{
		Content = new Image();

        ImageCachingService.Instance.RegisterEventsCallback(this);

        Apply(this.SourceImage, this.PlaceholderImage, this.ErrorImage);
    }
}
