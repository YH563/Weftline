using System.Diagnostics;
using System.Text;
using AvaloniaEdit.Document;
using Weftline.Native;

namespace Weftline.Benchmarks;

internal static class Program
{
    private static readonly int[] Sizes = [10_000, 100_000, 1_000_000];

    private static int Main()
    {
        try
        {
            using var client = new NativeWeftlineClient();
            Console.WriteLine($"Weftline native {client.Version}");
            RunParseBenchmarks();
            RunProjectBenchmarks(client);
            RunDocumentBenchmarks();
            return 0;
        }
        catch (DllNotFoundException exception)
        {
            Console.Error.WriteLine($"无法加载 weftline_core：{exception.Message}");
            Console.Error.WriteLine("先构建 native core，再运行基准：");
            Console.Error.WriteLine("  cmake --build native/out/build/release");
            Console.Error.WriteLine("  dotnet run --project bench/Weftline.Benchmarks");
            return 1;
        }
    }

    private static void RunParseBenchmarks()
    {
        PrintHeader("single-file parse");
        foreach (var size in Sizes)
        {
            var text = GenerateSingleFile(size);
            var bytes = Encoding.UTF8.GetByteCount(text);
            var sample = Measure(
                () => _ = NativeWeftlineClient.Parse("bench.weft", text, size),
                3,
                IterationsFor(size));
            PrintRow(SizeLabel(size), text.Length, bytes, sample);
        }
    }

    private static void RunProjectBenchmarks(NativeWeftlineClient client)
    {
        PrintHeader("project compile");
        foreach (var size in Sizes)
        {
            var folder = CreateProject(size, FileCountFor(size));

            try
            {
                var files = Directory.GetFiles(folder, "*.weft");
                var chars = files.Sum(path => File.ReadAllText(path).Length);
                var bytes = files.Sum(path => new FileInfo(path).Length);

                var openDocuments = new Dictionary<string, string>(StringComparer.Ordinal);
                client.LoadFolder(folder, openDocuments, reloadFromDisk: true);
                client.Compile(allowPartial: true, buildProjection: true);

                var warm = Measure(
                    () => client.Compile(allowPartial: true, buildProjection: true),
                    2,
                    IterationsFor(size));
                PrintRow(
                    $"{SizeLabel(size)} warm",
                    chars,
                    bytes,
                    warm);

                var cold = Measure(
                    () =>
                    {
                        using var fresh = new NativeWeftlineClient();
                        fresh.LoadFolder(folder, openDocuments, reloadFromDisk: true);
                        fresh.Compile(allowPartial: true, buildProjection: true);
                    },
                    1,
                    ColdIterationsFor(size));
                PrintRow(
                    $"{SizeLabel(size)} cold",
                    chars,
                    bytes,
                    cold);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static void RunDocumentBenchmarks()
    {
        PrintHeader("TextDocument");
        foreach (var size in Sizes)
        {
            var text = GenerateSingleFile(size);
            var bytes = Encoding.UTF8.GetByteCount(text);
            var create = Measure(
                () => _ = new TextDocument(text),
                2,
                IterationsFor(size));
            PrintRow(
                $"{SizeLabel(size)} create",
                text.Length,
                bytes,
                create);

            var document = new TextDocument(text);
            var snapshot = Measure(
                () => _ = document.Text,
                5,
                IterationsFor(size));
            PrintRow(
                $"{SizeLabel(size)} snapshot",
                text.Length,
                bytes,
                snapshot);
        }
    }

    private static Sample Measure(Action action, int warmup, int iterations)
    {
        for (var index = 0; index < warmup; index++)
        {
            action();
        }

        var samples = new double[iterations];
        for (var index = 0; index < iterations; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        Array.Sort(samples);
        return new Sample(
            samples.Average(),
            samples[samples.Length / 2],
            samples[0],
            samples[^1]);
    }

    private static string GenerateSingleFile(int targetChars)
    {
        var builder = new StringBuilder();
        builder.Append("@title 基准\n\n");
        builder.Append("@var flag false\n");
        builder.Append("@char 角色A\n");
        builder.Append("@scene 场景A\n\n");

        var index = 0;
        while (builder.Length < targetChars)
        {
            builder.Append("@module M").Append(index).Append('\n');
            builder.Append("    @scene 场景A\n");
            builder.Append("    [第 ").Append(index)
                .Append(" 个模块的正文内容，用于性能基准测试。]\n");
            builder.Append("    角色A:[对话 ").Append(index).Append("。]\n");
            builder.Append("@next -> END\n\n");
            index++;
        }

        return builder.ToString();
    }

    private static string GenerateChapter(
        int targetChars,
        int chapterIndex,
        bool includeDeclarations)
    {
        var builder = new StringBuilder();
        builder.Append("@title 第").Append(chapterIndex + 1).Append("章\n\n");

        if (includeDeclarations)
        {
            builder.Append("@var flag false\n");
            builder.Append("@char 角色A\n");
            builder.Append("@scene 场景A\n\n");
        }

        var moduleStart = chapterIndex * 100_000;
        var index = 0;
        while (builder.Length < targetChars)
        {
            builder.Append("@module M").Append(moduleStart + index).Append('\n');
            builder.Append("    @scene 场景A\n");
            builder.Append("    [第 ").Append(moduleStart + index)
                .Append(" 个模块的正文内容，用于性能基准测试。]\n");
            builder.Append("    角色A:[对话 ").Append(moduleStart + index).Append("。]\n");
            builder.Append("@next -> END\n\n");
            index++;
        }

        return builder.ToString();
    }

    private static string CreateProject(int targetChars, int fileCount)
    {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "weftline-bench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var perFile = Math.Max(2_000, targetChars / fileCount);
        for (var index = 0; index < fileCount; index++)
        {
            var text = GenerateChapter(perFile, index, includeDeclarations: index == 0);
            File.WriteAllText(
                Path.Combine(folder, $"chapter{index}.weft"),
                text);
        }

        return folder;
    }

    private static void PrintHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {title} ===");
        Console.WriteLine(
            $"{"case",-18}{"chars",12}{"bytes",12}{"mean ms",11}{"median ms",12}{"min ms",10}{"max ms",10}");
    }

    private static void PrintRow(string label, int chars, long bytes, Sample sample)
        => Console.WriteLine(
            $"{label,-18}{chars,12:N0}{bytes,12:N0}{sample.Mean,11:F3}{sample.Median,12:F3}{sample.Min,10:F3}{sample.Max,10:F3}");

    private static string SizeLabel(int size)
        => size >= 1_000_000 ? "1MB" : $"{size / 1_000}KB";

    private static int IterationsFor(int size)
        => size switch
        {
            <= 20_000 => 20,
            <= 200_000 => 10,
            _ => 5,
        };

    private static int ColdIterationsFor(int size)
        => size switch
        {
            <= 20_000 => 3,
            <= 200_000 => 2,
            _ => 1,
        };

    private static int FileCountFor(int size)
        => size switch
        {
            <= 20_000 => 1,
            <= 200_000 => 10,
            _ => 50,
        };

    private readonly record struct Sample(
        double Mean,
        double Median,
        double Min,
        double Max);
}
