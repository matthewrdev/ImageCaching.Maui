namespace ImageCaching
{
    public class ImageStateChangedEventArgs : EventArgs
    {
        public ImageStateChangedEventArgs(Uri imageUri,
                                          ImageState oldState,
                                          ImageState newState)
        {
            ImageUri = imageUri ?? throw new ArgumentNullException(nameof(imageUri));
            OldState = oldState;
            NewState = newState;
        }

        public Uri ImageUri { get; }

        public ImageState OldState { get; }

        public ImageState NewState { get; }

    }
}

