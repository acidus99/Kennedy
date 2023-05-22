using System;
using System.Collections.Generic;

using System.ComponentModel.DataAnnotations.Schema;

namespace Kennedy.SearchIndex.Models
{
    [Table("HashTags")]
    public class HashTag
	{
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public List<Document> Documents { get; set; } = new();
	}
}

