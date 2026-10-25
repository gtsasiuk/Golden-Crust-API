using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Seeding;

public static class DbInitializer
{
    private const string CdnBase = "https://cdn.goldencrust.example/img/";

    private record Seed(long CategoryId, string Name, decimal Price, string Description, string Ingredients,
                        int Weight, int Calories, string[] Images, long[] TagIds);

    private static readonly Seed[] Items =
    [
        new(1, "Маргарита",        149m, "Класична піца з томатним соусом і моцарелою.",         "тісто, томати, моцарела, базилік",              450,  980, ["margherita-1.jpg", "margherita-2.jpg"], [1, 2]),
        new(1, "Пепероні",         179m, "Соковита піца з пікантною салямі пепероні.",            "тісто, томати, моцарела, пепероні",             480, 1250, ["pepperoni-1.jpg", "pepperoni-2.jpg"],   [2]),
        new(1, "Чотири сири",      189m, "Піца з моцарелою, горгонзолою, пармезаном і чеддером.", "тісто, вершковий соус, чотири сорти сиру",      470, 1320, ["four-cheese-1.jpg"],                    [1]),
        new(1, "Діабола",          185m, "Гостра піца з салямі і перцем халапеньйо.",             "тісто, томати, моцарела, салямі, халапеньйо",   490, 1280, ["diavola-1.jpg"],                        [3]),
        new(2, "Цезар з куркою",   139m, "Салат з курячим філе, пармезаном і грінками.",          "курка, романо, пармезан, грінки, соус цезар",   280,  420, ["caesar-1.jpg"],                         []),
        new(2, "Грецький салат",   119m, "Свіжі овочі з сиром фета та оливками.",                 "огірок, томат, фета, оливки, оливкова олія",    250,  260, ["greek-1.jpg"],                          [1]),
        new(3, "Тірамісу",          99m, "Італійський десерт з маскарпоне та кавою.",             "маскарпоне, савоярді, кава, какао",             150,  390, ["tiramisu-1.jpg"],                       [1]),
        new(4, "Домашній лимонад",  59m, "Лимонад зі свіжого лимона та м'яти.",                   "лимон, м'ята, цукор, вода",                     400,  140, ["lemonade-1.jpg"],                       [1]),
    ];

    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        if (await db.MenuItems.IgnoreQueryFilters().AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        var created = new List<MenuItem>();

        foreach (var s in Items)
        {
            var item = new MenuItem(s.CategoryId, s.Name, s.Price);
            item.SetDetail(s.Description, s.Ingredients, s.Weight, s.Calories);
            foreach (var file in s.Images) item.AddImage(CdnBase + file, s.Name);
            foreach (var tagId in s.TagIds) item.AddTag(tagId);
            item.StampCreated(now, "seed");
            created.Add(item);
        }

        db.MenuItems.AddRange(created);
        await db.SaveChangesAsync(ct);

        for (var i = 0; i < created.Count; i++)
            if (created[i].Id != i + 1)
                throw new InvalidOperationException(
                    $"Seed expects MenuItem Id {i + 1} for '{created[i].Name}', got {created[i].Id}. " +
                    "Recreate the Catalog database (identity counter is not at its initial value).");
    }
}