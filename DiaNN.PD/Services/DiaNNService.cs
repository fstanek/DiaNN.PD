using System;
using System.Collections.Generic;
using System.IO;
using Thermo.Magellan.BL.Data;
using Thermo.Magellan.Utilities;

namespace DiaNN.PD.Services
{
    public class DiaNNService
    {
        private readonly string fileName;
        private readonly string workingDirectory;
        private readonly ArgumentList arguments = new ArgumentList();

        public event Action<string> OutputReceived;
        public event Action<string> ErrorReceived;

        public DiaNNService(string fileName)
        {
            this.fileName = fileName;
            workingDirectory = Path.GetDirectoryName(fileName);

            InitializeArguments();
        }

        private void InitializeArguments()
        {
            arguments.Add("verbose", 5); // sets the level of detail of the log. Reasonable values are in the range 0 - 5 (higher -> more details)
            arguments.Add("threads", Environment.ProcessorCount); // specifies the number of CPU threads to use

            arguments.Add("matrices"); // output quantities matrices
            arguments.Add("predictor"); // instructs DIA-NN to perform deep learning-based prediction of spectra, retention times and ion mobility values
            arguments.Add("met-excision"); // enables protein N-term methionine excision as variable modification for the in silico digest
            arguments.Add("qvalue", 0.01); // sets the q-value used to filter the output matrices

            arguments.Add("missed-cleavages", 1); // sets the maximum number of missed cleavages

            arguments.Add("min-pep-len", 7); // sets the minimum precursor length for the in silico library generation or library-free search
            arguments.Add("max-pep-len", 30); // sets the maximum precursor length for the in silico library generation or library-free search

            arguments.Add("min-pr-charge", 1); // sets the minimum precursor charge for the in silico library generation or library-free search
            arguments.Add("max-pr-charge", 4); // sets the maximum precursor charge for the in silico library generation or library-free search

            arguments.Add("min-pr-mz", 300); // sets the minimum precursor m/z for the in silico library generation or library-free search
            arguments.Add("max-pr-mz", 1800); // sets the maximum precursor m/z for the in silico library generation or library-free search

            arguments.Add("min-fr-mz", 200); // sets the minimum fragment m/z for the in silico library generation or library-free search
            arguments.Add("max-fr-mz", 1800); // sets the minimum fragment m/z for the in silico library generation or library-free search

            arguments.Add("reanalyse"); // enables MBR
            arguments.Add("smart-profiling"); //  enables an intelligent algorithm which determines how to extract spectra, when creating a spectral library from DIA data. This is highly recommended and should almost always be enabled

            // TODO investigate protein inference
            arguments.Add("no-prot-inf");  // disables protein inference (that is protein grouping) - protein groups from the spectral library will be used instead

            arguments.Add("unimod4"); // [not found in cmd reference]
            arguments.Add("lib", "\"\"");
        }

        public void SetSpectrumFiles(IEnumerable<SpectrumFile> spectrumFiles)
        {
            foreach (var spectrumFile in spectrumFiles)
                arguments.Add("f", spectrumFile.FullPhysicalFileName); // specifies a run to be analysed, use multiple --f commands to specify multiple runs
        }

        public void SetFastaFile(FastaFile fastaFile)
        {
            arguments.Add("fasta-search"); // instructs DIA-NN to perform an in silico digest of the sequence database
            arguments.Add("fasta", fastaFile.FullPhysicalFileName); // specifies a sequence database in FASTA format (full support for UniProt proteomes), use multiple --fasta commands to specify multiple databases
        }

        public void SetEnzyme(Enzyme enzyme)
        {
            var sites = GetSites(enzyme);
            var pattern = string.Join(",", sites);
            arguments.Add("cut", pattern); // specifies cleavage specificity for the in silico digest. Cleavage sites (pairs of amino acids) are listed separated by commas, '*' indicates any amino acid, and '!' indicates that the respective site will not be cleaved. Examples: "--cut K*,R*,!*P" - canonical tryptic specificity, "--cut " - digest disabled
        }

        private IEnumerable<string> GetSites(Enzyme enzyme)
        {
            string GetSite(char aminoAcid) => $"{aminoAcid}".Insert(enzyme.Offset, "*");

            foreach (var aminoAcid in enzyme.CleavageSites)
                yield return GetSite(aminoAcid);

            foreach (var aminoAcid in enzyme.CleavageInhibitors)
                yield return $"!{GetSite(aminoAcid)}";
        }

        public void SetOutputFile(string fileName)
        {
            arguments.Add("out", fileName); // specifies the name of the main output report. The names of all other report files will be derived from this one
        }

        public void SetOutputLibrary(string fileName)
        {
            arguments.Add("gen-spec-lib"); //  instructs DIA-NN to generate a spectral library 
            arguments.Add("out-lib", fileName); // specifies the name of a spectral library to be generated
        }

        public int Run()
        {
            return ExternalProcessHelper.ExecuteAbortableProcess(fileName, workingDirectory, arguments.ToString(),
                (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        OutputReceived?.Invoke(e.Data);
                },
                (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        ErrorReceived?.Invoke(e.Data);
                });
        }
    }
}