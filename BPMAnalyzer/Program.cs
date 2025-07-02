using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Channels;
using NAudio.Wave;

namespace BPMAnalyzer;

class Program
{
    const int FrameSize = 1024;

    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: BPMAnalyzer <filename>.wav/.mp3");
            return;
        }

        var fullFileName = args[0];

        var (data, waveFormat) = GetWave16Data(fullFileName);
        var channelData = GetChannelData(data, waveFormat, 0);
        var channelData2 = GetChannelData(data, waveFormat, 1);
        channelData = channelData.Select((p, i) => (p + channelData2[i]) / 2).ToArray();

        channelData = HighPassFilter(channelData, 800, waveFormat.SampleRate);

        var framesPerSecond = (double)waveFormat.SampleRate / FrameSize;
        var sampleCount = channelData.Length / FrameSize;

        var dft = DFT(channelData, sampleCount, framesPerSecond);
        var fft = FFT(channelData, sampleCount, waveFormat.SampleRate);

        Console.WriteLine($@"File: {Path.GetFileName(fullFileName)}
DFT: {dft}
FFT: {fft}");

        Console.ReadKey();
    }

    static (byte[] data, WaveFormat waveFormat) GetWave16Data(string fullFileName)
    {
        var extension = Path.GetExtension(fullFileName)?.ToLower();

        using WaveStream reader = extension switch
        {
            ".wav" => new WaveFileReader(fullFileName),
            ".mp3" => new Mp3FileReader(fullFileName),
            _ => throw new InvalidDataException("Incorrect Format.")
        };
        var waveProvider = reader.ToSampleProvider().ToWaveProvider();
        var waveFormat = waveProvider.WaveFormat;

        using var memoryStream = new MemoryStream();
        var buffer = new byte[waveProvider.WaveFormat.AverageBytesPerSecond];

        int bytesRead;
        while ((bytesRead = waveProvider.Read(buffer, 0, buffer.Length)) > 0)
        {
            memoryStream.Write(buffer, 0, bytesRead);
        }

        return (memoryStream.ToArray(), waveFormat);
    }

    static double[] GetChannelData(byte[] data, WaveFormat waveFormat, int channelIndex)
    {
        if (channelIndex >= waveFormat.Channels || channelIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(channelIndex), "通道索引超出范围");

        var bytesPerSample = waveFormat.BitsPerSample / 8;
        var frameSize = waveFormat.Channels * bytesPerSample;

        var totalFrames = data.Length / frameSize;
        var result = new double[totalFrames];

        for (var i = 0; i < totalFrames; i++)
        {
            var sampleOffset = i * frameSize + channelIndex * bytesPerSample;
            switch (waveFormat.BitsPerSample)
            {
                case 8: // 8-bit PCM
                    var b = data[sampleOffset];
                    result[i] = (b - 128) / 128.0;
                    break;

                case 16: // 16-bit PCM
                    var s16 = (short)(data[sampleOffset] | (data[sampleOffset + 1] << 8));
                    result[i] = s16 / 32768.0;
                    break;

                case 24: // 24-bit PCM
                    var sample24 = (data[sampleOffset + 2] << 24) | (data[sampleOffset + 1] << 16) | (data[sampleOffset] << 8);
                    result[i] = sample24 / 2147483648.0;
                    break;

                case 32:
                    if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        var f = BitConverter.ToSingle(data, sampleOffset);
                        result[i] = f;
                    }
                    else // 32-bit PCM
                    {
                        var s32 = BitConverter.ToInt32(data, sampleOffset);
                        result[i] = s32 / 2147483648.0;
                    }
                    break;

                default:
                    throw new NotSupportedException($"暂不支持的位深: {waveFormat.BitsPerSample}");
            }
        }

        return result;
    }

    static double DFT(double[] channelData, int sampleCount, double framesPerSecond)
    {
        var volumes = new double[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var sumSq = 0d;
            var start = i * FrameSize;
            for (var j = 0; j < FrameSize; j++)
                sumSq += channelData[start + j] * channelData[start + j];
            volumes[i] = Math.Sqrt(sumSq / FrameSize);
        }

        var prev = 0.0;
        var diff = (from v in volumes let temp = prev select Math.Max((prev = v) - temp, 0.0)).ToArray();

        var indices = Enumerable.Range(0, diff.Length).AsParallel();
        var r = (from i in Enumerable.Range(0, 181)
                 let freq = (i + 60) / 60.0
                 let theta = 2.0 * Math.PI * freq / framesPerSecond
                 let cosSum = indices.Sum(index => HannWindow(index, diff.Length) * Math.Cos(theta * index) * diff[index]) / sampleCount
                 let sinSum = indices.Sum(index => HannWindow(index, diff.Length) * Math.Sin(theta * index) * diff[index]) / sampleCount
                 select new { A = cosSum, B = sinSum, R = Math.Sqrt(cosSum * cosSum + sinSum * sinSum) }).ToArray();

        var peak = FindPeak(r.Select(obj => obj.R).ToArray());

        return peak + 60;

        static double HannWindow(int i, int size)
        {
            return i;
            //return 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / size);
        }

        static int FindPeak(double[] graph)
        {
            var obj = from i in Enumerable.Range(0, graph.Length - 1)
                      select new { Diff = graph[i + 1] - graph[i], Prev = i == 0 ? 0 : graph[i] - graph[i - 1], GraphValue = graph[i], Index = i };

            var indices = from o in obj
                          where o.Diff <= 0 && o.Prev > 0
                          orderby o.GraphValue descending
                          select o.Index;
            return indices.FirstOrDefault();
        }
    }

    static double FFT(double[] channelData, int sampleCount, int sampleRate)
    {
        var volumes = new double[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var sumSq = 0d;
            var start = i * FrameSize;
            for (var j = 0; j < FrameSize; j++)
                sumSq += channelData[start + j] * channelData[start + j];
            volumes[i] = Math.Sqrt(sumSq / FrameSize);
        }

        var windowed = volumes.Select((v, i) => v * (0.5 - 0.5 * Math.Cos((2 * Math.PI * i) / volumes.Length))).ToArray();

        var fftSize = 1 << (int)Math.Ceiling(Math.Log2(windowed.Length));
        var fftInput = new double[fftSize];
        for (var i = 0; i < windowed.Length; i++)
            fftInput[i] = windowed[i];

        var fft = FFTHelper.FFT(fftInput);

        var magnitudes = new double[fftSize];
        for (var i = 0; i < fftSize; i++)
        {
            magnitudes[i] = Math.Sqrt(fft[i].Real * fft[i].Real + fft[i].Imaginary * fft[i].Imaginary);
        }

        var rmsSampleRate = sampleRate / FrameSize;
        var minBpm = 60;
        var maxBpm = 240;
        var minHz = minBpm / 60.0;
        var maxHz = maxBpm / 60.0;

        var minIndex = (int)Math.Floor(minHz * fftSize / rmsSampleRate);
        var maxIndex = (int)Math.Ceiling(maxHz * fftSize / rmsSampleRate);

        // 1. 查找最大幅值的 bin
        var peakIndex = minIndex;
        for (var i = minIndex + 1; i <= maxIndex; i++)
        {
            if (magnitudes[i] > magnitudes[peakIndex])
            {
                peakIndex = i;
            }
        }

        // 2. 二次插值：计算 sub-bin 偏移 delta
        var delta = 0d;
        if (peakIndex > 0 && peakIndex < magnitudes.Length - 1)
        {
            var magL = magnitudes[peakIndex - 1];
            var magC = magnitudes[peakIndex];
            var magR = magnitudes[peakIndex + 1];

            var denominator = (magL - 2 * magC + magR);
            if (denominator != 0)
            {
                delta = 0.5 * (magL - magR) / denominator;
            }
        }

        // 3. 插值后的频率与 BPM 计算
        var interpolatedIndex = peakIndex + delta;
        var freqHz = interpolatedIndex * (rmsSampleRate / (double)fftSize);
        var bpm = freqHz * 60;
        return (int)(bpm * 1000) / 1000d;
    }

    public static double[] LowPassFilter(double[] input, double cutoffFreq, int sampleRate)
    {
        var output = new double[input.Length];

        double RC = 1.0 / (2 * Math.PI * cutoffFreq);
        double dt = 1.0 / sampleRate;
        double alpha = dt / (RC + dt);

        output[0] = input[0]; // 初始化

        for (int i = 1; i < input.Length; i++)
        {
            output[i] = output[i - 1] + alpha * (input[i] - output[i - 1]);
        }

        return output;
    }

    public static double[] HighPassFilter(double[] input, double cutoffFreq, int sampleRate)
    {
        var output = new double[input.Length];

        double RC = 1.0 / (2 * Math.PI * cutoffFreq);
        double dt = 1.0 / sampleRate;
        double alpha = RC / (RC + dt);

        output[0] = input[0]; // 初始化
        for (int i = 1; i < input.Length; i++)
        {
            output[i] = alpha * (output[i - 1] + input[i] - input[i - 1]);
        }

        return output;
    }
}

public static class FFTHelper
{
    /// <summary>
    /// 计算 FFT。输入长度必须是 2 的幂。
    /// </summary>
    public static Complex[] FFT(double[] input)
    {
        var n = input.Length;
        if ((n & (n - 1)) != 0)
            throw new ArgumentException("输入数组长度必须是2的幂");

        var data = new Complex[n];
        for (var i = 0; i < n; i++)
            data[i] = new Complex(input[i], 0);

        return FFTRecursive(data);
    }

    private static Complex[] FFTRecursive(Complex[] input)
    {
        var n = input.Length;
        if (n == 1)
            return [input[0]];

        var half = n / 2;

        var even = new Complex[half];
        var odd = new Complex[half];
        for (var i = 0; i < half; i++)
        {
            even[i] = input[i * 2];
            odd[i] = input[i * 2 + 1];
        }

        var fftEven = FFTRecursive(even);
        var fftOdd = FFTRecursive(odd);

        var output = new Complex[n];
        for (var k = 0; k < half; k++)
        {
            var t = Complex.FromPolarCoordinates(1, -2 * Math.PI * k / n) * fftOdd[k];
            output[k] = fftEven[k] + t;
            output[k + half] = fftEven[k] - t;
        }

        return output;
    }
}