namespace ImageCaching;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage()
	{
		InitializeComponent();
		this.cachedImage.SourceImage = ImageSource.FromUri(new Uri("https://media.istockphoto.com/id/959866606/photo/rabbit-4-months-old-sitting-against-white-background.jpg?s=612x612&w=0&k=20&c=8yRFVDIgoXj3gCh7ckkF4gCh8JjWN967r244PQ4vFUU="));

    }

    void invalidateCacheButton_Clicked(System.Object sender, System.EventArgs e)
    {
		ImageCachingService.Instance.ClearCache();
    }
}


