using System;

namespace Tileworks.Kit.Core
{
    public enum ErrorKind : byte
    {
        None = 0,
        Validation,
        NotFound,
        InsufficientFunds,
        Conflict,
        Cancelled,
        Io,
        Corrupt,
        Unexpected,
    }

    public readonly struct Error : IEquatable<Error>
    {
        public readonly ErrorKind Kind;
        public readonly string Code;
        public readonly string Message;

        public Error(ErrorKind kind, string code, string message = null)
        {
            Kind = kind;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool IsNone => Kind == ErrorKind.None;

        public static Error Validation(string code, string message = null) => new Error(ErrorKind.Validation, code, message);
        public static Error NotFound(string code, string message = null) => new Error(ErrorKind.NotFound, code, message);
        public static Error Unexpected(string code, string message = null) => new Error(ErrorKind.Unexpected, code, message);

        public bool Equals(Error other) => Kind == other.Kind && Code == other.Code;
        public override bool Equals(object obj) => obj is Error other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Code?.GetHashCode() ?? 0);
        public override string ToString() => Message.Length == 0 ? $"{Kind}:{Code}" : $"{Kind}:{Code} ({Message})";
    }

    public readonly struct Result
    {
        public readonly Error Error;

        private Result(Error error) => Error = error;

        public bool IsOk => Error.IsNone;
        public static Result Ok() => default;
        public static Result Fail(Error error) => new Result(error);
        public static implicit operator Result(Error error) => new Result(error);
        public override string ToString() => IsOk ? "Ok" : Error.ToString();
    }

    public readonly struct Result<T>
    {
        private readonly T _value;
        public readonly Error Error;

        private Result(T value, Error error)
        {
            _value = value;
            Error = error;
        }

        public bool IsOk => Error.IsNone;

        public T Value
        {
            get
            {
                if (!IsOk) throw new InvalidOperationException($"Result has no value: {Error}");
                return _value;
            }
        }

        public bool TryGet(out T value)
        {
            value = _value;
            return IsOk;
        }

        public static Result<T> Ok(T value) => new Result<T>(value, default);
        public static Result<T> Fail(Error error) => new Result<T>(default, error);
        public static implicit operator Result<T>(Error error) => new Result<T>(default, error);
        public override string ToString() => IsOk ? $"Ok({_value})" : Error.ToString();
    }
}
