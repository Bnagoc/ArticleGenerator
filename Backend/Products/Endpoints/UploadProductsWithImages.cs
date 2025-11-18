using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace ArticleGenerator.Products.Endpoints
{
    public class UploadProductsWithImages : IEndpoint
    {
        public const string API_KEY = "sk-870e662c8e104444a17e2c9104db88db";
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(15, 15);

        public static void Map(IEndpointRouteBuilder app) => app
            .MapPost("/upload-with-images", Handle)
            .DisableAntiforgery();

        private static async Task<IResult> Handle([FromForm] IFormFile file, AppDbContext db, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest("Файл не выбран или пустой.");

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Поддерживаются только .xlsx файлы.");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            try
            {
                var workbook = new XSSFWorkbook(memoryStream);
                var sheet = workbook.GetSheetAt(0); // Первый лист

                // Получаем контейнер с рисунками
                var drawing = (XSSFDrawing)sheet.CreateDrawingPatriarch();
                var pictures = drawing.GetShapes();

                var imageTasks = new List<Task<(int RowIndex, string TnvedCode)>>();

                foreach (var shape in pictures)
                {
                    if (shape is XSSFPicture picture)
                    {
                        var anchor = picture.ClientAnchor;
                        int rowIndex = anchor.Row1;

                        int productNumber = rowIndex + 1;

                        var pictureData = picture.PictureData;
                        string ext = GetImageExtension(pictureData.PictureType);


                        // Сохраняем для вставки в Excel
                        imageTasks.Add(Task.Run(async () => 
                        {
                            string tnvedCode = await AnalyzeImageWithQwen(pictureData.Data, ext);
                            Console.WriteLine($"Фото {productNumber}: предложенный код ТН ВЭД — {tnvedCode}");
                            return (rowIndex, tnvedCode);
                        }));

                    }
                }

                var imageInfoList = await Task.WhenAll(imageTasks);

                // Вставляем столбец между A и B → становится новым столбцом B
                int lastColumnIndex = sheet.GetRow(0)?.LastCellNum ?? 0; // Индекс последнего столбца в шапке
                int newColumnIndex = lastColumnIndex; // Новый столбец будет следующим по порядку

                // Устанавливаем заголовок в ячейке B1 (строка 0, столбец 1)
                var headerRow = sheet.GetRow(0) ?? sheet.CreateRow(0);
                var cell = headerRow.GetCell(newColumnIndex) ?? headerRow.CreateCell(newColumnIndex);
                cell.SetCellValue("Код ТН ВЭД");

                // Заполняем значениями по номерам строк
                foreach (var (RowIndex, ProductNumber) in imageInfoList)
                {
                    // Вставляем значение строку RowIndex
                    var row = sheet.GetRow(RowIndex) ?? sheet.CreateRow(RowIndex);
                    var cellInRow = row.GetCell(newColumnIndex) ?? row.CreateCell(newColumnIndex);
                    if (cellInRow.StringCellValue.Contains(ProductNumber))
                    {
                        continue;
                    }
                    if (cellInRow.StringCellValue.IsNullOrEmpty())
                    {
                        cellInRow.SetCellValue(ProductNumber);
                    }
                    else
                    {
                        cellInRow.SetCellValue($"{cellInRow.StringCellValue}, {ProductNumber}");
                    }
                    
                }

                // === ШАГ 3: Сохраняем изменённый файл и возвращаем его ===
                using var outputStream = new MemoryStream();
                workbook.Write(outputStream);

                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
                var resultFileName = $"{fileNameWithoutExt}_с_кодами.xlsx";

                return Results.File(
                    outputStream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    resultFileName
                );

            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка при обработке Excel: {ex.Message}");
            }
            
        }

        // Вспомогательная функция для определения расширения
        private static string GetImageExtension(PictureType type)
        {
            return type switch
            {
                PictureType.JPEG => ".jpg",
                PictureType.PNG => ".png",
                PictureType.GIF => ".gif",
                PictureType.BMP => ".bmp",
                PictureType.WMF => ".wmf",
                _ => ".png"
            };
        }

        private static async Task<string> AnalyzeImageWithQwen(byte[] imageBytes, string ext)
        {
            await semaphore.WaitAsync();
            try
            {
                using var httpClient = new HttpClient();

                // Устанавливаем заголовки
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");

                var jsonContent = new
                {
                    model = "qwen-vl-plus",
                    messages = new[]
                    {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:image/{(ext == ".jpg" ? "jpeg" : ext.TrimStart('.'))};base64,{Convert.ToBase64String(imageBytes)}"
                            }
                        },
                        new
                        {
                            type = "text",
                            text = "Return only the TNVED code from the image. No explanation. Digits only."
                        }
                    }
                }
            },
                    temperature = 0.1,
                    top_p = 0.9
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(jsonContent),
                    Encoding.UTF8,
                    "application/json");

                try
                {
                    var response = await httpClient.PostAsync("https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions  ", content);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Ошибка API: {responseBody}");
                        return "ошибка";
                    }

                    // Парсим JSON и извлекаем ответ
                    var jsonResponse = JObject.Parse(responseBody);
                    return jsonResponse["choices"][0]["message"]["content"].ToString().Trim();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Исключение при вызове Qwen-VL: {ex.Message}");
                    return "ошибка";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при вызове Qwen-VL: {ex.Message}");
                return "ошибка";
            }
            finally
            {
                semaphore.Release();
            }
        }

    }
}
