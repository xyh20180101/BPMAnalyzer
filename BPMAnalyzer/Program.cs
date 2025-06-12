using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace BPMAnalyzer
{
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

            var extension = Path.GetExtension(fullFileName)?.ToLower();

            double frameCount;
            int sampleCount;
            double[] volume;
            int sampleRate;

            using (WaveStream reader = extension switch
            {
                ".wav" => new WaveFileReader(fullFileName),
                ".mp3" => new Mp3FileReader(fullFileName),
                _ => throw new InvalidDataException("Incorrect Format.")
            })
            {
                var waveProvider16 = reader.ToSampleProvider().ToWaveProvider16();

                sampleRate = reader.WaveFormat.SampleRate;
                frameCount = (double)sampleRate / FrameSize;
                var dataLength = (int)reader.Length / 2;
                sampleCount = dataLength / FrameSize;
                volume = (from index in Enumerable.Range(0, sampleCount)
                          let sum = GetData(waveProvider16, FrameSize * 2).Sum(d => (double)d * d)
                          select Math.Sqrt(sum / FrameSize)).ToArray();
            }

            var prev = 0.0;
            var diff = (from v in volume let temp = prev select Math.Max((prev = v) - temp, 0.0)).ToArray();

            var onsetOffsetMs = DetectFirstOnsetOffsetMs(diff, sampleRate, FrameSize);

            var beatPattern = DetectMeter(diff);

            var indices = Enumerable.Range(0, diff.Length).AsParallel();
            var r = (from i in Enumerable.Range(0, 181)
                     let freq = (i + 60) / 60.0
                     let theta = 2.0 * Math.PI * freq / frameCount
                     let cosSum = indices.Sum(index => HannWindow(index, diff.Length) * Math.Cos(theta * index) * diff[index]) / sampleCount
                     let sinSum = indices.Sum(index => HannWindow(index, diff.Length) * Math.Sin(theta * index) * diff[index]) / sampleCount
                     select new { A = cosSum, B = sinSum, R = Math.Sqrt(cosSum * cosSum + sinSum * sinSum) }).ToArray();

            var peaks = FindPeak(r.Select(obj => obj.R).ToArray(), 1);

            if (peaks.Any())
            {
                var bpm = peaks[0] + 60;
                var msg = $@"File: {Path.GetFileName(fullFileName)}
Peak BPM: {bpm}
First Beat Onset Offset: {onsetOffsetMs}ms
Estimated Meter: {beatPattern} time";
                Console.WriteLine(msg);
            }
            else
            {
                Console.WriteLine("Get Peaks Failed.");
            }

            Console.ReadKey();
        }

        static short[] GetData(IWaveProvider provider, int count)
        {
            var data = new short[count / 2];
            var byteData = new byte[count];
            provider.Read(byteData, 0, count);//provider.position is increasing
            Buffer.BlockCopy(byteData, 0, data, 0, count);
            return data;
        }

        static double HannWindow(int i, int size)
        {
            return 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / size);
        }

        static int[] FindPeak(double[] graph, int count)
        {
            var obj = from i in Enumerable.Range(0, graph.Length - 1)
                      select new { Diff = graph[i + 1] - graph[i], Prev = i == 0 ? 0 : graph[i] - graph[i - 1], GraphValue = graph[i], Index = i };

            var indices = from o in obj
                          where o.Diff <= 0 && o.Prev > 0
                          orderby o.GraphValue descending
                          select o.Index;
            return indices.Take(count).ToArray();
        }

        static double DetectFirstOnsetOffsetMs(double[] diff, int sampleRate, int frameSize)
        {
            var threshold = diff.Max() * 0.3;
            for (var i = 0; i < diff.Length; i++)
            {
                if (diff[i] >= threshold)
                {
                    var samplesPerFrame = frameSize;
                    var timeMs = i * samplesPerFrame * 1000.0 / sampleRate;
                    return timeMs;
                }
            }

            return 0.0;
        }

        static string DetectMeter(double[] diff)
        {
            var threshold = diff.Max() * 0.3;
            var beatIndices = Enumerable.Range(0, diff.Length)
                .Where(i => diff[i] >= threshold)
                .ToList();

            if (beatIndices.Count < 4)
                return "Unknown";

            var intervals = beatIndices.Zip(beatIndices.Skip(1), (a, b) => b - a).ToList();

            var median = intervals.OrderBy(i => i).ElementAt(intervals.Count / 2);

            var meterCounts = new Dictionary<int, int>();

            for (var meter = 2; meter <= 6; meter++)
            {
                var groupCount = 0;
                for (var i = 0; i + meter < beatIndices.Count; i++)
                {
                    var start = beatIndices[i];
                    var end = beatIndices[i + meter];
                    var expected = median * meter;
                    var actual = end - start;

                    if (Math.Abs(actual - expected) < median * 0.3)
                        groupCount++;
                }
                meterCounts[meter] = groupCount;
            }

            var best = meterCounts.OrderByDescending(kvp => kvp.Value).First();
            if (best.Value < 2)
                return "Unknown";

            return $"{best.Key}/4";
        }

    }
}
