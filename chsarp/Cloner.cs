using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using LibGit2Sharp;
using System.Diagnostics;
using System.IO;
using SharpGit;

namespace wiki_cloner
{
    public class Cloner
    {
        [FunctionName("Function1")]
        public void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            var targetFolder = Environment.GetEnvironmentVariable("TargetFolder");
            var repositoryUrl = Environment.GetEnvironmentVariable("RepositoryUrl");
            var repositoryUrl2 = Environment.GetEnvironmentVariable("RepositoryUrl2");
            string patToken = Environment.GetEnvironmentVariable("ADOPATToken");
            string username = Environment.GetEnvironmentVariable("ADOUsername");
            int DirectoryDeleteThresholdInMins;
            if (!int.TryParse(Environment.GetEnvironmentVariable("DirectoryDeleteThreshold"), out DirectoryDeleteThresholdInMins))
            {
                DirectoryDeleteThresholdInMins = 30;
            }
                
            try
            {

                if (string.IsNullOrEmpty(repositoryUrl) || string.IsNullOrEmpty(targetFolder) || string.IsNullOrEmpty(patToken))
                {
                    throw new NullReferenceException("RepositoryUrl and TargetFolder must be specified in the configuration.");
                }

                // Generate the folder name using the epoch timestamp
                string folderName = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
                // Combine the target location and folder name
                string folderPath = Path.Combine(targetFolder, folderName);
                Directory.CreateDirectory(folderPath);
                log.LogInformation($"Starting cloning repository {repositoryUrl} into {folderPath}.");
                UseLibGitMethod( folderPath,  username,  patToken,  repositoryUrl,  log);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error cloning repository.");
            }
            finally
            {
                // To delte folders older than a certin time threshold
                DirectoryInfo directoryInfo = new DirectoryInfo(targetFolder);
                DirectoryInfo[] directories = directoryInfo.GetDirectories();

                foreach (var directory in directories)
                {
                    TimeSpan elapsed = DateTime.Now - directory.LastWriteTime;
                    if (elapsed > TimeSpan.FromMinutes(DirectoryDeleteThresholdInMins))
                    {
                        // Delete the folder
                        directory.Delete(true);
                        log.LogDebug($"Deleted folder: {directory.FullName}");
                    }
                }
            }
        }

        //Uses the LibGit2Sharp library
        private void UseLibGitMethod(string folderPath,string username, string patToken, string repositoryUrl, ILogger log)
        {
            if (Repository.IsValid(folderPath))
            {
                throw new AmbiguousSpecificationException($"Target folder {folderPath} already contains a valid Git repository.");
            }
            CloneOptions cloneOptions = new CloneOptions();
            cloneOptions.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials
            {
                Username = username,
                Password = patToken
            };

            var result = Repository.Clone(repositoryUrl, folderPath, cloneOptions);
            log.LogInformation($"Cloning result:{result.ToString()} ");
        }

        // Uses the process method
        private void UseProcessMethod(string repositoryUrl2, string folderPath, ILogger log)
        {

            using (Process gitProcess = new Process())
            {
                gitProcess.StartInfo.FileName = "git"; // The name of the executable
                gitProcess.StartInfo.Arguments = $"clone --config core.longpaths=true {repositoryUrl2} {folderPath}"; // The git command and its arguments
                gitProcess.StartInfo.UseShellExecute = false; // Do not use the system shell
                gitProcess.StartInfo.RedirectStandardOutput = true; // Redirect the standard output to the process
                gitProcess.StartInfo.RedirectStandardError = true; // Redirect the standard error to the process
                gitProcess.Start();
                // Read the output and error streams
                string output = gitProcess.StandardOutput.ReadToEnd();
                string error = gitProcess.StandardError.ReadToEnd();

                // Wait for the process to exit
                gitProcess.WaitForExit();
                // Check the exit code
                int exitCode = gitProcess.ExitCode;
                if (exitCode == 0)
                {
                    // The git command was successful
                    log.LogInformation($"Git command output:{output}");
            
                }
                else
                {
                    // The git command failed
                    log.LogError($"Git command error:{error}");
                }
            }
        }
    }
}
