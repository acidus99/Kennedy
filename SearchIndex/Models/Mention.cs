using System;
using System.Collections.Generic;

using System.ComponentModel.DataAnnotations.Schema;

namespace Kennedy.SearchIndex.Models
{
    [Table("Mentions")]
    public class Mention
	{
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public required string Name { get; set; }

        public List<Document> Documents { get; set; } = new();
	}
}

