namespace DirWatcher
{
    public class WatcherFileState
    {
        public WatcherFileState(WatcherFileIdentifier identifier, int numberOfLines)
        {
            this.FileIdentifier = identifier;
            this.NumberOfLines = numberOfLines;
        }

        public WatcherFileIdentifier FileIdentifier { get; }

        public int NumberOfLines { get; }
    }
}