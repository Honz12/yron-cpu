using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Assembler
{
    public sealed class LibraryFile
    {
        private static readonly byte[] Magic = { (byte) 'Y', (byte) 'R', (byte) 'L', 0 };

        public Dictionary<string, int> Symbols { get; }
        public List<(string Name, int Offset)> References { get; }
        public byte[] Binary { get; }

        public LibraryFile(Dictionary<string, int> symbols, List<(string Name, int Offset)> references, byte[] binary)
        {
            Symbols = symbols;
            References = references;
            Binary = binary;
        }

        public static LibraryFile Read(string path)
        {
            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                throw new Exception($"cannot read '{path}': {e.Message}");
            }

            try
            {
                return FromBytes(data, path);
            }
            catch (Exception e)
            {
                throw new Exception($"'{path}' is not a valid link file: {e.Message}");
            }
        }

        public void Write(string path) => File.WriteAllBytes(path, ToBytes());

        public byte[] ToBytes()
        {
            MemoryStream ms = new();
            ms.Write(Magic);

            foreach ((string name, int address) in Symbols)
            {
                WriteString(ms, name);
                WriteDword(ms, address);
            }
            WriteString(ms, "");

            foreach ((string name, int offset) in References)
            {
                WriteString(ms, name);
                WriteDword(ms, offset);
            }
            WriteString(ms, "");

            ms.Write(Binary);
            return ms.ToArray();
        }

        public static LibraryFile FromBytes(byte[] data, string source)
        {
            int pos = 0;

            byte[] magic = ReadBytes(data, ref pos, 4);
            if (magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                throw new Exception("missing 'YRL\\0' header");

            Dictionary<string, int> symbols = new();
            while (true)
            {
                string name = ReadString(data, ref pos);
                if (name.Length == 0) break;
                int address = ReadDword(data, ref pos);
                if (symbols.ContainsKey(name))
                    throw new Exception($"duplicate symbol '{name}' in symbol table");
                symbols[name] = address;
            }

            List<(string Name, int Offset)> references = new();
            while (true)
            {
                string name = ReadString(data, ref pos);
                if (name.Length == 0) break;
                references.Add((name, ReadDword(data, ref pos)));
            }

            byte[] binary = new byte[data.Length - pos];
            Array.Copy(data, pos, binary, 0, binary.Length);

            return new LibraryFile(symbols, references, binary);
        }

        private static void WriteString(MemoryStream ms, string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            ms.Write(bytes);
            ms.WriteByte(0);
        }

        private static void WriteDword(MemoryStream ms, int value)
        {
            ms.WriteByte((byte) value);
            ms.WriteByte((byte) (value >> 8));
            ms.WriteByte((byte) (value >> 16));
            ms.WriteByte((byte) (value >> 24));
        }

        private static byte[] ReadBytes(byte[] data, ref int pos, int count)
        {
            if (pos + count > data.Length)
                throw new Exception("unexpected end of file");
            byte[] result = new byte[count];
            Array.Copy(data, pos, result, 0, count);
            pos += count;
            return result;
        }

        private static string ReadString(byte[] data, ref int pos)
        {
            int start = pos;
            while (pos < data.Length && data[pos] != 0) pos++;
            if (pos >= data.Length)
                throw new Exception("unterminated string");
            string s = Encoding.UTF8.GetString(data, start, pos - start);
            pos++;
            return s;
        }

        private static int ReadDword(byte[] data, ref int pos)
        {
            byte[] b = ReadBytes(data, ref pos, 4);
            return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
        }
    }

    public static class Linker
    {
        public static LibraryFile Link(IReadOnlyList<string> inputPaths)
        {
            if (inputPaths.Count == 0)
                throw new Exception("link requires at least one input file");

            Dictionary<string, int> merged = new();
            Dictionary<string, HashSet<string>> definedByFile = new();
            List<(string File, string Name, int Offset, long Base)> pendingRefs = new();
            List<byte[]> binaries = new();
            long baseAddr = 0;

            foreach (string path in inputPaths)
            {
                LibraryFile lib = LibraryFile.Read(path);

                HashSet<string> defined = new();
                foreach ((string name, int address) in lib.Symbols)
                {
                    defined.Add(name);
                    if (merged.ContainsKey(name))
                        throw new Exception($"duplicate symbol '{name}' (in '{path}')");
                    merged[name] = (int) (baseAddr + address);
                }
                definedByFile[path] = defined;

                foreach ((string name, int offset) in lib.References)
                    pendingRefs.Add((path, name, offset, baseAddr));

                binaries.Add(lib.Binary);
                baseAddr += lib.Binary.Length;
            }

            if (baseAddr > Assembler.MaxRomSize)
                throw new Exception($"linked image too large: {baseAddr} bytes (max {Assembler.MaxRomSize})");

            List<byte> output = new();
            foreach (byte[] binary in binaries)
                output.AddRange(binary);

            foreach ((string file, string name, int offset, long fileBase) in pendingRefs)
            {
                if (!merged.TryGetValue(name, out int address))
                    throw new Exception($"unresolved reference '{name}' (in '{file}')");

                int at = (int) (fileBase + offset);
                if (at < 0 || at + 3 >= output.Count)
                    throw new Exception($"bad reference offset {offset} for '{name}' (in '{file}')");

                if (definedByFile.TryGetValue(file, out HashSet<string>? defined) && defined.Contains(name))
                {
                    long value = (long) output[at] |
                                 ((long) output[at + 1] << 8) |
                                 ((long) output[at + 2] << 16) |
                                 ((long) output[at + 3] << 24);
                    value += fileBase;
                    address = (int) value;
                }

                output[at] = (byte) address;
                output[at + 1] = (byte) (address >> 8);
                output[at + 2] = (byte) (address >> 16);
                output[at + 3] = (byte) (address >> 24);
            }

            return new LibraryFile(merged, new List<(string Name, int Offset)>(), output.ToArray());
        }
    }
}
