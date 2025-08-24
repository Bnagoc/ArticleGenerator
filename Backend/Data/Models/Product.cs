namespace ArticleGenerator.Data.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Отсутствует";
        public string Model { get; set; } = "Отсутствует";
        public string Brand { get; set; } = "Отсутствует";
        public string TariffCode { get; set; } = "Отсутствует";
        public decimal Price { get; set; }
        public string Article { get; set; } = "Отсутствует";
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
