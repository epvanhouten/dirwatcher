using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DirWatcher
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (o, s) => { cts.Cancel(); };
            var pathToWatch = args[0];
            var pattern = args[1];
            

            var outstandingTasks = new List<Task>();
            var waitDuration = TimeSpan.FromSeconds(10);
            var oldState = await InitializeDirectoryState(ScanDirectory(pathToWatch, pattern).Values, cts.Token);
            //Init your watcher
            var timerTask = WaitMessageAsync(waitDuration, cts.Token);

            while (!cts.Token.IsCancellationRequested)
            {
                if (timerTask.IsCompleted)
                {
                    //Fire up the new timer
                    timerTask = WaitMessageAsync(waitDuration, cts.Token);
                    outstandingTasks.Add(timerTask);

                    //Start working on your new answers
                    var newState = ScanDirectory(pathToWatch, pattern);
                    var changes = GetChangedFiles(oldState, newState, cts.Token);
                    outstandingTasks.AddRange(changes);
                }

                //If any of the work you ask for is done, this will return
                var completedTask = await Task.WhenAny(outstandingTasks);
                outstandingTasks.Remove(completedTask);

                //Unwrap the message and push it to the console
                if (completedTask is Task<string> stringTask)
                {
                    var message = await stringTask;
                    Console.WriteLine(message);
                }
                else if (completedTask is Task<WatcherFileStateChange> stateChangeTask)
                {
                    var stateChange = await stateChangeTask;
                    Console.WriteLine(stateChange.ToString());
                }
            }
        }

        private static async Task<Dictionary<string, WatcherFileState>> InitializeDirectoryState(IEnumerable<WatcherFileIdentifier> identifiers, CancellationToken cancellationToken)
        {
            var taskList = identifiers.Select(i => GetFileLineCount(i, cancellationToken));
            return (await Task.WhenAll(taskList)).ToDictionary(wfs => wfs.FileIdentifier.Path);
        }

        private static IEnumerable<Task<WatcherFileStateChange>> GetChangedFiles(Dictionary<string, WatcherFileState> oldState,
                                                                                 Dictionary<string, WatcherFileIdentifier> newIdentifiers,
                                                                                 CancellationToken cancellationToken)
        {
            var filesInBoth = oldState.Keys.Union(newIdentifiers.Keys);
            foreach (var file in filesInBoth)
            {
                if (oldState[file].FileIdentifier.ModifiedTime < newIdentifiers[file].ModifiedTime)
                {
                    yield return GetChangedStateAsync(oldState[file], newIdentifiers[file], cancellationToken);
                }
            }

            foreach (var deleted in oldState.Keys.Except(newIdentifiers.Keys))
            {
                yield return Task.FromResult(new WatcherFileStateChange(oldState[deleted], null));
            }

            foreach (var newFile in newIdentifiers.Keys.Except(oldState.Keys))
            {
                yield return GetChangedStateAsync(null, newIdentifiers[newFile], cancellationToken);
            }
        }

        private static async Task<WatcherFileStateChange> GetChangedStateAsync(WatcherFileState oldState, WatcherFileIdentifier newIdentifier, CancellationToken cancellationToken)
        {
            var newState = await GetFileLineCount(newIdentifier, cancellationToken);
            return new WatcherFileStateChange(oldState, newState);
        }

        private static async Task<string> WaitMessageAsync(TimeSpan waitDuration, CancellationToken cancellationToken)
        {
            // Don't access the console down here, just do it in main
            await Task.Delay(waitDuration, cancellationToken);
            return "10 second check in";
        }

        private static Dictionary<string, WatcherFileIdentifier> ScanDirectory(string path, string pattern)
        {
            return (from file in Directory.EnumerateFiles(path, pattern) 
                        let modifiedTime = File.GetLastWriteTimeUtc(file)
                        select new WatcherFileIdentifier(file, modifiedTime)
                       ).ToDictionary(w => w.Path);
        }

        private static async Task<WatcherFileState> GetFileLineCount(WatcherFileIdentifier fileIdentifier, CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(fileIdentifier.Path);
            var lines = await CountLines(stream, cancellationToken);
            return new WatcherFileState(fileIdentifier, lines);
        }

        //https://github.com/NimaAra/Easy.Common/blob/master/Easy.Common/Extensions/StreamExtensions.cs#L46
        public static async Task<int> CountLines(Stream stream, CancellationToken cancellationToken)
        {
            if(stream == null)
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
            while ((bytesRead = (await stream.ReadAsync(byteBuffer, 0, byteBuffer.Length, cancellationToken))) > 0)
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

        private class WatcherFileIdentifier
        {
            public string Path { get; }
            public string Name => System.IO.Path.GetFileName(Path);
            public DateTime ModifiedTime { get; }

            public WatcherFileIdentifier(string path, DateTime modifiedTime)
            {
                Path = path;
                ModifiedTime = modifiedTime;
            }
        }

        private class WatcherFileState
        {
            public WatcherFileIdentifier FileIdentifier { get; }
            public int NumberOfLines { get; }

            public WatcherFileState(WatcherFileIdentifier identifier, int numberOfLines)
            {
                FileIdentifier = identifier;
                NumberOfLines = numberOfLines;
            }
        }

        private class WatcherFileStateChange
        {
            private WatcherFileState OldState { get; }
            private WatcherFileState NewState { get; }

            private string Name { get; }

            public WatcherFileStateChange(WatcherFileState oldState, WatcherFileState newState)
            {
                if (oldState == null && newState == null)
                {
                    throw new ArgumentException("One of the states must be non null");
                }

                OldState = oldState;
                NewState = newState;
                Name = oldState?.FileIdentifier.Name ??
                       newState?.FileIdentifier.Name;
            }

            private StateChangeActionEnum Action
            {
                get
                {
                    if (OldState == null)
                    {
                        return StateChangeActionEnum.New;
                    }
                    if (NewState == null)
                    {
                        return StateChangeActionEnum.Deleted;
                    }
                    if (OldState.NumberOfLines != NewState.NumberOfLines)
                    {
                        return StateChangeActionEnum.Changed;
                    }
                    return StateChangeActionEnum.None;
                }
            }

            public override string ToString()
            {
                switch (Action)
                {
                    case StateChangeActionEnum.New:
                        return $"{Name} {NewState.NumberOfLines}";
                    case StateChangeActionEnum.Deleted:
                        return $"{Name}";
                    case StateChangeActionEnum.Changed:
                        return $"{Name} {NewState.NumberOfLines - OldState.NumberOfLines}";
                    case StateChangeActionEnum.None:
                        return string.Empty;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private enum StateChangeActionEnum
        {
            New,
            Deleted,
            Changed,
            None
        }
    }
}
