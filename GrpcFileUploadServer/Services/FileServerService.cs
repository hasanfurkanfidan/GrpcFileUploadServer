using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using GrpcFileService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using static System.Net.Mime.MediaTypeNames;
using Google.Protobuf;

namespace GrpcFileService
{
    public class FileServerService:FileServer.FileServerBase
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public FileServerService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

       
        public override async Task FileUpload(IAsyncStreamReader<AllFile> requestStream, IServerStreamWriter<ResponseDataForUpload> responseStream, ServerCallContext context)
        {
            string path = Path.Combine(_webHostEnvironment.WebRootPath, "files");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            FileStream fileStream = null;
            decimal chunkSize = 0;
            int count = 0;
            try
            {
                while (await requestStream.MoveNext())
                {
                    if (count++ == 0)
                    {
                        fileStream = new FileStream($"{path}/{requestStream.Current.Info.FileName}{requestStream.Current.Info.FileExtention}", FileMode.CreateNew);
                        fileStream.SetLength(requestStream.Current.FileSize);

                    }
                    var buffer = requestStream.Current.Buffers.ToByteArray();
                    await fileStream.WriteAsync(buffer, 0, buffer.Length);
                    await responseStream.WriteAsync(new ResponseDataForUpload
                    {
                        Message = "Teşekkürler",
                        Percent = Convert.ToInt32(Math.Round((chunkSize += requestStream.Current.ReadedByte) * 100))
                    }) ;
                }
                await fileStream.DisposeAsync();
                fileStream.Close();

            }
            catch (Exception)
            {
                await responseStream.WriteAsync(new ResponseDataForUpload
                {
                    Message = "Hata",
                    Percent = requestStream.Current.ReadedByte * 100
                });

            }

        }
        public override async Task FileDownload(FileInfo request, IServerStreamWriter<AllFile> responseStream, ServerCallContext context)
        {
            string path = Path.Combine(_webHostEnvironment.ContentRootPath, "files");
            using FileStream fileStream = new FileStream($"{path}/{request.FileName}/{request.FileExtention}", FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[2048];
            AllFile content = new AllFile
            {
                FileSize = fileStream.Length,
                Info = new FileInfo
                {
                    FileName = Path.GetFileNameWithoutExtension(fileStream.Name),
                    FileExtention = Path.GetExtension(fileStream.Name),
                },
                ReadedByte = 0,
            };
            while((content.ReadedByte = await fileStream.ReadAsync(buffer, 0, buffer.Length))>0)
            {
                content.Buffers = ByteString.CopyFrom(buffer);
                await responseStream.WriteAsync(content);

            }
            fileStream.Close();
        }
    }
}
