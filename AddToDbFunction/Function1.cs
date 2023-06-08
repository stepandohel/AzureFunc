using AddToDbFunction.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PBIX_to_Flat.OutputModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace AddToDbFunction
{
    public class AddToDbFunction
    {
        [FunctionName("AddToDbFunction")]
        public async Task RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log,
            [Sql(commandText: "PBIX_to_Flat.Visuals", connectionStringSetting: "MyDb")] IAsyncCollector<Visual> visuals,
            [Sql(commandText: "PBIX_to_Flat.Filters", connectionStringSetting: "MyDb")] IAsyncCollector<Filter> filters,
            [Sql(commandText: "PBIX_to_Flat.Local_Measures", connectionStringSetting: "MyDb")] IAsyncCollector<LocalMeasure> localMeasures)

        {
            var personalAccessToken = Environment.GetEnvironmentVariable("PersonalAccessToken");
            var reportObjects = new List<ReportObject>();

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string commitId = data?.commitId;
            var commitDate = Convert.ToDateTime(data?.commitDate);

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(
                        System.Text.ASCIIEncoding.ASCII.GetBytes(
                            string.Format("{0}:{1}", "", personalAccessToken))));

                using (HttpResponseMessage response = client.GetAsync(
                Environment.GetEnvironmentVariable("CommitURL") + commitId + "/changes?api-version=7.0").Result
                )
                {
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync();
                    var responseBodyaJson = JObject.Parse(responseBody);
                    var commitChangesValue = responseBodyaJson["changes"];

                    foreach (var item in commitChangesValue)
                    {
                        var itemName = item["item"]["path"].ToString();

                        if (itemName.EndsWith(".pbix"))
                        {
                            reportObjects.Add(new ReportObject()
                            {
                                reportURL = item["item"]["url"].ToString() + "&includeContent=true",
                                reportName = Path.GetFileNameWithoutExtension(itemName),
                                modified_date = commitDate,
                                change_Type = item["changeType"].ToString(),

                            });
                        }
                    }
                }
                client.DefaultRequestHeaders.Accept.Remove(client.DefaultRequestHeaders.Accept.First());
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/zip"));

                using (SqlConnection connection = new SqlConnection(Environment.GetEnvironmentVariable("MyDb")))
                {
                    connection.Open();

                    using (SQLHelper sqlHelper = new SQLHelper(connection))
                    {
                        //foreach (var reportObject in reportObjects)
                        //{
                        //    if (reportObject.change_Type == "edit" || reportObject.change_Type == "add")
                        //    {
                        //        using (SqlDataReader reader = sqlHelper.SelectGroupById(reportObject.reportName))
                        //        {
                        //            try
                        //            {
                        //                while (reader.Read())
                        //                {

                        //                    var item = reportObjects.Find(x => x.reportName == reader["report_id"].ToString());

                        //                    if (item is not null)
                        //                    {
                        //                        sqlHelper.DeleteById("PBIX_to_Flat.Filters", reportObject.reportName);
                        //                        sqlHelper.DeleteById("PBIX_to_Flat.Visuals", reportObject.reportName);
                        //                        sqlHelper.DeleteById("PBIX_to_Flat.Local_Measures", reportObject.reportName);
                        //                        using (HttpResponseMessage response = client.GetAsync(reportObject.reportURL).Result)
                        //                        {
                        //                            response.EnsureSuccessStatusCode();
                        //                            reportObject.reportBody = await response.Content.ReadAsByteArrayAsync();

                        //                        }

                        //                    }
                        //                }
                        //            }
                        //            finally
                        //            {
                        //                reader.Close();
                        //            }
                        //        }
                        //    }
                        //}

                        foreach (var reportObject in reportObjects)
                        {
                            if (reportObject.change_Type == "edit")
                            {
                                sqlHelper.DeleteById("PBIX_to_Flat.Filters", reportObject.reportName);
                                sqlHelper.DeleteById("PBIX_to_Flat.Visuals", reportObject.reportName);
                                sqlHelper.DeleteById("PBIX_to_Flat.Local_Measures", reportObject.reportName);

                            }
                            if (reportObject.change_Type == "edit" || reportObject.change_Type == "add")
                            {
                                using (HttpResponseMessage response = client.GetAsync(reportObject.reportURL).Result)
                                {
                                    response.EnsureSuccessStatusCode();
                                    reportObject.reportBody = await response.Content.ReadAsByteArrayAsync();

                                }
                            }
                        }
                    }
                }
            }
            var parser = new ByteParser();
            foreach (var reportObject in reportObjects)
            {
                if (reportObject.reportBody is not null)
                {

                    var item = parser.GetSourceFilesFromZip(reportObject.reportBody);

                    var outputFilter = parser.ParseFileBytes(item, reportObject.reportName, reportObject.modified_date);
                    await Task.WhenAll(outputFilter.Filters.Select(x => filters.AddAsync(x)));
                    await Task.WhenAll(outputFilter.Visuals.Select(x => visuals.AddAsync(x)));
                    await Task.WhenAll(outputFilter.Measures.Select(x => localMeasures.AddAsync(x)));
                }

            }
            await filters.FlushAsync();
            await visuals.FlushAsync();
            await localMeasures.FlushAsync();
        }
    }
}
