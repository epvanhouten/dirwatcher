namespace DirWatcher
{
    using System;

    public class WatcherFileStateChange
    {
        public WatcherFileStateChange(WatcherFileState oldState, WatcherFileState newState)
        {
            if (oldState == null && newState == null)
            {
                throw new ArgumentException("One of the states must be non null");
            }

            this.OldState = oldState;
            this.NewState = newState;
            this.Path = oldState?.FileIdentifier.Path ??
                        newState?.FileIdentifier.Path;
            this.Name = oldState?.FileIdentifier.Name ??
                        newState?.FileIdentifier.Name;
        }

        public WatcherFileState NewState { get; }

        public string Path { get; }

        public StateChangeActionEnum Action
        {
            get
            {
                if (this.OldState == null)
                {
                    return StateChangeActionEnum.New;
                }

                if (this.NewState == null)
                {
                    return StateChangeActionEnum.Deleted;
                }

                if (this.OldState.NumberOfLines != this.NewState.NumberOfLines)
                {
                    return StateChangeActionEnum.Changed;
                }

                return StateChangeActionEnum.None;
            }
        }

        private WatcherFileState OldState { get; }

        private string Name { get; }

        public override string ToString()
        {
            switch (this.Action)
            {
                case StateChangeActionEnum.New:
                    return $"{this.Name} {this.NewState.NumberOfLines}";
                case StateChangeActionEnum.Deleted:
                    return $"{this.Name}";
                case StateChangeActionEnum.Changed:
                    var changeSize = this.NewState.NumberOfLines - this.OldState.NumberOfLines;
                    var changeSign = changeSize > 0 ? "+" : "-";
                    return $"{this.Name} {changeSign}{changeSize}";
                case StateChangeActionEnum.None:
                    return string.Empty;
                default:
                    return string.Empty;
            }
        }
    }
}