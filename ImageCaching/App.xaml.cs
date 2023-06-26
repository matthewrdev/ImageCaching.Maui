namespace ImageCaching;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

        ImageCachingService.Instance.Initialise(new CustomHttpClientHandler());

        MainPage = new MainPage();
	}
}

