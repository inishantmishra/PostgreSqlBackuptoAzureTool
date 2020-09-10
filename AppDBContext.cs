using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace PostgreSqlBackuptoAzureTool
{
    public class AppDBContext : DbContext
    {
        private readonly IConfiguration _Config;

        public AppDBContext(IConfiguration config)
        {
            _Config = config;
        }

        public AppDBContext(DbContextOptions<AppDBContext> options)
            : base(options)
        {
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

            optionsBuilder.UseNpgsql(_Config.GetConnectionString("DefaultConnection"),
                options=>options.EnableRetryOnFailure());
        }
        public virtual DbSet<DBBackupInfo> DBBackupInfo { get; set; }
        public virtual DbSet<ExceptionLog> ExceptionLogs { get; set; }
        public virtual DbSet<PostgreBackupLogs> PostgreBackupLogs { get; set; }

    }
}
