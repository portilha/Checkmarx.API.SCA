using Checkmarx.API.SCA;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace Checkmarx.API.SCA
{
    public class SCAClient
    {
        public const string NOT_EXPLOITABLE_STATE = "NotExploitable";
        public const string TO_VERIFY = "ToVerify";

        public const string SCAN_DONE = "Done";

        private Uri _acUrl;
        private Uri _baseURL;

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly HttpClient _acHttpClient = new HttpClient();

        private string _username;
        private string _password;
        private string _tenant;

        private DateTime _bearerValidTo;


        private AccessControlClient _ac = null;
        public AccessControlClient AC
        {
            get
            {
                if (_ac == null && ClientSCA != null)
                {
                    _ac = new AccessControlClient(_acHttpClient)
                    {
                        BaseUrl = _acUrl.AbsoluteUri
                    };
                }
                return _ac;
            }
        }


        private Client _clientSCA = null;

        public Client ClientSCA
        {
            get
            {
                if (_clientSCA == null || (_bearerValidTo - DateTime.UtcNow).TotalMinutes < 5)
                {
                    var token = Autenticate(_tenant, _username, _password);
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    // _httpClient.DefaultRequestHeaders.Add("Team", string.Empty);
                    
                    // _httpClient.DefaultRequestHeaders.Add("Host", Environment.MachineName);

                    _acHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    _clientSCA = new Client(_httpClient)
                    {
                        BaseUrl = _baseURL.AbsoluteUri
                    };

                    _bearerValidTo = DateTime.UtcNow.AddHours(1);
                }
                return _clientSCA;
            }
        }



        public SCAClient(
            string tenant,
            string username,
            string password,
            string acUrl = "https://platform.checkmarx.net",
            string apiUrl = "https://api-sca.checkmarx.net")
        {
            if (string.IsNullOrEmpty(tenant)) throw new ArgumentNullException(nameof(tenant));
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            if (string.IsNullOrEmpty(acUrl)) throw new ArgumentNullException(nameof(acUrl));
            if (string.IsNullOrEmpty(apiUrl)) throw new ArgumentNullException(nameof(apiUrl));

            _username = username;
            _password = password;
            _tenant = tenant;
            _acUrl = new Uri(acUrl);
            _baseURL = new Uri(apiUrl);
        }


        public bool Connected
        {
            get
            {
                return ClientSCA != null;
            }
        }

        private string Autenticate(string tenant, string username, string password)
        {
            var identityURL = $"{_acUrl}identity/connect/token";

            var kv = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "client_id", "sca_resource_owner" },
                { "scope", "sca_api access_control_api" },
                { "username", username },
                { "password", password},
                { "acr_values", "Tenant:" + tenant }
            };
            var req = new HttpRequestMessage(HttpMethod.Post, identityURL) { Content = new FormUrlEncodedContent(kv) };
            var response = _httpClient.SendAsync(req).Result;
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                JObject accessToken = JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result);
                string authToken = ((JProperty)accessToken.First).Value.ToString();
                return authToken;
            }
            throw new Exception(response.Content.ReadAsStringAsync().Result);
        }

        public void EnableExploitablePathForAllProjects()
        {
            foreach (var project in ClientSCA.GetProjectsAsync().Result)
            {
                ClientSCA.UpdateProjectsSettingsAsync(project.Id,
                            new API.SCA.ProjectSettings { EnableExploitablePath = true }).Wait();
            }
        }


        

        /// <summary>
        /// Name -> Project
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, Project> GetProjects()
        {
            var scaProjectsKist = ClientSCA.GetProjectsAsync().Result;

            var scaProjects = new Dictionary<string, Project>(StringComparer.InvariantCultureIgnoreCase);

            // Support when there are duplicated project inside SCA.
            foreach (var scaProj in scaProjectsKist)
            {
                if (scaProjects.ContainsKey(scaProj.Name))
                    continue;

                scaProjects.Add(scaProj.Name, scaProj);
            }

            

            return scaProjects;
        }

        public void ScanWithSourceCode(Guid projectID, string sourceCodePath)
        {
            var resultLink = ClientSCA.GenerateUploadLinkAsync(new ProjectBody
            {
                ProjectId = projectID
            }).Result;

            using (FileStream fs = File.OpenRead(sourceCodePath))
            {
                ClientSCA.UploadLinkAsync(resultLink.UploadUrl, fs).Wait();
            }

            var scanId = ClientSCA.UploadedZipAsync(new UploadSourceCodeBody
            {
                ProjectId = projectID,
                UploadedFileUrl = resultLink.UploadUrl
            }).Result;
        }

        // github 
        // guid projectid
        // url 
        // branch

        /// <summary>
        /// Scans the SCA project with a git repository.
        /// </summary>
        /// <param name="projectID">guid of the SCA project</param>
        /// <param name="gitRepository">Url of the repository</param>
        /// <param name="apiKey>null if the repository is public</param>
        /// <exception cref="NotImplementedException"></exception>
        public UploadCodeResponse ScanWithGitRepository(Guid projectID, Uri gitRepository, string apiKey = null)
        {

            throw new NotImplementedException();

            //var resultLink = ClientSCA.GenerateUploadLinkAsync(new ProjectBody
            //{
            //    ProjectId = projectID
            //}).Result;

            // ClientSCA.UploadLinkAsync(resultLink.UploadUrl, fs).Wait();

            var scanId = ClientSCA.UploadedZipAsync(new UploadSourceCodeBody
            {
                ProjectId = projectID,
                Type = "git",
                UploadedFileUrl = gitRepository
            }).Result;

            return scanId;
        }

        /// <summary>
        /// Returns all the scans with the Done status.
        /// </summary>
        /// <param name="projectId">Id of the projects</param>
        /// <returns></returns>
        public IEnumerable<Scan> GetSuccessfullScansForProject(Guid projectId)
        {
            return ClientSCA.GetScansForProjectAsync(projectId).Result.Where(s => s.Status?.Name == SCAN_DONE);
        }

        /// <summary>
        /// Sets all the vulnerabilities of the package as NotExploitable
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="packageId"></param>
        /// <returns>Number of vulnerabilities found for the package</returns>
        public int SetPackageAsSecure(Guid projectId, string packageId)
        {
            if (projectId == Guid.Empty)
                throw new ArgumentOutOfRangeException(nameof(projectId));

            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentNullException(nameof(packageId));

            var proj = ClientSCA.GetProjectAsync(projectId).Result;

            int numberOfVulnerabilities = 0;

            foreach (var scan in GetSuccessfullScansForProject(proj.Id))
            {
                foreach (var vulnerability in ClientSCA.VulnerabilitiesAsync(scan.ScanId).Result.Where(x => x.PackageId == packageId))
                {
                    numberOfVulnerabilities++;

                    ClientSCA.PackageRiskStateAsync(new Client.PackageState
                    {
                        packageId = packageId,
                        projectId = projectId,
                        state = NOT_EXPLOITABLE_STATE,
                        vulnerabilityId = vulnerability.Id
                    }).Wait();
                }
            }

            return numberOfVulnerabilities;
        }

      
    }
}
