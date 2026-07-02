using System.Runtime.InteropServices;
using ParakeetPtt.Core;

namespace ParakeetPtt.App;

internal sealed class WaveInAudioRecorder : IAudioRecorder, IDisposable
{
    private const int WaveMapper = -1;
    private const int WaveFormatPcm = 1;
    private const int CallbackFunction = 0x00030000;
    private const int WomData = 0x3C0;
    private const int BufferCount = 4;
    private const int BufferSize = 4096;

    private readonly object _gate = new();
    private readonly WaveInProc _callback;
    private readonly List<WaveBuffer> _buffers = [];
    private readonly string _appData;
    private MemoryStream? _pcm;
    private IntPtr _handle;
    private DateTimeOffset _startedAt;
    private bool _recording;

    public WaveInAudioRecorder()
        : this(Path.GetTempPath())
    {
    }

    public WaveInAudioRecorder(string appData)
    {
        _appData = appData;
        _callback = OnWaveData;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_recording)
            {
                return Task.CompletedTask;
            }

            Directory.CreateDirectory(_appData);
            _pcm = new MemoryStream();
            _startedAt = DateTimeOffset.UtcNow;
            var format = WaveFormat.CreatePcm16KhzMono();
            ThrowIfWaveError(waveInOpen(out _handle, WaveMapper, ref format, _callback, IntPtr.Zero, CallbackFunction), "waveInOpen");

            for (var i = 0; i < BufferCount; i++)
            {
                var buffer = new WaveBuffer(_handle, BufferSize);
                _buffers.Add(buffer);
                ThrowIfWaveError(waveInAddBuffer(_handle, buffer.HeaderPointer, Marshal.SizeOf<WaveHdr>()), "waveInAddBuffer");
            }

            _recording = true;
            ThrowIfWaveError(waveInStart(_handle), "waveInStart");
            return Task.CompletedTask;
        }
    }

    public Task<RecordedAudio> StopAsync(CancellationToken cancellationToken)
    {
        MemoryStream pcm;
        TimeSpan duration;

        lock (_gate)
        {
            if (!_recording || _pcm is null)
            {
                throw new InvalidOperationException("Recorder is not running.");
            }

            _recording = false;
            waveInStop(_handle);
            waveInReset(_handle);
            duration = DateTimeOffset.UtcNow - _startedAt;
            pcm = _pcm;
            _pcm = null;

            foreach (var buffer in _buffers)
            {
                buffer.Dispose();
            }

            _buffers.Clear();
            waveInClose(_handle);
            _handle = IntPtr.Zero;
        }

        var wavPath = Path.Combine(_appData, $"utterance-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.wav");
        WriteWav(wavPath, pcm.ToArray());
        pcm.Dispose();
        return Task.FromResult(new RecordedAudio(wavPath, duration, DeleteAfterUse: true));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _recording = false;
            if (_handle != IntPtr.Zero)
            {
                waveInReset(_handle);
                foreach (var buffer in _buffers)
                {
                    buffer.Dispose();
                }

                _buffers.Clear();
                waveInClose(_handle);
                _handle = IntPtr.Zero;
            }

            _pcm?.Dispose();
            _pcm = null;
        }
    }

    private void OnWaveData(IntPtr hwi, int uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
    {
        if (uMsg != WomData)
        {
            return;
        }

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
            }

            if (_recording)
            {
                waveInAddBuffer(hwi, dwParam1, Marshal.SizeOf<WaveHdr>());
            }
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

    private sealed class WaveBuffer : IDisposable
    {
        private readonly IntPtr _handle;
        private readonly IntPtr _dataPointer;
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
            Marshal.StructureToPtr(header, HeaderPointer, false);
            ThrowIfWaveError(waveInPrepareHeader(_handle, HeaderPointer, Marshal.SizeOf<WaveHdr>()), "waveInPrepareHeader");
        }

        public void Dispose()
        {
            waveInUnprepareHeader(_handle, HeaderPointer, Marshal.SizeOf<WaveHdr>());
            Marshal.FreeHGlobal(HeaderPointer);
            Marshal.FreeHGlobal(_dataPointer);
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
