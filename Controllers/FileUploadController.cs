using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DocumentAnalyzer.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Newtonsoft.Json.Linq;

namespace DocumentAnalyzer.Controllers
{
    public class FileUploadController : Controller
    {
        // Add your Computer Vision subscription key and endpoint to your environment variables.
        static string subscriptionKey = @"";

        static string endpoint = @"";
        static string uriBase = endpoint + "vision/v1.0/ocr";



        [HttpPost("FileUpload")]
        public IActionResult Index(List<IFormFile> files)
        {
            long size = files.Sum(f => f.Length);
            StringBuilder sb = new StringBuilder();

            var filePaths = new List<string>();
            foreach (var formFile in files)
            {
                if (formFile.Length > 0)
                {
                    // full path to file in temp location
                    var filePath = Path.GetTempFileName();
                    filePaths.Add(filePath);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        formFile.CopyToAsync(stream).Wait();
                    }


                    List<string> foundText = new List<string>();
                    JToken docData = MakeOCRRequest(filePath).Result;
                    using (StringReader sr = new StringReader(docData.ToString()))
                    {
                        string line = "";
                        while ((line = sr.ReadLine()) != null)
                        {
                            string lineTrimm = line.Trim();
                            if (lineTrimm.StartsWith("\"text\":"))
                            {
                                foundText.Add(lineTrimm.Split(":")[1].Trim().Replace("\"", ""));
                            }
                        }
                    }

                    PersonDTO person = new PersonDTO();

                    person.LastName = foundText[foundText.FindIndex(f => f == "SURNAME") + 1];
                    person.FirstName = "";

                    int indexName = foundText.FindIndex(f => f == "NAMES") + 1;
                    string space = "";
                    while (foundText[indexName] != "SUGU")
                    {
                        person.FirstName += space + foundText[indexName];
                        indexName++;
                        space = " ";
                    }

                    person.IdCode = foundText[foundText.FindIndex(f => f == "CODE") + 3];
                    ViewBag.Person = person;
                }
            }

            // process uploaded files
            // Don't rely on or trust the FileName property without validation.
            return View("~/Views/Home/Index.cshtml");
        }

        public static ComputerVisionClient Authenticate(string endpoint, string key)
        {
            ComputerVisionClient client =
                new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
                { Endpoint = endpoint };
            return client;
        }
                      
        /// <summary>
        /// Gets the text visible in the specified image file by using
        /// the Computer Vision REST API.
        /// </summary>
        /// <param name="imageFilePath">The image file with printed text.</param>
        static async Task<JToken> MakeOCRRequest(string imageFilePath)
        {
            try
            {
                HttpClient client = new HttpClient();

                // Request headers.
                client.DefaultRequestHeaders.Add(
                    "Ocp-Apim-Subscription-Key", subscriptionKey);

                // Request parameters. 
                // The language parameter doesn't specify a language, so the 
                // method detects it automatically.
                // The detectOrientation parameter is set to true, so the method detects and
                // and corrects text orientation before detecting text.
                string requestParameters = "language=unk&detectOrientation=true";

                // Assemble the URI for the REST API method.
                string uri = uriBase + "?" + requestParameters;

                HttpResponseMessage response;

                // Read the contents of the specified local image
                // into a byte array.
                byte[] byteData = GetImageAsByteArray(imageFilePath);

                // Add the byte array as an octet stream to the request body.
                using (ByteArrayContent content = new ByteArrayContent(byteData))
                {
                    // This example uses the "application/octet-stream" content type.
                    // The other content types you can use are "application/json"
                    // and "multipart/form-data".
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/octet-stream");

                    // Asynchronously call the REST API method.
                    response = await client.PostAsync(uri, content);
                }

                // Asynchronously get the JSON response.
                string contentString = await response.Content.ReadAsStringAsync();

                // Display the JSON response.
                return JToken.Parse(contentString).ToString();
            }
            catch (Exception e)
            {
                return JToken.Parse("exception:" + e.Message);
            }
            finally
            {
                System.IO.File.Delete(imageFilePath);
            }
        }

        /// <summary>
        /// Returns the contents of the specified file as a byte array.
        /// </summary>
        /// <param name="imageFilePath">The image file to read.</param>
        /// <returns>The byte array of the image data.</returns>
        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            // Open a read-only file stream for the specified file.
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                // Read the file's contents into a byte array.
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }
    }
}