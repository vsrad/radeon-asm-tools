namespace VSRAD.Package.Utils
{
    // Inspired by https://stackoverflow.com/a/34636692
    public sealed class Result<T>
    {
        private readonly T _result;
        private readonly Error? _error;

        public Result(T result) => _result = result;

        public Result(Error error) => _error = error;

        public bool TryGetResult(out T result, out Error error)
        {
            if (!_error.HasValue)
            {
                result = _result;
                error = default;
                return true;
            }

            result = default;
            error = _error.Value;
            return false;
        }

        public static implicit operator Result<T>(T result) => new Result<T>(result);

        public static implicit operator Result<T>(Error error) => new Result<T>(error);
    }
}
