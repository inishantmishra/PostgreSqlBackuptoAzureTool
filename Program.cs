using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PostgreSqlBackuptoAzureTool
{
    class Program
    {
        public static IConfigurationRoot Configuration;

        static void Main(string[] args)
        {

            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(AppContext.BaseDirectory))
                .AddJsonFile("appSettings.json", optional: true);

            Configuration = builder.Build();

            try
            {
                GetDataForBackup();
            }
            catch (Exception ex)
            {
                ErrorLog(Configuration.GetSection("BackupSettings:BackupPath").Value + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "ExceptionMain- " + ex.Message);
                using (var context = new AppDBContext(Configuration))
                {
                    ExceptionLog log = new ExceptionLog();
                    log.DMSServiceInfoId = 0;
                    if (ex.InnerException == null)
                        log.InnerException = string.Empty;
                    else
                        log.InnerException = ex.InnerException.Message;

                    if (ex.StackTrace == null)
                        log.StackTrace = string.Empty;
                    else
                        log.StackTrace = ex.StackTrace.ToString();
                    log.ExceptionMessage = "Error Executing while Backup- " + ex.Message.ToString();
                    context.ExceptionLogs.Add(log);
                    context.SaveChanges();
                }
                throw ex;
            }
        }

        public static void ErrorLog(string sPathName, string sErrMsg)
        {
            StreamWriter sw = new StreamWriter(sPathName, true);
            sw.WriteLine(sErrMsg + "\n");
            sw.Flush();
            sw.Close();
        }

        public static void GetDataForBackup()
        {
            string backuppath = Configuration.GetSection("BackupSettings:BackupPath").Value;

            using (var context = new AppDBContext(Configuration))
            {
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                List<DBBackupInfo> dbBackupList = new List<DBBackupInfo>();
                dbBackupList = context.DBBackupInfo.AsNoTracking()
                                 .Where(x => x.WantBackup)
                                 .OrderBy(x => x.Id).ToList();
                watch.Stop();
                ErrorLog(backuppath + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "Count is- " + dbBackupList.Count().ToString());
                ErrorLog(backuppath + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "Time taken for fetchign data from DB is- " + watch.ElapsedMilliseconds.ToString() + "ms");

                foreach (var db in dbBackupList)
                {
                    try
                    {
                        string dbname = db.DatabaseName.ToLower();
                        string server = db.DatabaseServer.ToLower();
                        string backupFile = backuppath + dbname + Configuration.GetSection("BackupSettings:Extension").Value;
                        string destcontaineName= Configuration.GetSection("GenericStorageSettings:ContainerName").Value.ToLower();

                        ErrorLog(backuppath + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", dbname + server);

                        if (!watch.IsRunning)
                            watch.Restart();

                        TakeBackupofPostgreSql(db, backupFile);

                        watch.Stop();

                        ErrorLog(backuppath + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "Time taken for Backup of DB is- " + watch.ElapsedMilliseconds.ToString() + "ms and DB is- " + db.DatabaseName);

                        string destinationConnectionString = Configuration.GetSection("GenericStorageSettings:ConnectionString").Value;
                        BlobServiceClient destBlobClient = new BlobServiceClient(destinationConnectionString);

                        var destContainers = destBlobClient.GetBlobContainers();
                        if (destContainers.Where(b => b.Name == destcontaineName).Count() == 0)
                        {
                            destBlobClient.CreateBlobContainer(destcontaineName);
                        }


                        BlobContainerClient destContainerClient = destBlobClient.GetBlobContainerClient(destcontaineName);

                            if (!watch.IsRunning)
                                watch.Restart();

                            DeleteOldFiles(destContainerClient,dbname);

                            watch.Stop();
                            ErrorLog(backuppath + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "Time taken for Deleting files over Azure is- " + watch.ElapsedMilliseconds.ToString() + "ms");
                        
                        if (!watch.IsRunning)
                            watch.Restart();

                        ExecuteCopyOverAzure(destContainerClient, server, dbname, backupFile.ToLower(), db, context);
                        watch.Stop();
                        ErrorLog(backuppath + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "Time taken for uploading the backup pver azure is- " + watch.ElapsedMilliseconds.ToString() + "ms");

                    }
                    catch (Exception ex)
                    {
                        ErrorLog(Configuration.GetSection("BackupSettings:BackupPath").Value + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "Exception1- " + ex.Message);
                        //ErrorLog(Configuration.GetSection("BackupSettings:BackupPath").Value + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "Exception1 Deatila- " + db.Id.ToString() + "-" + ex.InnerException.ToString() + "-" + ex.StackTrace.ToString());
                        ExceptionLog log = new ExceptionLog();
                        log.DMSServiceInfoId = db.Id;
                        if (ex.InnerException == null)
                            log.InnerException = string.Empty;
                        else
                            log.InnerException = ex.InnerException.Message;

                        if (ex.StackTrace == null)
                            log.StackTrace = string.Empty;
                        else
                            log.StackTrace = ex.StackTrace.ToString();
                        log.ExceptionMessage = "Error Executing while Backup- " + ex.Message.ToString();
                        context.ExceptionLogs.Add(log);
                        context.SaveChanges();
                        throw ex;
                    }
                }
            }
        }


        public static void TakeBackupofPostgreSql(DBBackupInfo db, string backupFile)
        {
            string server = db.DatabaseServer;
            string port = db.DatabasePort;
            string user = db.DatabaseUserName;
            string password = db.DatabasePassword;
            string dbname = db.DatabaseName;
            string backupCommandDir = Path.Combine(AppContext.BaseDirectory) + "lib\\";

            string command = "-h " + server + " -p " + port + " -U " + user + " -F c -b -v -f " + backupFile + " " + dbname;

            using (Process p = new Process())
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.FileName = backupCommandDir + "pg_dump.exe";
                p.StartInfo.EnvironmentVariables.Add("PGPASSWORD", password);
                p.StartInfo.Arguments = command;

                ErrorLog(Configuration.GetSection("BackupSettings:BackupPath").Value + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "Inside Process- Arguments is- " + command);

                p.Start();
                p.WaitForExit();
                p.Close();

            }
            ErrorLog(Configuration.GetSection("BackupSettings:BackupPath").Value + "\\ErrorLog" + DateTime.Now.ToString("yyyy-dd-M") + ".txt", "Inside Process- Arguments is- " + command);
        }

        public static void DeleteOldFiles(BlobContainerClient destContainerClient,string dbName)
        {
            var blobs = destContainerClient.GetBlobs().ToList();
            bool dailyBackup = Configuration.GetSection("ScheduleSettings:Daily").Value.ToLower() == "true" ? true : false;
            bool weeklyBackup = Configuration.GetSection("ScheduleSettings:Weekly").Value.ToLower() == "true" ? true : false;
            foreach (var blob in blobs)
            {
                if (dailyBackup && blob.Name.Contains(dbName+"-Daily"))
                {
                    destContainerClient.DeleteBlob(blob.Name);
                }
                if (weeklyBackup && blob.Name.Contains(dbName+"-Weekly"))
                {
                    destContainerClient.DeleteBlob(blob.Name);
                }
            }
        }

        public static void ExecuteCopyOverAzure(BlobContainerClient destContainerClient, string server, string dbname, string backupFile, DBBackupInfo db, AppDBContext context)
        {
            bool dailyBackup = Configuration.GetSection("ScheduleSettings:Daily").Value.ToLower() == "true" ? true : false;
            bool weeklyBackup = Configuration.GetSection("ScheduleSettings:Weekly").Value.ToLower() == "true" ? true : false;

            int dailyCounter = 0;
            int weeklyCounter = 0;
            string fileName = dbname + Configuration.GetSection("BackupSettings:Extension").Value;

            if (dailyBackup)
            {
                BlobClient destBlob = destContainerClient.GetBlobClient(server + "/"+dbname+"-Daily/" + fileName);
                try
                {
                    // Start the copy operation.
                    destBlob.Upload(backupFile);
                    dailyCounter = dailyCounter + 1;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            if (weeklyBackup)
            {
                BlobClient destBlob = destContainerClient.GetBlobClient(server + "/" + dbname +"-Weekly/" + fileName);
                // Start the copy operation.
                destBlob.Upload(backupFile);
                weeklyCounter = weeklyCounter + 1;
            }

            if (dailyBackup)
            {
                PostgreBackupLogs log = new PostgreBackupLogs();
                log.Category = "Daily";
                log.DatabaseName = db.DatabaseName;
                log.DatabasePassword = db.DatabasePassword;
                log.DatabaseUserName = db.DatabaseUserName;
                log.DatabaseServer = db.DatabaseServer;
                log.NoOfBackupFiles = dailyCounter;
                log.DBBackupInfoId = db.Id;
                log.DatabasePort = db.DatabasePort;
                log.CreatedOn = DateTime.Now;
                context.PostgreBackupLogs.Add(log);
            }
            if (weeklyBackup)
            {
                PostgreBackupLogs log = new PostgreBackupLogs();
                log.Category = "Weekly";
                log.DatabaseName = db.DatabaseName;
                log.DatabasePassword = db.DatabasePassword;
                log.DatabaseUserName = db.DatabaseUserName;
                log.DatabaseServer = db.DatabaseServer;
                log.NoOfBackupFiles = dailyCounter;
                log.DBBackupInfoId = db.Id;
                log.DatabasePort = db.DatabasePort;
                log.CreatedOn = DateTime.Now;
                context.PostgreBackupLogs.Add(log);
            }
            context.SaveChanges();
        }
    }
}
