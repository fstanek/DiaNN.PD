using System.Collections.Generic;

namespace DiaNN.PD.Models
{
    public class ProteinGroup
    {
        public string Name { get; set; }
        public List<int> ProteinIds { get; set; }
        public List<double?> Areas { get; set; }
    }

    /*
     * Columns of report.pg_matrix.tsv:
            Protein.Group
            Protein.Ids
            Protein.Names
            Genes
            First.Protein.Description
            [sample areas by filename]
     */
}