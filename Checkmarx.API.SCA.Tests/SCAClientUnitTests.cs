using Checkmarx.API.SCA;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using static Checkmarx.API.SCA.Client;

namespace Checkmarx.API.SCA.Tests
{
    [TestClass]
    public class SCAClientUnitTests
    {
        public static IConfigurationRoot Configuration { get; set; }

        private static string Username;
        private static string Password;
        private static string Tenant;

        public static string APIUrl { get; private set; }
        public static string AcUrl { get; private set; }

        private static SCAClient _client;

        private Guid TestProject = Guid.Empty;
        private Guid TestScan = Guid.Empty;

        // Defaults, 
        //private static string AC = "https://platform.checkmarx.net";
        //private static string APIURL = "https://api-sca.checkmarx.net";

        [ClassInitialize]
        public static void InitializeTest(TestContext testContext)
        {
            var builder = new ConfigurationBuilder()
                .AddUserSecrets<SCAClientUnitTests>();

            Configuration = builder.Build();

            Username = Configuration["Username"];
            Password = Configuration["Password"];
            Tenant = Configuration["Tenant"];

            APIUrl = Configuration["APIUrl"];
            AcUrl = Configuration["AcUrl"];

            Assert.IsNotNull(Username, "Please define the Username in the Secrets file");
            Assert.IsNotNull(Password, "Please define the Password in the Secrets file");
            Assert.IsNotNull(Tenant, "Please define the Tenant in the Secrets file");

            _client = new SCAClient(Tenant, Username, Password, AcUrl, APIUrl);
        }

        [TestInitialize]
        public void InitiateTestGuid()
        {
            string testGuid = Configuration["TestProject"];
            if (!string.IsNullOrWhiteSpace(testGuid))
                TestProject = new Guid(testGuid);

            string testScan = Configuration["TestScan"];
            if (!string.IsNullOrWhiteSpace(testScan))
                TestScan = new Guid(testScan);
        }


        [TestMethod]
        public void ConnectionTest()
        {
            Assert.IsTrue(_client.Connected);
        }

        [TestMethod]
        public void SCA_AC_Test()
        {
            AccessControlClient accessControlClient = _client.AC;
            foreach (var item in accessControlClient.TeamsAllAsync().Result.ToDictionary(x => x.FullName, StringComparer.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"{item.Key} - {item.Value.Id} = {item.Value.Name} - {item.Value.ParentId}");
            }
        }

        [TestMethod]
        public void ListProjects()
        {
            foreach (var project in _client.GetProjects())
            {
                Trace.WriteLine(project.Key + " " + project.Value.Id);
            }
        }


        [TestMethod]
        public void GetPackagesByDependencyTest()
        {
            foreach (var project in _client.GetProjects())
            {
                foreach (var scan in _client.GetSuccessfullScansForProject(project.Value.Id))
                {
                    foreach (var package in _client.ClientSCA.PackagesAsync(scan.ScanId).Result)
                    {
                        Trace.WriteLine(package.PackageRepository);
                    }
                }
            }
        }

        [TestMethod]
        public void SetPackageAsSafeTest()
        {
            Guid scanUID = new Guid("0068cba5-ed9f-4168-b4fb-dec07fe81448");

            foreach (var package in _client.ClientSCA.PackagesAsync(scanUID).Result)
            {
                

                _client.ClientSCA.PackageRiskStateAsync(new PackageState
                {
                    packageId = package.Id, 
                    state = "",
                    
                });
            }

        }


        [TestMethod]
        public void SCA_List_Users_Test()
        {
            AccessControlClient accessControlClient = _client.AC;

            var roles = accessControlClient.RolesAllAsync().Result.ToDictionary(x => x.Id);
            var teams = accessControlClient.TeamsAllAsync().Result.ToDictionary(x => x.Id);

            foreach (var user in accessControlClient.GetAllUsersDetailsAsync().Result.Where(x => x.Active))
            {

                Trace.WriteLine(user.UserName + " " + user.ExpirationDate);

                accessControlClient.UpdateUserDetails(user.Id,
                    new UpdateUserModel
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        AllowedIpList = user.AllowedIpList,
                        CellPhoneNumber = user.CellPhoneNumber,
                        Country = user.Country,
                        Email = user.Email,
                        JobTitle = user.JobTitle,
                        LocaleId = user.LocaleId,
                        Other = user.Other,
                        RoleIds = user.RoleIds,
                        TeamIds = user.TeamIds,
                        PhoneNumber = user.PhoneNumber,
                        Active = user.Active,
                        ExpirationDate = new DateTimeOffset(new DateTime(2025, 10, 01))
                    }).Wait();

                //if (user.Email.EndsWith("@checkmarx.com"))
                //{
                //    Trace.WriteLine(user.Email + string.Join(";", user.TeamIds.Select(x => teams[x].FullName)) + " " + user.LastLoginDate);

                //    foreach (var role in user.RoleIds.Select(x => roles[x].Name))
                //    {
                //        Trace.WriteLine("+ " + role);
                //    }
                //}
            }
        }

