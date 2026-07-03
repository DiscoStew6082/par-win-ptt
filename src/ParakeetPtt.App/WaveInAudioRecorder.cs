using System.Runtime.InteropServices;
using ParakeetPtt.Core;

namespace ParakeetPtt.App;

internal sealed class WaveInAudioRecorder : IChunkedAudioRecorder, IDisposable
{
    private const int WaveMapper = -1;
    private const int WaveFormatPcm = 1;
    private const int CallbackFunction = 0x00030000;
    private const int WimData = 0x3C0;
    private const int BufferCount = 4;
    private const int BufferSize = 4096;
    private const int BytesPerSecond = 32000;
    private const int ChunkDurationMilliseconds = 4000;
    private const int ChunkOverlapMilliseconds = 800;
    private const int ChunkBytes = BytesPerSecond * ChunkDurationMilliseconds / 1000;
    private const int ChunkOverlapBytes = BytesPerSecond * ChunkOverlapMilliseconds / 1000;

    private readonly object _gate = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly WaveInProc _callback;
    private readonly List<WaveBuffer> _buffers = [];
    private readonly string _appData;
    private MemoryStream? _pcm;
    private IntPtr _handle;
    private DateTimeOffset _startedAt;
    private bool _recording;
    private bool _stopping;
    private bool _disposed;
    private bool _disposeRequested;
    private long _chunkStartByte;
    private int _chunkSequence;

    public event Action<double>? AudioLevelChanged;

    public event Action<RecordedAudio>? AudioChunkReady;

    public WaveInAudioRecorder()
        : this(Path.GetTempPath())
    {
    }

