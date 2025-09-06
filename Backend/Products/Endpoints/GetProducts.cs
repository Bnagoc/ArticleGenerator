namespace ArticleGenerator.Products.Endpoints
{
    public class GetProducts : IEndpoint
    {
        public static void Map(IEndpointRouteBuilder app) => app
            .MapGet("/", Handle);

        public record Request(int page = 1);

        public static async Task<IResult> Handle([AsParameters] Request request, AppDbContext database, CancellationToken cancellationToken)
        {
            const int pageSize = 500;
            int page = request.page < 1 ? 1 : request.page;

            // Общее количество товаров
            var totalCount = await database.Products.CountAsync(cancellationToken);

            // Вычисляем общее количество страниц
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Ограничиваем page, чтобы не выйти за пределы
            if (page > totalPages && totalPages > 0)
                page = totalPages;

            var products = await database.Products
               .AsNoTracking()
               .OrderByDescending(p => p.UploadedAt)
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
               .Skip((page - 1) * pageSize)
               .Take(pageSize)
               .ToListAsync(cancellationToken);

            // Формируем таблицу
            var tableRows = string.Join("", products.Select(p => $@"
                <tr>
                    <td>{p.Id}</td>
                    <td>{HtmlEncode(p.Name)}</td>
                    <td>{HtmlEncode(p.Model)}</td>
                    <td>{HtmlEncode(p.Brand)}</td>
                    <td>{HtmlEncode(p.TariffCode)}</td>
                    <td><strong>{HtmlEncode(p.Article)}</strong></td>
                    <td>{p.UploadedAt:dd.MM.yyyy HH:mm}</td>
                </tr>"));

            var tableHtml = products.Any() ? tableRows : "<tr><td colspan='7' style='color: #999;'>Товары не найдены</td></tr>";

            // Генерация ссылок пагинации
            string prevLink = page > 1 ? $"<a href='/products?page={page - 1}' style='{ButtonStyle}'>« Предыдущая</a>" : "<span style='color: #aaa;'>« Предыдущая</span>";

            string nextLink = page < totalPages ? $"<a href='/products?page={page + 1}' style='{ButtonStyle}'>Следующая »</a>" : "<span style='color: #aaa;'>Следующая »</span>";

            string pageInfo = $"Страница {page} из {totalPages} &nbsp; ({products.Count}/{totalCount} товаров)";

            return Results.Content($@"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>📋 Все товары — Страница {page}</title>
                    <meta charset='utf-8' />
                    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
                    <style>
                        body, html {{
                            margin: 0;
                            padding: 0;
                            height: 100%;
                            width: 100%;
                            font-family: Arial, sans-serif;
                            background: #f7f7f7;
                        }}
                        .flex-container {{
                            display: flex;
                            justify-content: center;
                            align-items: flex-start;
                            min-height: 100vh;
                            padding: 40px 20px;
                            box-sizing: border-box;
                        }}
                        .container {{
                            max-width: 1400px;
                            width: 100%;
                            background: white;
                            padding: 40px;
                            border-radius: 12px;
                            box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
                        }}
                        h1 {{
                            color: #2c3e50;
                            text-align: center;
                            margin-top: 0;
                        }}
                        .pagination {{
                            display: flex;
                            justify-content: space-between;
                            margin: 20px 0;
                            align-items: center;
                            font-size: 14px;
                        }}
                        .pagination .info {{
                            flex-grow: 1;
                            text-align: center;
                        }}
                        table {{
                            width: 100%;
                            border-collapse: collapse;
                            margin: 20px 0;
                            table-layout: fixed;
                        }}
                        th, td {{
                            padding: 12px 10px;
                            text-align: left;
                            border-bottom: 1px solid #ddd;
                            overflow: hidden;
                            text-overflow: ellipsis;
                        }}
                        td {{cursor: default;
                            title: attr(data-title);
                        }}
                        th:nth-child(1), td:nth-child(1) {{width: 4%;  }} /* № */
                        th:nth-child(2), td:nth-child(2) {{width: 44%; }} /* Наименование */
                        th:nth-child(3), td:nth-child(3) {{width: 14%; }} /* Модель */
                        th:nth-child(4), td:nth-child(4) {{width: 8%; }} /* Бренд */
                        th:nth-child(5), td:nth-child(5) {{width: 8%; }} /* Код ТН ВЭД */
                        th:nth-child(6), td:nth-child(6) {{width: 8%; }} /* Артикул */
                        th:nth-child(7), td:nth-child(7) {{width: 14%; }} /* Дата создания */
                        th {{
                            background: #f0f0f0;
                            color: #333;
                            font-weight: bold;
                        }}
                        tr:hover {{
                            background: #f9f9f9;
                        }}
                        .back-button {{
                            background: #95a5a6;
                            color: white;
                            padding: 10px 20px;
                            border: none;
                            border-radius: 6px;
                            cursor: pointer;
                            font-size: 16px;
                            text-decoration: none;
                            display: inline-block;
                            margin-bottom: 20px;
                        }}
                        .back-button:hover {{
                            background: #7f8c8d;
                        }}
                        .download-button {{
                            background: #27ae60;
                            color: white;
                            padding: 10px 20px;
                            margin-bottom: 20px;
                            margin-left: 10px;
                            border: none;
                            border-radius: 6px;
                            cursor: pointer;
                            font-size: 16px;
                            text-decoration: none;
                        }}
                        .download-button:hover {{
                            background: #219653;
                        }}
                        .empty {{
                            text-align: center;
                            color: #999;
                            font-style: italic;
                            padding: 20px;
                        }}
                    </style>
                </head>
                <body>
                    <div class='flex-container'>
                        <div class='container'>
                            <h1>📋 Все товары</h1>
                            <a href='/' class='back-button'>← Назад</a>
                            <a href='/products/download' class='download-button'>⬇️ Скачать все товары</a>

                            <div style=""margin: 20px 0; text-align: center;"">
                                <input 
                                    type=""text"" 
                                    id=""searchInput"" 
                                    placeholder=""Поиск..."" 
                                    style=""
                                        padding: 10px 16px;
                                        width: 80%;
                                        max-width: 500px;
                                        border: 1px solid #ddd;
                                        border-radius: 6px;
                                        font-size: 16px;
                                        box-shadow: 0 2px 5px rgba(0,0,0,0.05);
                                    ""
                                />
                            </div>

                            <div class='pagination'>
                                {prevLink}
                                <div class='info'>{pageInfo}</div>
                                {nextLink}
                            </div>

                            <table>
                                <thead>
                                    <tr>
                                        <th>ID</th>
                                        <th>Наименование</th>
                                        <th>Модель</th>
                                        <th>Бренд</th>
                                        <th>Код ТН ВЭД</th>
                                        <th>Артикул</th>
                                        <th>Дата создания</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {tableHtml}
                                </tbody>
                            </table>

                            <div class='pagination'>
                                {prevLink}
                                <div class='info'>{pageInfo}</div>
                                {nextLink}
                            </div>
                        </div>
                    </div>

               <script>
                    document.addEventListener('DOMContentLoaded', function () {{
                        const searchInput = document.getElementById('searchInput');
                        const tableRows = Array.from(document.querySelectorAll('tbody tr')); // Преобразуем в массив
                        let noResultsRow = document.getElementById('no-results');

                        // Если строки ""Ничего не найдено"" ещё нет — создаём
                        if (!noResultsRow) {{
                            noResultsRow = document.createElement('tr');
                            noResultsRow.id = 'no-results';
                            noResultsRow.innerHTML = `
                                <td colspan=""7"" style=""color: #999; text-align: center; font-style: italic;"">
                                    Ничего не найдено
                                </td>
                            `;
                            document.querySelector('tbody').appendChild(noResultsRow);
                            noResultsRow.style.display = 'none'; // Сначала скрыта
                        }}

                        searchInput.focus();

                        searchInput.addEventListener('input', function () {{
                            const searchTerm = searchInput.value.trim().toLowerCase();
                            let visibleRows = 0;

                            tableRows.forEach(row => {{
                                // Пропускаем строку ""Ничего не найдено""
                                if (row === noResultsRow) return;

                                // Берём нужные ячейки: Наименование, Модель, Бренд, Код ТН ВЭД, Артикул
                                const cells = [row.cells[1], row.cells[2], row.cells[3], row.cells[4], row.cells[5]];
                                const text = cells.map(c => c ? c.textContent : '').join(' ').toLowerCase();

                                if (searchTerm === '' || text.includes(searchTerm)) {{
                                    row.style.display = '';
                                    visibleRows++;
                                }} else {{
                                    row.style.display = 'none';
                                }}
                            }});

                            // Показываем/скрываем сообщение ""Ничего не найдено""
                            noResultsRow.style.display = (searchTerm !== '' && visibleRows === 0) ? '' : 'none';
                        }});
                    }});
                </script>

                </body>
                </html>", "text/html");
        }

        // Общая стилизация кнопок пагинации
        private const string ButtonStyle = @"
            display: inline-block;
            background: #3498db;
            color: white;
            padding: 10px 16px;
            border-radius: 6px;
            text-decoration: none;
            font-size: 14px;
        ";

        // Вспомогательный метод для экранирования HTML
        private static string HtmlEncode(string? value) =>
            value?.Replace("&", "&amp;")
                 .Replace("<", "<")
                 .Replace(">", ">")
                 .Replace("\"", "&quot;")
                 .Replace("'", "&#39;") ?? "";
    }
}