        [TestMethod]
        public void ListAllProjects()
        {
            var allUser = new Dictionary<string, UserViewModel>(StringComparer.OrdinalIgnoreCase);

            foreach (var user in _client.AC.GetAllUsersDetailsAsync().Result)
            {
                if (!allUser.ContainsKey(user.UserName))
                    allUser.Add(user.UserName, user);
            }

            StringBuilder stringBuilder = new StringBuilder("sep=;\nId;Name;Teams;CreationDate;User;Username\n");

            foreach (var project in _client.ClientSCA.GetProjectsAsync().Result)
            {
                if (project.CreatedOn <= (DateTime.Now - TimeSpan.FromDays(90)))
                {
                    continue;
                }

                var collection = _client.ClientSCA.GetScansForProjectAsync(project.Id).Result;

                if (!collection.Any())
                    continue;

                var last = collection.Last();

                var username = last.AdditionalProperties["username"].ToString();
                var user = allUser.ContainsKey(username) ? allUser[username] : null;

                var userName = user != null ? user.FirstName + " " + user.LastName : "Not found";

                stringBuilder.AppendLine($"\"{project.Id}\";\"{project.Name}\";" +
                    $"\"{string.Join(",", project.AssignedTeams)}\"" +
                    $";\"{project.CreatedOn?.DateTime.ToShortDateString()}\"" +
                    $";\"{userName}\";\"{username}\"");

                // Trace.WriteLine(last.AdditionalProperties["username"]);

                // Trace.WriteLine(project.Id + "  " + project.Name);
            }

            File.WriteAllText(@"C:\Users\pedropo\OneDrive - Checkmarx\ASA Delivery\EMEA\BP P.L.C\3month.csv", stringBuilder.ToString());
        }

        [TestMethod]
        public void ListProjectsTests()
        {
            foreach (var item in _client.GetProjects())
            {
                var scans = _client.ClientSCA.GetScansForProjectAsync(item.Value.Id).Result;

                Trace.WriteLine(item.Value);

                var lastSCAa = scans.First();

                if (lastSCAa != null)
                {
                    // TODO: Make this request work
                    var result = _client.ClientSCA.GetScanReportAsync(lastSCAa.ScanId, "json").Result;
                }
            }
        }

        [TestMethod]
        public void GetAllScanFromProject()
        {
            Assert.IsTrue(TestProject != Guid.Empty, "Please define a TestProjectGuid in the secrets file");

            foreach (var scan in _client.ClientSCA.GetScansForProjectAsync(TestProject).Result.Where(x => x.Status.Name == "Done"))
            {
                Trace.WriteLine(scan.ScanId + " " + scan.Status.Name);

                foreach (var package in _client.ClientSCA.PackagesAsync(scan.ScanId).Result)
                {
                }
            }
        }

        [TestMethod]
        public void GetProject()
        {
            Assert.IsTrue(TestProject != Guid.Empty, "Please define a TestProjectGuid in the secrets file");

            var project = _client.ClientSCA.GetProjectAsync(TestProject).Result;

            Assert.IsNotNull(project);
        }

        [TestMethod]
        public void GetScan()
        {

            var scan = _client.ClientSCA.GetScanAsync(TestScan).Result;

            Assert.IsNotNull(scan);
        }

        [TestMethod]
        public void GetPackagesTest()
        {
            foreach (var item in _client.ClientSCA.PackagesAsync(TestScan).Result)
            {
                Trace.WriteLine(item.Id);
            }
        }

