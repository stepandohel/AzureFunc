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
            // Таймер на каждую минуту для тестов, тут если что менять https://crontab.cronhub.io/
            //[TimerTrigger("* * * * *")] TimerInfo myTimer,
            //ILogger log,
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log,
            //тут к беру таблицы из бд
            [Sql(commandText: "PBIX_to_Flat.Visuals", connectionStringSetting: "MyDb")] IAsyncCollector<Visual> visuals,
            [Sql(commandText: "PBIX_to_Flat.Filters", connectionStringSetting: "MyDb")] IAsyncCollector<Filter> filters,
            [Sql(commandText: "PBIX_to_Flat.Local_Measures", connectionStringSetting: "MyDb")] IAsyncCollector<LocalMeasure> localMeasures)

        {
            // твой 30 дневный токен на файлы из azure dev ops из local.setting
            var personalAccessToken = Environment.GetEnvironmentVariable("PersonalAccessToken");

            byte[] responseBody;
            string testURL = "";
            var reportObjects = new List<ReportObject>();

            //получаешь файл из репозитория
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(
                        System.Text.ASCIIEncoding.ASCII.GetBytes(
                            string.Format("{0}:{1}", "", personalAccessToken))));



                using (HttpResponseMessage response = client.GetAsync(
                //url файла из local.setting
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
                    // Open the connection
                    connection.Open();


                    foreach (var reportObject in reportObjects)
                    {
                        //Check tables
                        using (SqlCommand command = new SqlCommand($"select report_id, modified_date from PBIX_to_Flat.Visuals WHERE report_id = @reportId group by report_id, modified_date", connection))
                        {
                            command.Parameters.AddWithValue("@reportId", reportObject.reportName);
                            // Execute the command
                            SqlDataReader reader = command.ExecuteReader();
                            try
                            {
                                while (reader.Read())
                                {

                                    var item = reportObjects.Find(x => x.reportName == reader["report_id"].ToString());

                                    if (item is not null)
                                    {
                                        item.is_New = false;
                                    }
                                    if (item is not null && item.modified_date >= Convert.ToDateTime(reader["Modified_Date"]))
                                    {
                                        item.is_Changed = true;
                                    }
                                }
                            }
                            finally
                            {
                                // Always call Close when done reading.
                                reader.Close();
                            }
                        }
                    }

                    foreach (var reportObject in reportObjects)
                    {
                        if (reportObject.is_Changed && !reportObject.is_New)
                        {
                            // Create a SQL command to delete all records in the table
                            using (SqlCommand command = new SqlCommand($"DELETE FROM PBIX_to_Flat.Filters WHERE report_id = @reportId", connection))
                            {
                                command.Parameters.AddWithValue("reportId", reportObject.reportName);
                                // Execute the command
                                command.ExecuteNonQuery();
                            }
                            using (SqlCommand command = new SqlCommand($"DELETE FROM PBIX_to_Flat.Visuals WHERE report_id = @reportId", connection))
                            {
                                command.Parameters.AddWithValue("@reportId", reportObject.reportName);
                                // Execute the command
                                command.ExecuteNonQuery();
                            }
                            using (SqlCommand command = new SqlCommand($"DELETE FROM PBIX_to_Flat.Local_Measures WHERE report_id = @reportId", connection))
                            {
                                command.Parameters.AddWithValue("@reportId", reportObject.reportName);
                                // Execute the command
                                command.ExecuteNonQuery();
                            }
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
            //Сервис для работы с архивом 
            var service = new MyService(DateTime.Now);
            foreach (var reportObject in reportObjects)
            {
                if (reportObject.reportBody is not null)
                {

                    //Распаковываешь архив с файлом и берешь файлы
                    var item = service.GetSourceFilesFromZip(reportObject.reportBody);

                    var outputFilter = service.ParseFileBytes(item, reportObject.reportName);
                    await Task.WhenAll(outputFilter.Filters.Select(x => filters.AddAsync(x)));
                    await Task.WhenAll(outputFilter.Visuals.Select(x => visuals.AddAsync(x)));
                    await Task.WhenAll(outputFilter.Measures.Select(x => localMeasures.AddAsync(x)));
                }

            }
            ////Сохраняю в бд по табицам
            await filters.FlushAsync();
            await visuals.FlushAsync();
            await localMeasures.FlushAsync();
        }
    }
}
