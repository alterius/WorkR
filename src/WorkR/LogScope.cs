using System.Collections;

namespace WorkR
{
    internal readonly struct LogScope : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly KeyValuePair<string, object?>[] _values;

        public LogScope(params KeyValuePair<string, object?>[] values)
        {
            _values = values;
        }

        public LogScope(string key, object? value)
            : this([new(key, value)])
        {
        }

        public int Count => _values.Length;

        public KeyValuePair<string, object?> this[int index] => _values[index];

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
            ((IEnumerable<KeyValuePair<string, object?>>)_values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() =>
            $"{{ {string.Join(", ", _values.Select(kvp => $"{kvp.Key} = {kvp.Value}"))} }}";
    }
}
