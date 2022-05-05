using DiaNN.PD.Models;
using DiaNN.PD.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Thermo.Magellan.BL.Data;
using Thermo.Magellan.BL.Data.ProcessingNodeScores;
using Thermo.Magellan.BL.Processing.Interfaces;
using Thermo.Magellan.MassSpec;
using Thermo.Magellan.PeptideIdentificationNodes;
using Thermo.Magellan.Proteomics;
using Thermo.PD.EntityDataFramework;
using Peptide = DiaNN.PD.Models.Peptide;

namespace DiaNN.PD.Nodes
{
    // TODO add description
    [ProcessingNode("EBF8143D-1D91-45C0-85D6-8C2CEB87F4DD",
        Category = ProcessingNodeCategories.SequenceDatabaseSearch,
        DisplayName = "DiaNN Search",
        MainVersion = 0, MinorVersion = 1,
        Visible = true)]

    [ConnectionPointDataContract("IncomingSpectra", MassSpecDataTypes.MSnSpectra)]
    [ConnectionPoint("IncomingSpectra",
        ConnectionDataHandlingType = ConnectionDataHandlingType.InMemory,
        ConnectionDirection = ConnectionDirection.Incoming,
        ConnectionDisplayName = ProcessingNodeCategories.SpectrumAndFeatureRetrieval,
        ConnectionMode = ConnectionMode.Manual,
        ConnectionMultiplicity = ConnectionMultiplicity.Single,
        ConnectionRequirement = ConnectionRequirement.RequiredAtDesignTime)]

    [ConnectionPoint("OutgoingIdentifications",
        ConnectionDirection = ConnectionDirection.Outgoing,
        ConnectionDisplayName = "PSM Validation",
        ConnectionMode = ConnectionMode.Manual,
        ConnectionMultiplicity = ConnectionMultiplicity.Single,
        ConnectionRequirement = ConnectionRequirement.RequiredAtDesignTime)]
    [ConnectionPointDataContract("OutgoingIdentifications",
        ProteomicsDataTypes.Psms, DataTypeAttributes = new[]
        {
            ProteomicsDataTypeAttributes.WithDecoys,
            ProteomicsDataTypeAttributes.ScoredWithNativeScore,
            /*ProteomicsDataTypeAttributes.ScoredWithEValue*/
        })]
    [ConnectionPointDataContract("OutgoingIdentifications",
        ProteomicsDataTypes.Proteins, DataTypeAttributes = new[]
        {
            ProteomicsDataTypeAttributes.WithDecoys
        })]

    // HACK to satisfy feature mapper
    [ConnectionPointDataContract("OutgoingFeatures", 
        MassSpecDataTypes.LcmsFeatures)]
    [ConnectionPoint("OutgoingFeatures",
        ConnectionDataHandlingType = ConnectionDataHandlingType.FileBased, 
        ConnectionDirection = ConnectionDirection.Outgoing, 
        ConnectionMultiplicity = ConnectionMultiplicity.Multiple)]

    public class SearchNode : PeptideAndProteinIdentificationNode
    {
        // TODO spectral library input parameter
        // TODO spectral library output parameter (copy from scratch)

        private readonly SpectrumMapper spectrumMapper = new SpectrumMapper();

        [MultilineStringParameter(DisplayName = "Parameters", IsAdvanced = true)]
        public MultilineStringParameter Parameters;

        [FileSelectionParameter(DisplayName = "Application Path", IsConfig = true, IsHidden = true, DefaultValue = @"C:\DIA-NN\1.8.1\DiaNN.exe",
            ValueRequired = true, SelectionMode = FileSelectionParameter.SelectionMode.FileOpen)]
        public FileSelectionParameter ApplicationPath;

        // TODO multiselect?
        [FastaFileParameter(DisplayName = "Protein Database", IntendedPurpose = ParameterPurpose.SequenceDatabase, Position = 1, ValueRequired = true)]
        public FastaFileParameter FastaFile;

        [EnzymeParameter(DisplayName = "Enzyme", IntendedPurpose = ParameterPurpose.CleavageReagent, Position = 1, ValueRequired = false)]
        public EnzymeParameter Enzyme;

        [FileSelectionParameter(DisplayName = "Result path (for testing only)", IsAdvanced = true)]
        public FileSelectionParameter ResultPath;

        [Score(ProteomicsDataTypes.Psms, true, ScoreCategoryType.HigherIsBetter,
            Description = "The score of the DiaNN scoring function.",
            DisplayName = "CScore", FormatString = "F2", Guid = "6B651DAB-FF3A-4B45-9D40-263B362C91DA")]
        public Score CScore;

        private const string DebugCategory = "Debug";

        [BooleanParameter(Category = DebugCategory, DisplayName = "Debug mode",
            IsAdvanced = true, DefaultValue = "true")]
        public BooleanParameter DebugMode;

        [FileSelectionParameter(Category = DebugCategory, DisplayName = "Input file",
            IsAdvanced = true)]
        public FileSelectionParameter DebugInputFile;

        public override void OnNodeInitialized()
        {
            if (!File.Exists(ApplicationPath.Value))
            {
                SendAndLogErrorMessage($"Invalid application path given: {ApplicationPath.Value}");
                throw new FileNotFoundException();
            }
        }

