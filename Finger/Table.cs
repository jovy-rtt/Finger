namespace Finger
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Table")]
    public partial class Table
    {
        public int Id { get; set; }

        public string picture { get; set; }

        [StringLength(20)]
        public string name { get; set; }

        public DateTime? time { get; set; }

        public string details { get; set; }
    }
}
