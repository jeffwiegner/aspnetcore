// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;

namespace Microsoft.Extensions.Metrics;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable RS0016 // Add public types and members to the declared API
public sealed class InstrumentRecorder<T> : IDisposable where T : struct
{
    private readonly object _lock = new object();

    private readonly string _instrumentName;
    private readonly MeterListener _meterListener;
    private readonly List<Measurement<T>> _values;

    public InstrumentRecorder(IMeterRegistry registry, string instrumentName, object? state = null)
    {
        _instrumentName = instrumentName;
        _values = new List<Measurement<T>>();
        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (registry.Contains(instrument.Meter) && instrument.Name == _instrumentName)
            {
                listener.EnableMeasurementEvents(instrument, state);
            }
        };
        _meterListener.SetMeasurementEventCallback<T>(OnMeasurementRecorded);
        _meterListener.Start();
    }

    private void OnMeasurementRecorded(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
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
#pragma warning restore RS0016 // Add public types and members to the declared API
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
