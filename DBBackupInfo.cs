using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace PostgreSqlBackuptoAzureTool
{
    public class DBBackupInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
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
        public Boolean WantBackup { get; set; } = true;
    }
}
