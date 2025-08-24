namespace ArticleGenerator.Home.Endpoints
{
    public class GetForm : IEndpoint
    {
        public static void Map(IEndpointRouteBuilder app) => app
            .MapGet("/", Handle);

        public static async Task<IResult> Handle(AppDbContext database, CancellationToken cancellationToken)
        {
            return Results.Content(@"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>📄 Генерация артикула 1С</title>
                    <meta charset='utf-8' />
                    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
                    <style>
                        /* Сброс отступов и базовые стили */
                        body, html {
                            margin: 0;
                            padding: 0;
                            height: 100%;
                            width: 100%;
                            font-family: Arial, sans-serif;
                            background: #f7f7f7;
                        }

                        /* Центрирование */
                        .flex-container {
                            display: flex;
                            justify-content: center;
                            align-items: flex-start;
                            min-height: 100vh;
                            padding: 40px 20px;
                            box-sizing: border-box;
                        }

                        .container {
                            max-width: 700px;
                            width: 100%;
                            background: white;
                            padding: 40px;
                            border-radius: 12px;
                            box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
                            text-align: center;
                        }

                        h1 {
                            color: #2c3e50;
                            margin-top: 0;
                            font-size: 28px;
                        }

                        p {
                            color: #555;
                            line-height: 1.6;
                        }

                        button {
                            background: #3498db;
                            color: white;
                            padding: 12px 24px;
                            border: none;
                            border-radius: 6px;
                            font-size: 16px;
                            cursor: pointer;
                            margin: 10px 5px;
                            transition: background 0.3s;
                        }

                        button:hover {
                            background: #2980b9;
                        }

                        input[type='file'] {
                            font-size: 16px;
                            margin: 10px 0;
                        }
                    </style>
                </head>
                <body>
                    <div class='flex-container'>
                        <div class='container'>
                            <h1>📥 Загрузите Excel-файл</h1>
                            <p>Программа прочитает данные, добавит столбец <strong>Артикул 1С</strong>, сохранит товары в базу данных и вернёт изменённый файл.</p>
                            <form method='post' action='/products/upload' enctype='multipart/form-data'>
                                <input type='file' name='file' accept='.xlsx' required />
                                <br><br>
                                <button type='submit'>📤 Обработать и сохранить</button>
                            </form>
                            <br>
                            <button onclick=""window.location.href='/products'"" style=""background: #27ae60;"">📋 Посмотреть все товары</button>
                        </div>
                    </div>
                </body>
                </html>", "text/html"
            );
        }
    }
}
