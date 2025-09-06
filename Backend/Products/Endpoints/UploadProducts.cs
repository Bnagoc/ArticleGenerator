using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;

namespace ArticleGenerator.Products.Endpoints
{
    public class UploadProducts : IEndpoint
    {
        public const string LIST_SPEC_STRING = "Спецификация";
        public const string LIST_DATA_STRING = "Данные";
        public const string PRODUCT_NUMBER_CELL_STRING = "№";
        public const string PRODUCT_NAME_CELL_STRING = "Наименование товара";
        public const string PRODUCT_ARTICLE_CELL_STRING = "Артикул";
        public const string PRODUCT_BRAND_CELL_STRING = "Бренд";
        public const string PRODUCT_TARIFF_CODE_CELL_STRING = "ТН ВЭД";

        public static void Map(IEndpointRouteBuilder app) => app
            .MapPost("/upload", Handle)
            .DisableAntiforgery();

        private static async Task<IResult> Handle([FromForm] IFormFile file, AppDbContext db, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest("Файл не выбран или пустой.");

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Поддерживаются только .xlsx файлы.");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            try
            {
                using var workbook = new XLWorkbook(memoryStream);

                int headerRow = 0, productNumberColumn = 0, productNameColumn = 0, productModelColumn = 1,
                    productBrandColumn = 1, productTariffCodeColumn = 1, productArticleColumn = 0;

                // Ищем лист, в имени которого есть "спецификация" (без учёта регистра)
                var targetWorksheet = workbook.Worksheets
                    .FirstOrDefault(ws => ws.Name.Contains(LIST_SPEC_STRING, StringComparison.OrdinalIgnoreCase));

                // Ищем лист, в имени которого есть "данные" (без учёта регистра)
                var dataWorksheet = workbook.Worksheets
                    .FirstOrDefault(ws => ws.Name.Contains(LIST_DATA_STRING, StringComparison.OrdinalIgnoreCase));
                dataWorksheet.DataValidations.Worksheet.Columns(3, 13).Clear(XLClearOptions.AllFormats);

                if (targetWorksheet != null)
                {
                    List<Product> newProducts = new();

                    // Ищем адреса нужных столбцов: "Номер(№)", "наименование", "артикул", "Бренд", "ТН ВЭД"
                    for (int row = 1; ; row++)
                    {
                        foreach (var cell in targetWorksheet.Row(row).CellsUsed())
                        {
                            var value = cell.GetValue<string>()?.Trim();

                            if (value.Equals(PRODUCT_NUMBER_CELL_STRING, StringComparison.OrdinalIgnoreCase))
                            {
                                productNumberColumn = cell.Address.ColumnNumber;
                                continue;
                            }

                            if (value.Contains(PRODUCT_NAME_CELL_STRING, StringComparison.OrdinalIgnoreCase))
                            {
                                productNameColumn = cell.Address.ColumnNumber;
                                continue;
                            }

                            if (value.Contains(PRODUCT_ARTICLE_CELL_STRING, StringComparison.OrdinalIgnoreCase))
                            {
                                productModelColumn += cell.Address.ColumnNumber;
                                continue;
                            }

                            if (value.Contains(PRODUCT_BRAND_CELL_STRING, StringComparison.OrdinalIgnoreCase))
                            {
                                productBrandColumn += cell.Address.ColumnNumber;
                                continue;
                            }

                            if (value.Contains(PRODUCT_TARIFF_CODE_CELL_STRING, StringComparison.OrdinalIgnoreCase))
                            {
                                productTariffCodeColumn += cell.Address.ColumnNumber;
                                continue;
                            }

                            if (productNameColumn != 0 && productModelColumn != 1 && productTariffCodeColumn != 1 && productBrandColumn != 1)
                            {
                                headerRow = row;
                                break;
                            }
                        }

                        if (headerRow != 0) break;
                    }

                    // Добавляем новый столбец "Артикул 1С"
                    productArticleColumn = productNameColumn + 1;
                    targetWorksheet.Column(productNameColumn).InsertColumnsAfter(1);
                    targetWorksheet.Cell(headerRow, productArticleColumn).Value = "Артикул 1С";


                    // Генерируем артикул, сохраняем продукт в базу, добавляем артикул в таблицу
                    for (int row = headerRow + 1; ; row++)
                    {
                        var isNumberValid = int.TryParse(targetWorksheet.Cell(row, productNumberColumn).GetValue<string>(), out int number);
                        var isNameDigit = int.TryParse(targetWorksheet.Cell(row, productNameColumn).GetValue<string>(), out int digitName);

                        if (!isNumberValid)
                        {
                            break;
                        }

                        if (!isNameDigit)
                        {
                            // Забираем данные из нужных ячеек
                            var name = targetWorksheet.Cell(row, productNameColumn).GetValue<string>()?.Trim();
                            var model = targetWorksheet.Cell(row, productModelColumn).GetValue<string>()?.Trim();
                            var brand = targetWorksheet.Cell(row, productBrandColumn).GetValue<string>()?.Trim();
                            var tariffCode = targetWorksheet.Cell(row, productTariffCodeColumn).GetValue<string>()?.Trim();

                            // Создаём продукт
                            var product = new Product
                            {
                                Name = name ?? default,
                                Model = model ?? default,
                                Brand = brand ?? default,
                                TariffCode = tariffCode ?? default,
                                // Генерируем артикул на основе наименования, кода ТН ВЭД и модели товара
                                Article = GenerateArticle(name, tariffCode, model)
                            };

                            // Вставляем артикул
                            targetWorksheet.Cell(row, productArticleColumn).Value = product.Article;
                            newProducts.Add(product);
                        }
                    }

                    // Выбираем все сгенерированные артикулы
                    var newArticles = newProducts.Select(p => p.Article).ToList();

                    // Находим артикулы, которые УЖЕ есть в базе
                    var existingArticles = await db.Products
                        .Where(p => newArticles.Contains(p.Article))
                        .Select(p => p.Article)
                        .ToHashSetAsync(cancellationToken);

                    // Оставляем товары только с новыми артикулами
                    var productsToInsert = newProducts
                        .Where(p => !existingArticles.Contains(p.Article))
                        .DistinctBy(a => a.Article)
                        .ToList();

                    // Сохраняем только новые
                    if (productsToInsert.Count > 0)
                    {
                        await db.Products.AddRangeAsync(productsToInsert, cancellationToken);
                        await db.SaveChangesAsync(cancellationToken);
                    }

                }
                else
                {
                    return Results.Problem($"Ошибка: лист с названием \"Спецификация\" не найден");
                }

                // Сохраняем Excel в память
                using var outputMemoryStream = new MemoryStream();
                workbook.SaveAs(outputMemoryStream);

                // Возвращаем изменённый файл
                var fileName = Path.GetFileNameWithoutExtension(file.FileName) + "_обработанный.xlsx";
                return Results.File(
                    outputMemoryStream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName
                );
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка: {ex.Message}");
            }
        }

        private static string GenerateArticle(string? name, string? tariffCode, string? model)
        {
            name = (name ?? "NO_NAME").Trim();
            model = (model ?? "NO_MODEL").Trim();
            tariffCode = (tariffCode ?? "NO_CODE").Trim();

            // Формируем уникальную строку
            var input = $"{name}|{model}|{tariffCode}";

            // Генерируем SHA256-хеш
            byte[] hashBytes;
            using (var sha256 = SHA256.Create())
            {
                hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            }

            // Преобразуем первые 8 байт хеша в UInt64
            ulong hashAsNumber = 0;
            for (int i = 0; i < 8; i++)
            {
                hashAsNumber = (hashAsNumber << 8) | hashBytes[i];
            }

            // Берём остаток от деления на 10^10
            ulong max10Digits = 10_000_000_000;
            ulong articleNumber = hashAsNumber % max10Digits;

            // Форматируем как строку из 10 цифр с ведущими нулями
            return articleNumber.ToString("D10");
        }
    }
}
