namespace DirWatcher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (args.Length != 2)
            {
                Console.WriteLine(GetUsage());
            }

            using var cts = new CancellationTokenSource();
            var pathToWatch = args[0];
            var pattern = args[1];

            var outstandingTasks = new List<Task>();
            var waitDuration = TimeSpan.FromSeconds(10);
            var oldState = await InitializeDirectoryState(ScanDirectory(pathToWatch, pattern).Values, cts.Token).ConfigureAwait(false);

            // Init your watcher
            var timerTask = WaitMessageAsync(waitDuration, cts.Token);
            var endingTask = TerminateOnKeyPressAsync(cts);
            outstandingTasks.Add(timerTask);
            outstandingTasks.Add(endingTask);

            while (!cts.Token.IsCancellationRequested)
            {
                if (timerTask.IsCompleted)
                {
                    // Fire up the new timer
                    timerTask = WaitMessageAsync(waitDuration, cts.Token);
                    outstandingTasks.Add(timerTask);

                    // Start working on your new answers
                    var newState = ScanDirectory(pathToWatch, pattern);
                    var changes = GetChangedFiles(oldState, newState, cts.Token);
                    outstandingTasks.AddRange(changes);
                }

                // If any of the work you ask for is done, this will return
                var completedTask = await Task.WhenAny(outstandingTasks).ConfigureAwait(false);
                outstandingTasks.Remove(completedTask);

                switch (completedTask)
                {
                    // Unwrap the message and push it to the console
                    case Task<string> stringTask:
                    {
                        var message = await stringTask.ConfigureAwait(false);
                        Console.WriteLine(message);
                        break;
                    }

                    case Task<WatcherFileStateChange> stateChangeTask:
                    {
                        var stateChange = await stateChangeTask.ConfigureAwait(false);
                        oldState = ApplyStateChange(oldState, stateChange);
                        Console.WriteLine(stateChange.ToString());
                        break;
                    }
                }
            }
        }

        private static string GetUsage()
        {
            return $"{Assembly.GetExecutingAssembly().GetName().FullName} <Path to watch> <File filter>";
        }

        private static async Task TerminateOnKeyPressAsync(CancellationTokenSource cts)
        {
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cts.Token).ConfigureAwait(false);
                if (Console.KeyAvailable)
                {
                    cts.Cancel();
                }
            }
        }

        private static Dictionary<string, WatcherFileState> ApplyStateChange(Dictionary<string, WatcherFileState> oldState, WatcherFileStateChange stateChange)
        {
            var newState = new Dictionary<string, WatcherFileState>(oldState);

            switch (stateChange.Action)
            {
                case StateChangeActionEnum.New:
                    newState.Add(stateChange.Path, stateChange.NewState);
                    break;
                case StateChangeActionEnum.Deleted:
                    newState.Remove(stateChange.Path);
                    break;
                case StateChangeActionEnum.Changed:
                    newState[stateChange.Path] = stateChange.NewState;
                    break;
                case StateChangeActionEnum.None:
                    break;
            }

            return newState;
        }

        private static async Task<Dictionary<string, WatcherFileState>> InitializeDirectoryState(IEnumerable<WatcherFileIdentifier> identifiers, CancellationToken cancellationToken)
        {
            var taskList = identifiers.Select(i => i.GetFileLineCount(cancellationToken));
            return (await Task.WhenAll(taskList).ConfigureAwait(false)).ToDictionary(wfs => wfs.FileIdentifier.Path);
        }

        private static IEnumerable<Task<WatcherFileStateChange>> GetChangedFiles(
                                                                    Dictionary<string, WatcherFileState> oldState,
                                                                    Dictionary<string, WatcherFileIdentifier> newIdentifiers,
                                                                    CancellationToken cancellationToken)
        {
            var filesInBoth = oldState.Keys.Intersect(newIdentifiers.Keys);
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
            var newState = await newIdentifier.GetFileLineCount(cancellationToken).ConfigureAwait(false);
            return new WatcherFileStateChange(oldState, newState);
        }

        private static async Task<string> WaitMessageAsync(TimeSpan waitDuration, CancellationToken cancellationToken)
        {
            // Don't access the console down here, just do it in main
            await Task.Delay(waitDuration, cancellationToken).ConfigureAwait(false);
            return "10 second check in";
        }

        private static Dictionary<string, WatcherFileIdentifier> ScanDirectory(string path, string pattern)
        {
            return (from file in Directory.EnumerateFiles(path, pattern)
                        let modifiedTime = File.GetLastWriteTimeUtc(file)
                        select new WatcherFileIdentifier(file, modifiedTime))
                    .ToDictionary(w => w.Path);
        }
    }
}
