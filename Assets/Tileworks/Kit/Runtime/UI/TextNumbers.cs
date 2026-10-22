using System;
using TMPro;

namespace Tileworks.Kit.UI
{
    // Puts numbers into labels without garbage: small values reuse cached strings, anything else is written into a char
    // buffer the label reads directly.
    public sealed class TextNumbers
    {
        private const int Cached = 1000;
        private static readonly string[] Small = BuildSmall();

        private char[] _buffer = new char[48];
        private int _length;

        public int Length => _length;

        public static string Of(int value) => value >= 0 && value < Cached ? Small[value] : value.ToString();

        public void Set(TMP_Text label, int value)
        {
            if (value >= 0 && value < Cached) label.text = Small[value];
            else Clear().Append(value).Apply(label);
        }

        public void Set(TMP_Text label, string prefix, int value) => Clear().Append(prefix).Append(value).Apply(label);

        // "mm:ss".
        public void SetClock(TMP_Text label, int seconds) => Clear().Append(seconds / 60, 2).Append(':').Append(seconds % 60, 2).Apply(label);

        public TextNumbers Clear()
        {
            _length = 0;
            return this;
        }

        public TextNumbers Append(char c)
        {
            Grow(1);
            _buffer[_length++] = c;
            return this;
        }

        public TextNumbers Append(string text)
        {
            if (string.IsNullOrEmpty(text)) return this;
            Grow(text.Length);
            text.CopyTo(0, _buffer, _length, text.Length);
            _length += text.Length;
            return this;
        }

        // `minDigits` pads with zeros ("05" in a timer); `grouped` puts a comma every three digits.
        public TextNumbers Append(long value, int minDigits = 1, bool grouped = false)
        {
            if (value < 0)
            {
                Append('-');
                value = -value;
            }

            var digits = 1;
            for (var v = value; v >= 10; v /= 10) digits++;
            digits = Math.Max(digits, minDigits);
            var end = _length + digits + (grouped ? (digits - 1) / 3 : 0);
            Grow(end - _length);
            var i = end;
            var written = 0;
            do
            {
                if (grouped && written > 0 && written % 3 == 0) _buffer[--i] = ',';
                _buffer[--i] = (char)('0' + value % 10);
                value /= 10;
                written++;
            } while (value > 0 || written < digits);

            _length = end;
            return this;
        }

        public void Apply(TMP_Text label) => label.SetCharArray(_buffer, 0, _length);

        public override string ToString() => new string(_buffer, 0, _length);

        private void Grow(int extra)
        {
            if (_length + extra <= _buffer.Length) return;
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _length + extra));
        }

        private static string[] BuildSmall()
        {
            var numbers = new string[Cached];
            for (var i = 0; i < numbers.Length; i++) numbers[i] = i.ToString();
            return numbers;
        }
    }
}
