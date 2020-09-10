using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace PostgreSqlBackuptoAzureTool
{
    public class PostgreBackupLogs
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        public int DBBackupInfoId { get; set; }
        [Required]
        public string DatabaseServer { get; set; }
        [Required]
        public string DatabaseName { get; set; }
        [Required]
        public string DatabaseUserName { get; set; }
        [Required]
        public string DatabasePassword { get; set; }
        [Required]
        public string DatabasePort { get; set; }
        [Required]
        public int NoOfBackupFiles { get; set; }
        public string Category { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}
