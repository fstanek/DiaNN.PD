using DiaNN.PD.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DiaNN.PD.Services
{
    public static class ResultReader
    {
        // TODO read precursor areas

        private const char ColumnSeparator = '\t';
        private const char ProteinSeparator = ';';
        private readonly static Regex regex = new Regex(@"\(unimod:|\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static IEnumerable<ProteinGroup> GetProteinGroups(string fileName, IEnumerable<string> fileNames)
        {
            using (var reader = new StreamReader(fileName))
            {
                if (reader.EndOfStream)
                    yield break;

                var header = ReadLine(reader);
                var indices = new
                {
                    Name = Array.IndexOf(header, "Protein.Group"),
                    Ids = Array.IndexOf(header, "Protein.Ids"),
                    Areas = fileNames.Select(f => Array.IndexOf(header, f)).ToArray()
                };

                while (!reader.EndOfStream)
                {
                    var data = ReadLine(reader);

                    yield return new ProteinGroup
                    {
                        Name = data[indices.Name],
                        ProteinIds = GetProteinIds(data[indices.Ids]).ToList(),
                        Areas = indices.Areas.Select(i => GetDouble(data[i])).ToList()
                    };
                }
            }
        }

        private static double? GetDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return default;

            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        public static IEnumerable<Peptide> GetPeptideMatches(string fileName)
        {
            var proteinGroupFileName = Path.ChangeExtension(fileName, null) + "pg_matrix" + Path.GetExtension(fileName);

            using (var reader = new StreamReader(fileName))
            {
                if (reader.EndOfStream)
                    yield break;

                var header = ReadLine(reader);
                int GetIndex(string name) => Array.IndexOf(header, name);

                var columns = new List<(string name, Action<Peptide, string> factory)>();
                columns.Add(("File.Name", (p, v) => p.FileName = v));

                // TODO check if -1
                var indices = new
                {
                    FileName = Array.IndexOf(header, "File.Name"),
                    ScanNumber = Array.IndexOf(header, "MS2.Scan"),
                    ProteinIds = Array.IndexOf(header, "Protein.Ids"),

                    StrippedSequence = Array.IndexOf(header, "Stripped.Sequence"),
                    ModifiedSequence = Array.IndexOf(header, "Modified.Sequence"),

                    Score = Array.IndexOf(header, "CScore"),
                    Area = Array.IndexOf(header, "Ms1.Area"),

                    RT = GetIndex("RT"),
                    RTStart = GetIndex("RT.Start"),
                    RTStop = GetIndex("RT.Stop")
                };

                while (!reader.EndOfStream)
                {
                    var data = ReadLine(reader);

                    // TODO parser error handling
                    yield return new Peptide
                    {
                        FileName = data[indices.FileName],
                        ScanNumber = int.Parse(data[indices.ScanNumber], CultureInfo.InvariantCulture),
                        ProteinIds = GetProteinIds(data[indices.ProteinIds]).ToList(),

                        Sequence = data[indices.StrippedSequence],
                        Modifications = GetModifications(data[indices.ModifiedSequence]).ToList(),

                        Score = double.Parse(data[indices.Score], CultureInfo.InvariantCulture),
                        Area = double.Parse(data[indices.Area], CultureInfo.InvariantCulture),

                        RT = double.Parse(data[indices.RT]),
                        RTStart = double.Parse(data[indices.RTStart]),
                        RTStop = double.Parse(data[indices.RTStop])
                    };
                }
            }
        }

        private static string[] ReadLine(TextReader reader)
        {
            return reader.ReadLine().Split(ColumnSeparator);
        }

        private static IEnumerable<int> GetProteinIds(string text)
        {
            return text.Split(new[] { ProteinSeparator }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
        }

        private static IEnumerable<Modification> GetModifications(string modifiedSequence)
        {
            var segments = regex.Split(modifiedSequence);

            var offset = 0;
            var isId = false;

            foreach (var segment in segments)
            {
                if (isId)
                {
                    // TODO error handling
                    yield return new Modification
                    {
                        Position = offset - 1,
                        UnimodId = int.Parse(segment)
                    };
                }
                else
                {
                    offset += segment.Length;
                }

                isId = !isId;
            }
        }
    }
}