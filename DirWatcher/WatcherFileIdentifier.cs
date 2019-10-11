namespace DirWatcher
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class WatcherFileIdentifier
    {
        public WatcherFileIdentifier(string path, DateTime modifiedTime)
        {
            this.Path = path;
            this.ModifiedTime = modifiedTime;
        }

        public string Path { get; }

        public string Name => System.IO.Path.GetFileName(this.Path);

        public DateTime ModifiedTime { get; }

        public async Task<WatcherFileState> GetFileLineCount(CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(this.Path);
            var lines = await CountLines(stream, cancellationToken).ConfigureAwait(false);
            return new WatcherFileState(this, lines);
        }

        // https://github.com/NimaAra/Easy.Common/blob/master/Easy.Common/Extensions/StreamExtensions.cs#L46
        private static async Task<int> CountLines(Stream stream, CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            const char lf = '\n';
            const char cr = '\r';
            const char NULL = (char)0;

            var lineCount = 0;

            var byteBuffer = new byte[1024 * 1024];
            var detectedEol = NULL;
            var currentChar = NULL;

            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(byteBuffer, 0, byteBuffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                for (var i = 0; i < bytesRead; i++)
                {
                    currentChar = (char)byteBuffer[i];

                    if (detectedEol != NULL)
                    {
                        if (currentChar == detectedEol)
                        {
                            lineCount++;
                        }
                    }
                    else if (currentChar == lf || currentChar == cr)
                    {
                        detectedEol = currentChar;
                        lineCount++;
                    }
                }
            }

            if (currentChar != lf && currentChar != cr && currentChar != NULL)
            {
                lineCount++;
            }

            return lineCount;
        }
    }
}