    public WaveInAudioRecorder(string appData)
    {
        _appData = appData;
        _callback = OnWaveData;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            await Task.Run(StartCore, cancellationToken);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private void StartCore()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_recording || _handle != IntPtr.Zero)
            {
                return;
            }
        }

        Directory.CreateDirectory(_appData);
        var format = WaveFormat.CreatePcm16KhzMono();
        ThrowIfWaveError(waveInOpen(out var handle, WaveMapper, ref format, _callback, IntPtr.Zero, CallbackFunction), "waveInOpen");
        var buffers = new List<WaveBuffer>();

        try
        {
            for (var i = 0; i < BufferCount; i++)
            {
                var buffer = new WaveBuffer(handle, BufferSize);
                buffers.Add(buffer);
                ThrowIfWaveError(waveInAddBuffer(handle, buffer.HeaderPointer, Marshal.SizeOf<WaveHdr>()), "waveInAddBuffer");
            }

            var shouldAbortStart = false;
            lock (_gate)
            {
                if (_disposed || _disposeRequested)
                {
                    shouldAbortStart = true;
                }
                else
                {
                    _pcm = new MemoryStream();
                    _startedAt = DateTimeOffset.UtcNow;
                    _chunkStartByte = 0;
                    _chunkSequence = 0;
                    _handle = handle;
                    _buffers.AddRange(buffers);
                    _recording = true;
                    _stopping = false;
                }
            }

            if (shouldAbortStart)
            {
                CleanupNative(handle, buffers);
                lock (_gate)
                {
                    _disposed = true;
                }

                return;
            }

            ThrowIfWaveError(waveInStart(handle), "waveInStart");
            CleanupStartedDeviceIfDisposeWasRequested(handle);
        }
        catch
        {
            lock (_gate)
            {
                _recording = false;
                _stopping = true;
                _handle = IntPtr.Zero;
                _buffers.Clear();
                _pcm?.Dispose();
                _pcm = null;
            }

            waveInReset(handle);
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }

            waveInClose(handle);
            throw;
        }
    }

    public async Task<RecordedAudio> StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            return await Task.Run(StopCore, cancellationToken);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private RecordedAudio StopCore()
    {
        MemoryStream? pcm;
        TimeSpan duration;
        IntPtr handle;
        List<WaveBuffer> buffers;

        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_recording || _pcm is null)
            {
                throw new InvalidOperationException("Recorder is not running.");
            }

            _recording = false;
            _stopping = true;
            handle = _handle;
            duration = DateTimeOffset.UtcNow - _startedAt;
        }

        waveInStop(handle);
        waveInReset(handle);

        lock (_gate)
        {
            pcm = _pcm;
            _pcm = null;
            buffers = [.. _buffers];
            _buffers.Clear();
            _handle = IntPtr.Zero;
            _stopping = false;
        }

        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }

        waveInClose(handle);

        var wavPath = Path.Combine(_appData, $"utterance-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.wav");
        WriteWav(wavPath, pcm?.ToArray() ?? []);
        pcm?.Dispose();
        return new RecordedAudio(wavPath, duration, DeleteAfterUse: true);
    }

    public void Dispose()
    {
        _lifecycle.Wait();
        try
        {
            DisposeCore();
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private void DisposeCore()
    {
        IntPtr handle;
        List<WaveBuffer> buffers;
        MemoryStream? pcm;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposeRequested = true;
            _disposed = true;
            _recording = false;
            _stopping = true;
            handle = _handle;
            buffers = [.. _buffers];
            _buffers.Clear();
            _handle = IntPtr.Zero;
            pcm = _pcm;
            _pcm = null;
        }

        CleanupNative(handle, buffers);

        pcm?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _disposeRequested)
        {
            throw new ObjectDisposedException(nameof(WaveInAudioRecorder));
        }
    }

    private void CleanupStartedDeviceIfDisposeWasRequested(IntPtr handle)
    {
        List<WaveBuffer>? buffers = null;
        MemoryStream? pcm = null;

        lock (_gate)
        {
            if (!_disposeRequested && !_disposed)
            {
                return;
            }

            _recording = false;
            _stopping = true;
            _handle = IntPtr.Zero;
            buffers = [.. _buffers];
            _buffers.Clear();
            pcm = _pcm;
            _pcm = null;
            _disposed = true;
        }

        CleanupNative(handle, buffers);
        pcm?.Dispose();
    }

    private static void CleanupNative(IntPtr handle, IEnumerable<WaveBuffer> buffers)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        waveInReset(handle);
        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }

        waveInClose(handle);
    }

    private void OnWaveData(IntPtr hwi, int uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
    {
        if (uMsg != WimData)
        {
            return;
        }

        double? level = null;
        PendingChunk? pendingChunk = null;
        lock (_gate)
        {
            if (_pcm is null)
            {
                return;
            }

            var header = Marshal.PtrToStructure<WaveHdr>(dwParam1);
            if (header.BytesRecorded > 0)
            {
                var data = new byte[header.BytesRecorded];
                Marshal.Copy(header.Data, data, 0, data.Length);
                _pcm.Write(data, 0, data.Length);
                level = AudioLevelCalculator.CalculatePeakLevel(data);
                pendingChunk = TryCreatePendingChunk();
            }

            if (_recording && !_stopping)
            {
                waveInAddBuffer(hwi, dwParam1, Marshal.SizeOf<WaveHdr>());
            }
        }

        if (level.HasValue)
        {
            AudioLevelChanged?.Invoke(level.Value);
        }

        if (pendingChunk is not null)
        {
            _ = Task.Run(() => WriteChunkAndPublish(pendingChunk));
        }
    }

    private PendingChunk? TryCreatePendingChunk()
    {
        if (AudioChunkReady is null || _pcm is null || _pcm.Length - _chunkStartByte < ChunkBytes)
        {
            return null;
        }

        var chunkStart = _chunkStartByte;
        var chunkEnd = _pcm.Length;
        var pcm = _pcm.ToArray();
        var chunkLength = checked((int)(chunkEnd - chunkStart));
        var chunkOffset = checked((int)chunkStart);
        var chunkBytes = new byte[chunkLength];
        Array.Copy(pcm, chunkOffset, chunkBytes, 0, chunkBytes.Length);
        _chunkStartByte = Math.Max(0, chunkEnd - ChunkOverlapBytes);
        var path = Path.Combine(_appData, $"chunk-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{_chunkSequence++:000}.wav");
        return new PendingChunk(path, chunkBytes, TimeSpan.FromSeconds((double)chunkBytes.Length / BytesPerSecond));
    }

    private void WriteChunkAndPublish(PendingChunk chunk)
    {
        try
        {
            WriteWav(chunk.Path, chunk.Pcm);
            var audio = new RecordedAudio(chunk.Path, chunk.Duration, DeleteAfterUse: true);
            var handler = AudioChunkReady;
            if (handler is null)
            {
                TryDelete(chunk.Path);
                return;
            }

            handler.Invoke(audio);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void WriteWav(string path, byte[] pcm)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)WaveFormatPcm);
        writer.Write((short)1);
        writer.Write(16000);
        writer.Write(32000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
    }

    private static void ThrowIfWaveError(int result, string operation)
    {
        if (result != 0)
        {
            throw new InvalidOperationException($"{operation} failed with WinMM error {result}.");
        }
    }

    private sealed record PendingChunk(string Path, byte[] Pcm, TimeSpan Duration);

    private sealed class WaveBuffer : IDisposable
    {
        private readonly IntPtr _handle;
        private readonly IntPtr _dataPointer;
        private readonly bool _prepared;
        private bool _disposed;
        public IntPtr HeaderPointer { get; }

        public WaveBuffer(IntPtr handle, int size)
        {
            _handle = handle;
            _dataPointer = Marshal.AllocHGlobal(size);
            HeaderPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHdr>());
            var header = new WaveHdr
            {
                Data = _dataPointer,
                BufferLength = size
            };
            try
            {
                Marshal.StructureToPtr(header, HeaderPointer, false);
                ThrowIfWaveError(waveInPrepareHeader(_handle, HeaderPointer, Marshal.SizeOf<WaveHdr>()), "waveInPrepareHeader");
                _prepared = true;
            }
            catch
            {
                Marshal.FreeHGlobal(HeaderPointer);
                Marshal.FreeHGlobal(_dataPointer);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (!_prepared || waveInUnprepareHeader(_handle, HeaderPointer, Marshal.SizeOf<WaveHdr>()) == 0)
            {
                Marshal.FreeHGlobal(HeaderPointer);
                Marshal.FreeHGlobal(_dataPointer);
                _disposed = true;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort Size;

        public static WaveFormat CreatePcm16KhzMono()
        {
            return new WaveFormat
            {
                FormatTag = WaveFormatPcm,
                Channels = 1,
                SamplesPerSec = 16000,
                AvgBytesPerSec = 32000,
                BlockAlign = 2,
                BitsPerSample = 16,
                Size = 0
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHdr
    {
        public IntPtr Data;
        public int BufferLength;
        public int BytesRecorded;
        public IntPtr User;
        public int Flags;
        public int Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    private delegate void WaveInProc(IntPtr hwi, int uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    [DllImport("winmm.dll")]
    private static extern int waveInOpen(out IntPtr hWaveIn, int uDeviceID, ref WaveFormat lpFormat, WaveInProc dwCallback, IntPtr dwInstance, int dwFlags);

    [DllImport("winmm.dll")]
    private static extern int waveInPrepareHeader(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);

    [DllImport("winmm.dll")]
    private static extern int waveInUnprepareHeader(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);

    [DllImport("winmm.dll")]
    private static extern int waveInAddBuffer(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);

    [DllImport("winmm.dll")]
    private static extern int waveInStart(IntPtr hWaveIn);

    [DllImport("winmm.dll")]
    private static extern int waveInStop(IntPtr hWaveIn);

    [DllImport("winmm.dll")]
    private static extern int waveInReset(IntPtr hWaveIn);

    [DllImport("winmm.dll")]
    private static extern int waveInClose(IntPtr hWaveIn);
}