        [TestMethod]
        public void GetAllDevPackagesTest()
        {
            foreach (var project in _client.ClientSCA.GetProjectsAsync().Result)
            {
                Trace.WriteLine("+" + project.Name);

                var scan = _client.ClientSCA.GetScansForProjectAsync(project.Id).Result.FirstOrDefault();

                if (scan == null || scan.Status.Name != "Done")
                    continue;

                try
                {
                    foreach (var package in _client.ClientSCA.PackagesAsync(scan.ScanId).Result)
                    {
                        if (package.IsDevelopment)
                        {
                            Trace.WriteLine("\t-" + package.Id);
                        }


                        foreach (var dep in package.DependencyPaths)
                        {
                            foreach (var depdep in dep)
                            {


                                if (depdep.IsDevelopment)
                                    Trace.WriteLine(depdep.Id);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("ERROR " + ex.Message);

                }
            }
        }

        [TestMethod]
        public void GetVulnerabilitiesFromScanTest()
        {

            var vulns = _client.ClientSCA.VulnerabilitiesAsync(TestScan).Result;

            foreach (var vulnerabiltity in vulns)
            {
                Trace.WriteLine(vulnerabiltity.CveName);
            }


        }

        [TestMethod]
        public void GetReportRisk()
        {

            var riskReport = _client.ClientSCA.RiskReportsAsync(TestProject, null).Result;

            Assert.IsNotNull(riskReport);

        }

        [TestMethod]
        public void IgnoreVulnerabilityTest()
        {
            _client.ClientSCA.IgnoreVulnerabilityAsync(new IgnoreVulnerability
            {

            });
        }

        [TestMethod]
        public void GetExploitablePathTest()
        {
            foreach (var project in _client.ClientSCA.GetProjectsAsync(string.Empty).Result)
            {
                var settings = _client.ClientSCA.GetProjectsSettingsAsync(project.Id).Result;
                Trace.WriteLine(project.Name + " -> " + settings.EnableExploitablePath);
            }
        }

        [TestMethod]
        public void EnableExploitablePathForAllTest()
        {
            foreach (var project in _client.ClientSCA.GetProjectsAsync().Result)
            {
                try
                {
                    // Uncomment to execute the action
                    //_client.ClientSCA.UpdateProjectsSettingsAsync(project.Id,
                    //            new API.SCA.ProjectSettings { EnableExploitablePath = true }).Wait();

                    var settings = _client.ClientSCA.GetProjectsSettingsAsync(project.Id).Result;

                    Assert.IsTrue(settings.EnableExploitablePath);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(project.Name + " " + ex.Message);
                }
            }
        }

        [TestMethod]
        public void CreateProjectTest()
        {
            var project = _client.ClientSCA.CreateProjectAsync(new CreateProject
            {
                AssignedTeams = new string[] { "/CxServer/SCA-PM/Champions/UK" },
                Name = "MyFirstTest3"
            }).Result;

            Assert.IsNotNull(_client.ClientSCA.GetProjectAsync(project.Id).Result);
        }

        [TestMethod]
        public void GetProjectAndScanTest()
        {
            var result = _client.ClientSCA.CreateProjectAsync(new CreateProject
            {
                AssignedTeams = new string[] { "/CxServer/SCA-PM/Champions/UK" },
                Name = "MyFirstTest4"
            }).Result;

            // Assert.IsNotNull(_client.ClientSCA.GetProjectAsync(project.Id).Result);

            // var result = _client.ClientSCA.GetProjectAsync("MyFirstTest4").Result;

            Assert.IsNotNull(result);

            // Run Scan.

            _client.ScanWithSourceCode(result.Id, @"\WebGoat-develop.zip");


        }

        [TestMethod]
        public void ScanWithGitRepositoryTest()
        {
            var result = _client.ScanWithGitRepository(new Guid("c500b7f8-24e8-4a71-9e94-62afd3e61927"), new Uri("https://github.com/WebGoat/WebGoat.git"));

            Assert.IsNotNull(result);

        }


        [TestMethod]
        public void ListAllNotExploitableTest()
        {

            //foreach (var projNameProj in _client.GetProjects().Take(20))
            //{

            var project = _client.ClientSCA.GetProjectAsync("gs-spring-boot-docker-louis").Result;

            Trace.WriteLine("Get Scans for " + project.Name);

            //var scans = _client.ClientSCA.GetScansForProjectAsync(project.Id).Result;

            //if (!scans.Any())
            //{
            //    Trace.WriteLine("Not scans found");
            //    // continue;
            //}

            //foreach (var scan in scans)
            //{
            //    // var packages = client.ClientSCA.PackagesAsync(scan.ScanId).Result.ToDictionary(x => x.Id);

            foreach (var package in _client.ClientSCA.PackageStatesAsync(project.Id).Result)
            {
                if (package.state == SCAClient.NOT_EXPLOITABLE_STATE)
                {
                    Trace.WriteLine($"{project.Id} | {package.packageId} | {GetSCAVulnerabilityLink(package).AbsolutePath}");
                }
            }
            //}
            //}
        }


        public static Uri GetSCAVulnerabilityLink(PackageState package)
        {
            return new Uri($"https://sca.checkmarx.net/#/projects/{package.projectId}/reports/{package.packageId}/vulnerabilities/{package.vulnerabilityId}/vulnerabilityDetails");
        }
    }
}


