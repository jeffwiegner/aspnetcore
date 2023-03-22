// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;

namespace Microsoft.AspNetCore.Hosting;

public readonly struct Measurement<T>
{
    public Measurement(T value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        Value = value;
        Tags = tags.ToArray();
    }

    public T Value { get; }
    public IReadOnlyList<KeyValuePair<string, object>> Tags { get; }
}

public sealed class InstrumentRecorder<T> : IDisposable where T : struct
{
    private readonly object _lock = new object();

    private readonly string _instrumentName;
    private readonly MeterListener _meterListener;
    private readonly List<Measurement<T>> _values;

    public InstrumentRecorder(Meter meter, string instrumentName)
    {
        _instrumentName = instrumentName;
        _values = new List<Measurement<T>>();
        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter == meter && instrument.Name == _instrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<T>(OnMeasurementRecorded);
        _meterListener.Start();
    }

    private void OnMeasurementRecorded(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
    {
        lock (_lock)
        {
            _values.Add(new Measurement<T>(measurement, tags));
        }
    }

    public IReadOnlyList<Measurement<T>> GetMeasurements()
    {
        lock (_lock)
        {
            return _values.ToArray();
        }
    }

    public void Dispose()
    {
        _meterListener.Dispose();
    }
}
