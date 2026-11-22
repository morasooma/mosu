using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Razorvine.Pickle;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>Weights-only conversion for the hash-pinned Tiny-SD checkpoint; never runs Python or checkpoint code.</summary>
    internal static class BackgroundModelWeights
    {
        internal sealed record Tensor(int[] Shape, float[] Values);
        private sealed record Storage(string Key, bool Half);
        private sealed record View(Storage Storage, int Offset, int[] Shape, int[] Stride);
        private sealed class StateDictionary : Hashtable
        {
            // PyTorch attaches module metadata, not weights, through BUILD.
            public void __setstate__(Hashtable state) { }
        }
        private sealed class StorageType(string name) : IObjectConstructor
        {
            public string Name => name;
            public object construct(object[] args) => throw new InvalidDataException("Storage types must not execute.");
        }
        private sealed class Constructor(Func<object[], object> create) : IObjectConstructor
        {
            public object construct(object[] args) => create(args);
        }
        private sealed class WeightsUnpickler : Unpickler
        {
            protected override object persistentLoad(object pid)
            {
                var values = (object[])pid;
                if ((string)values[0] != "storage" || values[1] is not StorageType type
                    || values[2] is not string key || !key.All(char.IsAsciiDigit))
                    throw new InvalidDataException("Unsupported tensor storage.");
                return new Storage(key, type.Name == "F16");
            }
        }
        static BackgroundModelWeights()
        {
            Unpickler.registerConstructor("collections", "OrderedDict", new Constructor(_ => new StateDictionary()));
            Unpickler.registerConstructor("torch", "HalfStorage", new StorageType("F16"));
            Unpickler.registerConstructor("torch", "FloatStorage", new StorageType("F32"));
            // GLOBAL storage symbols are passed as constructors rather than invoked by pickle.
            Unpickler.registerConstructor("torch._utils", "_rebuild_tensor_v2", new Constructor(args =>
            {
                if (args.Length != 6 || args[0] is not Storage storage)
                    throw new InvalidDataException("Unsupported torch tensor.");
                return new View(storage, Convert.ToInt32(args[1]), Integers(args[2]), Integers(args[3]));
            }));
        }
        private static void Initialise() { }
        private static int[] Integers(object value) => ((object[])value).Select(Convert.ToInt32).ToArray();

        internal sealed class TorchFile : IDisposable
        {
            private readonly ZipArchive zip;
            private readonly Dictionary<string, View> views = new Dictionary<string, View>();
            private readonly string prefix;
            public IEnumerable<string> Names => views.Keys;
            public TorchFile(string path)
            {
                Initialise();
                zip = ZipFile.OpenRead(path);
                try
                {
                    var entry = zip.Entries.Single(e => e.FullName.EndsWith("/data.pkl", StringComparison.Ordinal));
                    if (entry.Length > 2_000_000) throw new InvalidDataException("Oversized torch metadata.");
                    prefix = entry.FullName[..^8];
                    using var stream = entry.Open();
                    using var unpickler = new WeightsUnpickler();
                    // Constructor objects for storage types are resolved by persistentLoad below.
                    var dictionary = (IDictionary)unpickler.load(stream);
                    foreach (DictionaryEntry item in dictionary)
                        if (item.Key is string name && item.Value is View view)
                            views.Add(name, view);
                    if (views.Count is < 1 or > 1000) throw new InvalidDataException("Invalid torch weights.");
                }
                catch { zip.Dispose(); throw; }
            }
            public Tensor Read(string name)
            {
                var view = views[name];
                var entry = zip.GetEntry(prefix + "data/" + view.Storage.Key) ?? throw new InvalidDataException("Missing torch storage.");
                if (entry.Length > 200_000_000) throw new InvalidDataException("Oversized torch storage.");
                using var input = entry.Open();
                byte[] raw = new byte[checked((int)entry.Length)];
                input.ReadExactly(raw);
                int count = view.Shape.Aggregate(1, (a, b) => checked(a * b));
                if (view.Shape.Length is < 1 or > 4 || view.Stride.Length != view.Shape.Length || count > 50_000_000)
                    throw new InvalidDataException("Unsupported torch shape.");
                float[] values = new float[count];
                for (int i = 0; i < count; i++)
                {
                    int index = view.Offset, rest = i;
                    for (int axis = view.Shape.Length - 1; axis >= 0; axis--)
                    {
                        index = checked(index + rest % view.Shape[axis] * view.Stride[axis]);
                        rest /= view.Shape[axis];
                    }
                    values[i] = view.Storage.Half ? (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(raw, checked(index * 2)))
                        : BitConverter.ToSingle(raw, checked(index * 4));
                }
                return new Tensor(view.Shape, values);
            }
            public void Dispose() => zip.Dispose();
        }

        internal sealed class SafeFile : IDisposable
        {
            private readonly FileStream input;
            private readonly JsonDocument header;
            private readonly long start;
            public IEnumerable<string> Names => header.RootElement.EnumerateObject().Where(p => p.Name != "__metadata__").Select(p => p.Name);
            public SafeFile(string path)
            {
                input = File.OpenRead(path);
                try
                {
                    byte[] length = new byte[8]; input.ReadExactly(length);
                    long size = BitConverter.ToInt64(length);
                    if (size is < 2 or > 2_000_000) throw new InvalidDataException("Invalid safetensors header.");
                    byte[] json = new byte[(int)size]; input.ReadExactly(json);
                    header = JsonDocument.Parse(json);
                    start = 8 + size;
                }
                catch { input.Dispose(); throw; }
            }
            public bool Contains(string name) => header.RootElement.TryGetProperty(name, out _);
            public Tensor Read(string name)
            {
                var item = header.RootElement.GetProperty(name);
                int[] shape = item.GetProperty("shape").EnumerateArray().Select(v => v.GetInt32()).ToArray();
                int count = shape.Aggregate(1, (a, b) => checked(a * b));
                string dtype = item.GetProperty("dtype").GetString()!;
                int bytes = dtype == "F32" ? 4 : dtype == "F16" ? 2 : throw new InvalidDataException("Unsupported safetensors dtype.");
                var offsets = item.GetProperty("data_offsets");
                if (count > 50_000_000 || offsets[1].GetInt64() - offsets[0].GetInt64() != (long)count * bytes)
                    throw new InvalidDataException("Invalid safetensors dimensions.");
                input.Position = checked(start + offsets[0].GetInt64());
                byte[] raw = new byte[count * bytes]; input.ReadExactly(raw);
                float[] values = new float[count];
                for (int i = 0; i < count; i++)
                    values[i] = bytes == 4 ? BitConverter.ToSingle(raw, i * 4) : (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(raw, i * 2));
                return new Tensor(shape, values);
            }
            public void Dispose() { header.Dispose(); input.Dispose(); }
        }

        internal static void WriteSafe(string path, IEnumerable<string> names, Func<string, Tensor> read, Func<string, string> rename, bool fp8, CancellationToken token)
        {
            string payload = path + ".data";
            try
            {
                var header = new Dictionary<string, object>();
                using (var output = File.Create(payload))
                {
                    foreach (string name in names.OrderBy(n => n, StringComparer.Ordinal))
                    {
                        token.ThrowIfCancellationRequested();
                        var tensor = read(name);
                        bool compact = fp8 && tensor.Values.Length >= 1024 && name.EndsWith(".weight", StringComparison.Ordinal);
                        byte[] bytes = new byte[tensor.Values.Length * (compact ? 1 : 2)];
                        for (int i = 0; i < tensor.Values.Length; i++)
                        {
                            if (!float.IsFinite(tensor.Values[i])) throw new InvalidDataException("Non-finite model weight.");
                            if (compact) bytes[i] = ToFloat8(tensor.Values[i]);
                            else BitConverter.TryWriteBytes(bytes.AsSpan(i * 2, 2), BitConverter.HalfToUInt16Bits((Half)tensor.Values[i]));
                        }
                        long offset = output.Position;
                        output.Write(bytes);
                        header.Add(rename(name), new { dtype = compact ? "F8_E4M3" : "F16", shape = tensor.Shape, data_offsets = new[] { offset, output.Position } });
                    }
                }
                byte[] json = JsonSerializer.SerializeToUtf8Bytes(header);
                int padding = (8 - json.Length % 8) % 8;
                using var file = File.Create(path);
                file.Write(BitConverter.GetBytes((long)json.Length + padding)); file.Write(json);
                for (int i = 0; i < padding; i++) file.WriteByte(32);
                using var data = File.OpenRead(payload); data.CopyTo(file);
            }
            finally { File.Delete(payload); }
        }

        internal static void WriteClip(string path, TorchFile clip, CancellationToken token)
        {
            string payload = path + ".data";
            try
            {
                var entries = new List<(string Name, int[] Shape, int Type, long Offset)>();
                using (var data = File.Create(payload))
                using (var writer = new BinaryWriter(data))
                {
                    foreach (string name in clip.Names.OrderBy(n => n, StringComparer.Ordinal))
                    {
                        token.ThrowIfCancellationRequested();
                        var tensor = clip.Read(name);
                        bool quantize = tensor.Shape.Length == 2 && tensor.Shape[1] % 32 == 0;
                        entries.Add(("cond_stage_model.transformer." + name, tensor.Shape, quantize ? 2 : 1, data.Position));
                        if (quantize)
                        {
                            for (int i = 0; i < tensor.Values.Length; i += 32)
                            {
                                float maximum = 0, absolute = 0;
                                for (int j = 0; j < 32; j++)
                                    if (MathF.Abs(tensor.Values[i + j]) > absolute) { maximum = tensor.Values[i + j]; absolute = MathF.Abs(maximum); }
                                float scale = maximum / -8, inverse = scale == 0 ? 0 : 1 / scale;
                                writer.Write(BitConverter.HalfToUInt16Bits((Half)scale));
                                for (int j = 0; j < 16; j++)
                                {
                                    int a = Math.Clamp((int)(tensor.Values[i + j] * inverse + 8.5f), 0, 15);
                                    int b = Math.Clamp((int)(tensor.Values[i + j + 16] * inverse + 8.5f), 0, 15);
                                    writer.Write((byte)(a | b << 4));
                                }
                            }
                        }
                        else foreach (float value in tensor.Values) writer.Write(BitConverter.HalfToUInt16Bits((Half)value));
                        while (data.Position % 32 != 0) writer.Write((byte)0);
                    }
                }
                using var output = File.Create(path);
                using var header = new BinaryWriter(output);
                header.Write(0x46554747); header.Write(3); header.Write((ulong)entries.Count); header.Write(0UL);
                foreach (var entry in entries)
                {
                    byte[] name = System.Text.Encoding.UTF8.GetBytes(entry.Name);
                    header.Write((ulong)name.Length); header.Write(name); header.Write((uint)entry.Shape.Length);
                    foreach (int dimension in entry.Shape.Reverse()) header.Write((ulong)dimension);
                    header.Write(entry.Type); header.Write((ulong)entry.Offset);
                }
                while (output.Position % 32 != 0) header.Write((byte)0);
                using var input = File.OpenRead(payload); input.CopyTo(output);
            }
            finally { File.Delete(payload); }
        }

        private static byte ToFloat8(float value)
        {
            int sign = value < 0 ? 128 : 0;
            float x = MathF.Abs(value);
            if (x >= 448) return (byte)(sign | 126);
            if (x < 1f / 64) return (byte)(sign | (int)MathF.Round(x * 512, MidpointRounding.ToEven));
            int exponent = Math.ILogB(x);
            int mantissa = (int)MathF.Round((MathF.ScaleB(x, -exponent) - 1) * 8, MidpointRounding.ToEven);
            return (byte)(sign | ((exponent + 7) * 8 + mantissa));
        }

        internal static Tensor Merge(Tensor weight, Tensor down, Tensor up, CancellationToken token)
        {
            int rows = weight.Shape[0], columns = weight.Values.Length / rows, rank = down.Shape[0];
            if (rank != 64 || down.Values.Length != rank * columns || up.Values.Length != rows * rank)
                throw new InvalidDataException("Unexpected Tiny-SD adapter shape.");
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token };
            Parallel.For(0, rows, parallel, row =>
            {
                float[] delta = new float[columns];
                for (int r = 0; r < rank; r++)
                {
                    var multiplier = new Vector<float>(up.Values[row * rank + r]);
                    int c = 0;
                    for (; c <= columns - Vector<float>.Count; c += Vector<float>.Count)
                        (new Vector<float>(delta, c) + new Vector<float>(down.Values, r * columns + c) * multiplier).CopyTo(delta, c);
                    for (; c < columns; c++) delta[c] += down.Values[r * columns + c] * up.Values[row * rank + r];
                }
                for (int c = 0; c < columns; c++) weight.Values[row * columns + c] += delta[c] * .125f;
            });
            return weight;
        }

        internal static string UnetName(string name)
        {
            string[] from = { "time_embedding.linear_1", "time_embedding.linear_2", "conv_in", "conv_norm_out", "conv_out" };
            string[] to = { "time_embed.0", "time_embed.2", "input_blocks.0.0", "out.0", "out.2" };
            for (int i = 0; i < from.Length; i++) if (name.StartsWith(from[i] + ".", StringComparison.Ordinal)) return "model.diffusion_model." + to[i] + name[from[i].Length..];
            if (name.Contains("resnets", StringComparison.Ordinal))
            {
                string[] a = { "norm1", "conv1", "norm2", "conv2", "time_emb_proj", "conv_shortcut" };
                string[] b = { "in_layers.0", "in_layers.2", "out_layers.0", "out_layers.3", "emb_layers.1", "skip_connection" };
                for (int i = 0; i < a.Length; i++) name = name.Replace(a[i], b[i], StringComparison.Ordinal);
            }
            for (int block = 0; block < 4; block++)
            {
                for (int j = 0; j < 2; j++)
                {
                    name = name.Replace($"down_blocks.{block}.resnets.{j}.", $"input_blocks.{3 * block + j + 1}.0.", StringComparison.Ordinal);
                    if (block < 3) name = name.Replace($"down_blocks.{block}.attentions.{j}.", $"input_blocks.{3 * block + j + 1}.1.", StringComparison.Ordinal);
                }
                for (int j = 0; j < 3; j++)
                {
                    name = name.Replace($"up_blocks.{block}.resnets.{j}.", $"output_blocks.{3 * block + j}.0.", StringComparison.Ordinal);
                    if (block > 0) name = name.Replace($"up_blocks.{block}.attentions.{j}.", $"output_blocks.{3 * block + j}.1.", StringComparison.Ordinal);
                }
                if (block < 3)
                {
                    name = name.Replace($"down_blocks.{block}.downsamplers.0.conv.", $"input_blocks.{3 * (block + 1)}.0.op.", StringComparison.Ordinal);
                    name = name.Replace($"up_blocks.{block}.upsamplers.0.", $"output_blocks.{3 * block + 2}.{(block == 0 ? 1 : 2)}.", StringComparison.Ordinal);
                }
            }
            name = name.Replace("mid_block.attentions.0.", "middle_block.1.", StringComparison.Ordinal);
            for (int j = 0; j < 2; j++) name = name.Replace($"mid_block.resnets.{j}.", $"middle_block.{2 * j}.", StringComparison.Ordinal);
            return "model.diffusion_model." + name;
        }
    }
}
