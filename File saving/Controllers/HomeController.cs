using File_saving.Migrations;
using File_saving.Models;
using File_saving.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Encryption;
using System.Diagnostics;
using System;
using System.IO;
using System.Security.Cryptography;

namespace File_saving.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _logger = logger;
            _webHostEnvironment = environment;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            ViewBag.message = TempData["message"];
            return View();
        }

        // Saves the file on the database
        public async Task<IActionResult> Upload1(UploadModel upload)
        {
            using (var db = new FileSavingDbContext())
            {
                using(var ms = new System.IO.MemoryStream())
                {
                    var file = new File_saving.Models.File();
                    await upload.MyFile.CopyToAsync(ms);
                    file.FileDb = ms.ToArray();
                    db.Add(file);

                }
            }

            TempData["message"] = "File upload"; 
            return RedirectToAction("Index");
        }

        // Reads the file from the database
        public async Task<IActionResult> Donwload1(int id)
        {
            using (var context = new FileSavingDbContext())
            {
                var file = await context.Files.FindAsync(id);
                return File(file.FileDb, "image/png", fileDownloadName: "File.png");
            }
        }

        // Saves the file on local storage
        public async Task<IActionResult> Upload2(UploadModel upload)
        {
            var fileName = System.IO.Path.Combine(_webHostEnvironment.ContentRootPath, "Uploads", upload.MyFile.FileName);
            await upload.MyFile.CopyToAsync(
                new System.IO.FileStream(fileName, System.IO.FileMode.Create));

            using (var context = new FileSavingDbContext())
            {
                var file = new Models.File();
                file.PathFile = upload.MyFile.FileName;
                context.Files.Add(file);
                context.SaveChanges();
            }

            TempData["message"] = "File upload";
            return RedirectToAction("Index");
        }

        // Reads the file from local storage
        public async Task<IActionResult> Download2(int id)
        {
            using (var context = new FileSavingDbContext())
            {
                var file = await context.Files.FindAsync(id);
                var fullFileName = System.IO.Path.Combine(_webHostEnvironment.ContentRootPath, "Uploads", file.PathFile);

                using (var fs = new System.IO.FileStream(fullFileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    using (var ms = new System.IO.MemoryStream())
                    {
                        await fs.CopyToAsync(ms);
                        return File(ms.ToArray(), "image/png", fileDownloadName: "File.png");
                    }
                }
            }
        }

        // Saves the file on S3 server
        public async Task<IActionResult> Upload3(UploadModel upload)
        {
            string server = _configuration["AWS:SecretKey"];
            string accesKey = _configuration["AWS:AccessKey"];
            string secretKey = _configuration["AWS:SecretKey"];
            string bucket = _configuration["AWS:Bucket"];

            var fileName = System.IO.Path.Combine(_webHostEnvironment.ContentRootPath, "Uploads", upload.MyFile.FileName);

            using (var fs = new System.IO.FileStream(fileName, System.IO.FileMode.Create))
            {
                await upload.MyFile.CopyToAsync(fs);   
            }

            var minioClient = new MinioClient()
                                    .WithEndpoint(server)
                                    .WithCredentials(accesKey, secretKey)
                                    .WithSSL()
                                    .Build();

            byte[] bs = await System.IO.File.ReadAllBytesAsync(fileName);
            var ms = new System.IO.MemoryStream(bs);

            Aes aesEncryption = Aes.Create();
            aesEncryption.KeySize = 256;
            aesEncryption.GenerateKey();
            var ssec = new SSEC(aesEncryption.Key);
            var progress = new Progress<ProgressReport>(progressReport =>
            {
                // Progress events are delivered asynchronously (see remark below)
                Console.WriteLine(
                        $"Percentage: {progressReport.Percentage}% TotalBytesTransferred: {progressReport.TotalBytesTransferred} bytes");
                if (progressReport.Percentage != 100)
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                else Console.WriteLine();
            });
            PutObjectArgs args = new PutObjectArgs()
                                                .WithBucket(bucket)
                                                .WithObject(upload.MyFile.ToString())
                                                .WithFileName(fileName)
                                                .WithContentType("application/octet-stream")
                                                .WithServerSideEncryption(ssec)
                                                .WithProgress(progress);
            await minioClient.PutObjectAsync(args);

            System.IO.File.Delete(fileName);

            using (var context = new FileSavingDbContext())
            {
                var file = new Models.File();
                file.PathFile = upload.MyFile.FileName;
                context.Files.Add(file);
                context.SaveChanges();
            }

            TempData["message"] = "File upload";
            return RedirectToAction("Index");
        }

        // Reads the file from S3 server
        public async Task<IActionResult> Download3(int id)
        {
            string server = _configuration["AWS:SecretKey"];
            string accesKey = _configuration["AWS:AccessKey"];
            string secretKey = _configuration["AWS:SecretKey"];
            string bucket = _configuration["AWS:Bucket"];

            using (var context = new FileSavingDbContext())
            {
                var file = await context.Files.FindAsync(id);
                var minioClient = new MinioClient()
                        .WithEndpoint(server)
                        .WithCredentials(accesKey, secretKey)
                        .WithSSL()
                        .Build();

                using (var ms = new System.IO.MemoryStream())
                {
                    GetObjectArgs getObjectArgs = new GetObjectArgs()
                                  .WithBucket(bucket)
                                  .WithObject(file.PathFile)
                                  .WithCallbackStream((stream) =>
                                  {
                                      stream.CopyTo(ms);
                                  });
                    await minioClient.GetObjectAsync(getObjectArgs);
                    return File(ms.ToArray(), "image/png", fileDownloadName: "File.png");
                }
            
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
