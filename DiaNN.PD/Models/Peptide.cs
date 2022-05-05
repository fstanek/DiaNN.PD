using System.Collections.Generic;

namespace DiaNN.PD.Models
{
    public class Peptide
    {
        public string FileName { get; set; }
        public int ScanNumber { get; set; }
        public List<int> ProteinIds { get; set; }

        public string Sequence { get; set; }
        public List<Modification> Modifications { get; set; }

        public double Score { get; set; }
        public double Area { get; set; }

        // TODO use for feature creation
        public double RT { get; set; }
        public double RTStart { get; set; }
        public double RTStop { get; set; }
    }

    /*
     * Columns of report.tsv:
            File.Name
            Run
            Protein.Group
            Protein.Ids
            Protein.Names
            Genes
            PG.Quantity
            PG.Normalised
            PG.MaxLFQ
            Genes.Quantity
            Genes.Normalised
            Genes.MaxLFQ
            Genes.MaxLFQ.Unique
            Modified.Sequence
            Stripped.Sequence
            Precursor.Id
            Precursor.Charge
            Q.Value
            PEP
            Global.Q.Value
            Protein.Q.Value
            PG.Q.Value
            Global.PG.Q.Value
            GG.Q.Value
            Translated.Q.Value
            Proteotypic
            Precursor.Quantity
            Precursor.Normalised
            Precursor.Translated
            Ms1.Translated
            Quantity.Quality
            RT
            RT.Start
            RT.Stop
            iRT
            Predicted.RT
            Predicted.iRT
            First.Protein.Description
            Lib.Q.Value
            Lib.PG.Q.Value
            Ms1.Profile.Corr
            Ms1.Area
            Evidence
            Spectrum.Similarity
            Mass.Evidence
            CScore
            Decoy.Evidence
            Decoy.CScore
            Fragment.Quant.Raw
            Fragment.Quant.Corrected
            Fragment.Correlations
            MS2.Scan
            IM
            iIM
            Predicted.IM
            Predicted.iIM
     */
}