using ClosedXML.Excel;

namespace ArticleGenerator.Products.Endpoints
{
    public class DownloadProducts : IEndpoint
    {
        public static void Map(IEndpointRouteBuilder app) => app
            .MapGet("/download", Handle)
            .DisableAntiforgery();

        private static async Task<IResult> Handle(AppDbContext db, CancellationToken cancellationToken)
        {
            var products = await db.Products
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Model,
                    p.Brand,
                    p.TariffCode,
                    p.Article,
                    p.UploadedAt
                })
                .ToListAsync(cancellationToken);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Товары");

            // Заголовки
            var currentRow = 1;
            ws.Cell(currentRow, 1).Value = "№";
            ws.Cell(currentRow, 2).Value = "Наименование";
            ws.Cell(currentRow, 3).Value = "Модель";
            ws.Cell(currentRow, 4).Value = "Бренд";
            ws.Cell(currentRow, 5).Value = "Код ТН ВЭД";
            ws.Cell(currentRow, 6).Value = "Артикул";
            ws.Cell(currentRow, 7).Value = "Дата создания";

            // Стиль заголовка
            var headerRow = ws.Row(currentRow);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.SetBackgroundColor(XLColor.FromTheme(XLThemeColor.Accent1, 0.8));
            headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Автоподбор ширины
            ws.Column(1).Width = 5;   // №
            ws.Column(2).Width = 30;  // Наименование
            ws.Column(3).Width = 20;  // Модель
            ws.Column(4).Width = 15;  // Бренд
            ws.Column(5).Width = 15;  // Код ТН ВЭД
            ws.Column(6).Width = 15;  // Артикул
            ws.Column(7).Width = 18;  // Дата

            // Заполняем данными
            foreach (var p in products)
            {
                currentRow++;
                ws.Cell(currentRow, 1).Value = p.Id;
                ws.Cell(currentRow, 2).Value = p.Name;
                ws.Cell(currentRow, 3).Value = p.Model ?? "";
                ws.Cell(currentRow, 4).Value = p.Brand ?? "";
                ws.Cell(currentRow, 5).Value = p.TariffCode ?? "";
                ws.Cell(currentRow, 6).Value = p.Article;
                ws.Cell(currentRow, 7).Value = p.UploadedAt;
            }

            // Границы таблицы
            var range = ws.Range(1, 1, currentRow, 7);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Сохраняем в память
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileBytes = stream.ToArray();

            // Имя файла с датой
            var fileName = $"Товары_{DateTime.Now:ddMMyyyy_HHmm}.xlsx";

            return Results.File(
                fileBytes,
                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileDownloadName: fileName
            );
        }
    }

}
