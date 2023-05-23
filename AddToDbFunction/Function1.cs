using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PBIX_to_Flat;
using PBIX_to_Flat.OutputModels;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace AddToDbFunction
{
    public class AddToDbFunction
    {
        [FunctionName("AddToDbFunction")]
        public async Task RunAsync(
            // Таймер на каждую минуту для тестов, тут если что менять https://crontab.cronhub.io/
            [TimerTrigger("* * * * *")] TimerInfo myTimer,
            ILogger log,
            //тут к беру таблицы из бд
            [Sql(commandText: "PBIX_to_Flat.Visuals", connectionStringSetting: "MyDb")] IAsyncCollector<Visual> visuals,
            [Sql(commandText: "PBIX_to_Flat.Filters", connectionStringSetting: "MyDb")] IAsyncCollector<Filter> filters,
            [Sql(commandText: "PBIX_to_Flat.Local_Measures", connectionStringSetting: "MyDb")] IAsyncCollector<LocalMeasure> localMeasures)

        {
            // твой 30 дневный токен на файлы из azure dev ops из local.setting
            var personalAccessToken = Environment.GetEnvironmentVariable("PersonalAccessToken");

            byte[] responseBody;
            OutputObject outputFilter = null;

            //получаешь файл из репозитория
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/zip"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(
                        System.Text.ASCIIEncoding.ASCII.GetBytes(
                            string.Format("{0}:{1}", "", personalAccessToken))));

                using (HttpResponseMessage response = client.GetAsync(
                //url файла из local.setting
                Environment.GetEnvironmentVariable("FileURL")).Result)
                {
                    response.EnsureSuccessStatusCode();
                    responseBody = await response.Content.ReadAsByteArrayAsync();
                }
            }

            //Сервис для работы с файлом 
            var service = new MyService(responseBody);

            //Распаковываешь архив с файлом и берешь биты
            byte[] fileBytes = service.GetSourceFileFromZip();

            //Парсишь биты
            outputFilter = service.ParseFileBytes(fileBytes);

            //Сохраняю в бд по табицам
            await Task.WhenAll(outputFilter.Filters.Select(x => filters.AddAsync(x)));
            await Task.WhenAll(outputFilter.Visuals.Select(x => visuals.AddAsync(x)));
            await Task.WhenAll(outputFilter.Measures.Select(x => localMeasures.AddAsync(x)));

            await filters.FlushAsync();
            await visuals.FlushAsync();
            await localMeasures.FlushAsync();
        }
    }
}