        protected override void OnSpectraSentForSearch(IProcessingNode sender, MassSpectrumCollection spectra)
        {
            SendAndLogVerboseMessage($"{spectra.Count} spectra received.");
            spectrumMapper.Add(spectra);
        }

        protected override void OnAllSpectraSentForSearch()
        { 
            SendAndLogVerboseMessage("All spectra received.");

            var workflow = ProcessingServices.CurrentWorkflow.GetWorkflow();
            var spectrumFiles = workflow.GetWorkflowInputFiles().ToArray();
            spectrumMapper.Add(spectrumFiles);

            if (DebugMode.Value)
            {
                PersistPeptides(DebugInputFile.Value);
            }
            else
            { 
                RunDiaNN(spectrumFiles, out var reportFileName, out var groupingFileName, out var _);
                PersistPeptides(reportFileName);
                // TODO precursor areas!
                //PersistProteinGroups(groupingFileName, spectrumFiles, proteinMap);
            }
        }

        private void RunDiaNN(IEnumerable<SpectrumFile> spectrumFiles, out string reportFileName, out string groupingFileName, out string libraryFileName)
        {
            reportFileName = ResultPath.IsValueSet ? ResultPath.Value : Path.Combine(NodeScratchDirectory, "report.tsv");
            groupingFileName = Path.ChangeExtension(reportFileName, "pr_matrix.tsv");
            libraryFileName = Path.ChangeExtension(reportFileName, ".lib.tsv");

            if (ResultPath.IsValueSet)
                return;

            var diannService = new DiaNNService(ApplicationPath.Value);

            diannService.OutputReceived += text => SendAndLogVerboseMessage(text);
            diannService.ErrorReceived += text => SendAndLogVerboseMessage(text);

            diannService.SetSpectrumFiles(spectrumFiles);
            diannService.SetOutputFile(reportFileName);
            diannService.SetOutputLibrary(libraryFileName);

            diannService.SetFastaFile(FastaFile.Value);
            diannService.SetEnzyme(Enzyme.Enzyme);

            diannService.Run();
        }

        private void PersistPeptides(string fileName)
        {
            var peptides = ResultReader.GetPeptideMatches(fileName).ToArray();
            var modificationMap = ProcessingServices.AminoAcidModificationService.GetAllModifications().ToLookup(m => m.UnimodAccession);

            var spectrum = default(Spectrum);
            var psmGroups = (from peptide in peptides
                             where spectrumMapper.TryGetSpectrumId(peptide.FileName, peptide.ScanNumber, out spectrum)
                             group (peptide, spectrum) by spectrum.SpectrumId into peptideGroup
                             select peptideGroup).ToArray();

            var psmCollection = new PeptideSpectrumMatchesCollection();

            if (!EntityDataService.ContainsEntity<LcmsFeature>())
                EntityDataService.RegisterEntity<LcmsFeature>(ProcessingNodeID);

            var features = new List<LcmsFeature>();

            foreach (var psmGroup in psmGroups.Where(p => p.Key != -1))
            {
                var psms = new PeptideSpectrumMatches(psmGroup.Key, 1);

                foreach (var psm in psmGroup)
                {
                    var match = CreatePeptideMatch(psm.peptide, modificationMap);
                    psms.AddMatch(match);

                    var feature = CreateLcmsFeature(psm.peptide, psm.spectrum);
                    features.Add(feature);
                }

                ProcessingServices.PeptideSpectrumMatchService.CalculateAndAssignRanksAndDeltaScores(MainPsmScore, psms);
                psmCollection.Add(psms);
            }

            PersistTargetPeptideSpectrumMatches(psmCollection);
            ProcessingServices.PeptideSpectrumMatchService.TransferAndPersistConnectedTargetProteins(this, psmCollection, FastaFile.Value);

            EntityDataService.InsertItems(features);
        }

        private PeptideMatch CreatePeptideMatch(Peptide peptide, ILookup<long, AminoAcidModificationDO> modificationMap)
        {
            var match = ProcessingServices.PeptideSpectrumMatchService.CreatePeptideMatch(peptide.Sequence, 1, 1, 0);

            match.SearchEngineRank = 1; // TODO assign rank
            match.Confidence = MatchConfidence.High;
            match.AddScore(nameof(CScore), peptide.Score);

            foreach (var proteinId in peptide.ProteinIds)
                match.AddProteinID(proteinId);

            // TODO consider N- or T-terminal mods?
            foreach (var modification in peptide.Modifications)
            {
                var modificationMatches = modificationMap[modification.UnimodId];
                if (modificationMatches.Any())
                {
                    var peptideModification = new PeptideModification(modificationMatches.First().ID, modification.Position);
                    match.AddModification(peptideModification);
                }
            }

            return match;
        }

        private LcmsFeature CreateLcmsFeature(Peptide peptide, Spectrum spectrum)
        {
            return new LcmsFeature
            {
                Id = EntityDataService.NextId<LcmsFeature>(),

                FileId = spectrum.FileId,
                Intensity = spectrum.Intensity,
                ChargeState = spectrum.Charge,
                MonoisotopicMassOverCharge = spectrum.MassOverCharge,

                ApexRT = peptide.RT,
                LeftRT = peptide.RTStart,
                RightRT = peptide.RTStop,
                Area = peptide.Area,
            };
        }
        
        // TODO implement
        private LcmsPeak CreateLcmsPeak()
        {
            return new LcmsPeak
            {
            };
        }
    }
}