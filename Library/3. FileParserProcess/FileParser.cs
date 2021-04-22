﻿
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS_ProgramingChallengeLibrary.Helpers;
using DS_ProgramingChallengeLibrary.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DS_ProgramingChallengeLibrary
{
    public class FileParser : IFileParser
    {
        private readonly ILogger _log;
        private readonly IConfiguration _config;

        public FileParser(ILogger<FileParser> log, IConfiguration config)
        {
            _log = log;
            _config = config;
        }

        public Task<GroupByOutputModel> TransformData(string fileNamePath)
        {
            _log.LogInformation("Transforming data: {fileNamePath}", fileNamePath);
            //const int chunkSize = 2 * 1024; // 2KB
            int chunkSize = 10000000; // -> 10MB  //10000; // -> 10KB //1000000; // => 1MB
            byte[] buffer = new byte[chunkSize];
            char separator = ' ';
            List<ContainedDataModel> containedData = new List<ContainedDataModel>();
            List<ContainedDataModel> preResultData = new List<ContainedDataModel>();
            GroupByOutputModel result = new GroupByOutputModel();

            lock (this)
            {
                using (FileStream fileStream = new FileStream(fileNamePath, FileMode.Open, FileAccess.Read))
                {
                    int reading = 1;
                    while (reading > 0)
                    {
                        reading = fileStream.Read(buffer, 0, chunkSize);
                        string partOfFile = Encoding.UTF8.GetString(buffer, 0, reading);
                        using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(partOfFile)))
                        {
                            using (StreamReader streamReader = new StreamReader(memoryStream, Encoding.UTF8, true))
                            {
                                while (!streamReader.EndOfStream)
                                {
                                    var line = streamReader.ReadLine();
                                    var columns = line.Split(separator);

                                    string domain_code = columns[0];
                                    string page_title = columns[1];
                                    int count_views = int.Parse(columns[2]);

                                    containedData.Add(new ContainedDataModel()
                                    {
                                        domain_code = domain_code,
                                        page_title = page_title,
                                        count_views = count_views
                                    });
                                }
                            }
                        }

                        preResultData.AddRange(GroupByCountData(containedData));

                    }
                }

                result.containedDataModel = GroupByCountData(preResultData);
                _log.LogInformation("Transforming data finished.");
            }
            return Task.Run(() =>
            {
                return result;
            });
        }

        public void TransformDataIntoDataTable(out DataTable resultDataTable)
        {
            _log.LogInformation("Transforming Data");
            string fileDownloadPath = GeneralHelper.GetDownloadedFilesPath(_config);
            string resultFilePath = GeneralHelper.GetResultFilePath(_config);
            string resultFileNamePath = FileParserHelper.CombineMultipleTextFiles(fileDownloadPath, resultFilePath, "output.txt", true);
            resultDataTable = FileParserHelper.ConvertToDataTable(resultFileNamePath, 4, ' ');
            _log.LogInformation("Transformed.");
        }

        private List<ContainedDataModel> GroupByCountData(List<ContainedDataModel> containedData)
        {
            return containedData
                .GroupBy(c => new { c.domain_code, c.page_title })
                .Select(gb => new ContainedDataModel()
                {
                    domain_code = gb.Key.domain_code,
                    page_title = gb.Key.page_title,
                    count_views = gb.Count()
                }).ToList();
        }
        private List<ContainedDataModel> GroupBySumData(List<ContainedDataModel> containedData)
        {
            return containedData
               .GroupBy(c => new { c.domain_code, c.page_title })
               .Select(gb => new ContainedDataModel()
               {
                   domain_code = gb.Key.domain_code,
                   page_title = gb.Key.page_title,
                   count_views = gb.Sum(x => x.count_views)
               }).ToList();
        }
    }
}
