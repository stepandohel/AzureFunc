using AddToDbFunction.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PBIX_to_Flat.OutputModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(
                        System.Text.ASCIIEncoding.ASCII.GetBytes(
                            string.Format("{0}:{1}", "", personalAccessToken))));

                using (HttpResponseMessage response = client.GetAsync(

                Environment.GetEnvironmentVariable("FileMetaDataURL")).Result)
                {
                    response.EnsureSuccessStatusCode();

                    var folderMetaData = await response.Content.ReadAsStringAsync();
                    var folderMetaDataJson = JObject.Parse(folderMetaData);
                    var metaDataValue = folderMetaDataJson["value"];

                    var item = metaDataValue[1];
                    while (item is not null)
                    {
                        var stringBuilder = new StringBuilder(item["path"].ToString());
                        reportObjects.Add(new ReportObject()
                        {
                            reportURL = item["url"].ToString() + "&includeContent=true",
                            reportName = item["path"].ToString()
                            .Remove(item["path"].ToString().Length - 5)
                            .Replace(metaDataValue[0]["path"].ToString() + "/", ""),
                            modified_date = Convert.ToDateTime(item["latestProcessedChange"]["committer"]["date"]),
                            is_Changed = false,
                            is_New = true

                        });
                        item = item.Next;
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
                        foreach (var reportObject in reportObjects)
                        {
                            using (SqlDataReader reader = sqlHelper.SelectGroupById(reportObject.reportName))
                            {
                                try
                                {
                                    while (reader.Read())
                                    {

                                        var item = reportObjects.Find(x => x.reportName == reader["report_id"].ToString());

                                        if (item is not null)
                                        {
                                            item.is_New = false;
                                        }
                                        if (item is not null && item.modified_date > Convert.ToDateTime(reader["Modified_Date"]))
                                        {
                                            item.is_Changed = true;
                                        }
                                    }
                                }
                                finally
                                {
                                    reader.Close();
                                }
                            }
                        }

                        foreach (var reportObject in reportObjects)
                        {
                            if (reportObject.is_Changed && !reportObject.is_New)
                            {
                                sqlHelper.DeleteById("PBIX_to_Flat.Filters", reportObject.reportName);
                                sqlHelper.DeleteById("PBIX_to_Flat.Visuals", reportObject.reportName);
                                sqlHelper.DeleteById("PBIX_to_Flat.Local_Measures", reportObject.reportName);

                            }
                            if (reportObject.is_New || reportObject.is_Changed)
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

                    var outputFilter = parser.ParseFileBytes(item, reportObject.reportName,reportObject.modified_date);
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
