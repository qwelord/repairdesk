using RepairDesk.Core;
using RepairDesk.Data;

namespace RepairDesk.Api;

public record PerformanceResult(int GameId, string Game, int Width, decimal? Fps, decimal? Lower, decimal? Upper, string Kind, string Preset, string Explanation, IReadOnlyList<Benchmark> Sources);

public static class BuildEngine
{
    private static readonly string[] Categories = ["Процессор", "Материнская плата", "Видеокарта", "Память", "Блок питания", "Корпус", "Накопитель"];

    public static BuildSelection Select(IReadOnlyList<Part> parts, IReadOnlyList<int> ids)
    {
        if (ids.Count > 7 || ids.Distinct().Count() != ids.Count)
            throw new BusinessException("Выберите не более семи различных комплектующих.");
        var selected = ids.Select(id => parts.FirstOrDefault(part => part.Id == id) ?? throw new BusinessException("Комплектующая не найдена.")).ToList();
        if (selected.GroupBy(part => part.Category).Any(group => group.Count() > 1))
            throw new BusinessException("В каждой категории выбирается одна комплектующая.");
        Part? Get(string category) => selected.FirstOrDefault(part => part.Category == category);
        return new(Get(Categories[0]), Get(Categories[1]), Get(Categories[2]), Get(Categories[3]), Get(Categories[4]), Get(Categories[5]), Get(Categories[6]));
    }

    public static async Task<object> EvaluateAsync(Repository repository, IReadOnlyList<int> ids, int width)
    {
        ValidateWidth(width);
        var parts = await repository.PartsAsync();
        var build = Select(parts, ids);
        var benchmarks = await BenchmarksAsync(repository, parts);
        var games = await repository.GamesAsync();
        return new
        {
            build,
            compatibility = Compatibility.Check(build),
            performance = games.Select(game => Predict(game, build.Gpu?.Id, width, benchmarks)),
            notice = "Это ориентир по настольной видеокарте, а не замер выбранного ПК. CPU, память, драйверы и версия игры влияют на результат. Для ноутбуков с тем же названием GPU эти данные неприменимы."
        };
    }

    public static async Task<object> RecommendAsync(Repository repository, BudgetRequest request)
    {
        ValidateWidth(request.Width);
        if (request.Budget is < 10000 or > 1000000)
            throw new BusinessException("Бюджет: от 10 000 до 1 000 000 ₽.");
        var all = await repository.PartsAsync();
        var parts = all.Where(part => !request.InStockOnly || part.Stock > 0).ToList();
        var labor = (await repository.ServicesAsync()).FirstOrDefault(service => service.Name == "Сборка и проверка ПК")?.Price ?? 0;
        var benchmarks = await BenchmarksAsync(repository, all);
        var gpuIds = parts.Where(part => part.Category == "Видеокарта" && benchmarks.Any(sample => sample.GpuId == part.Id && sample.Width == request.Width)).Select(part => part.Id).ToHashSet();
        var commonGames = benchmarks.Where(sample => gpuIds.Contains(sample.GpuId) && sample.Width == request.Width).GroupBy(sample => sample.GameId).Where(group => group.Select(sample => sample.GpuId).Distinct().Count() == gpuIds.Count).Select(group => group.Key).ToHashSet();
        IEnumerable<Part> Category(string category) => parts.Where(part => part.Category == category).OrderBy(part => part.Price);
        var options = new List<(BuildSelection Build, decimal Score)>();
        foreach (var cpu in Category(Categories[0]))
            foreach (var board in Category(Categories[1]).Where(board => board.Socket == cpu.Socket))
                foreach (var gpu in Category(Categories[2]).Where(gpu => gpuIds.Contains(gpu.Id)))
                {
                    var ram = Category(Categories[3]).FirstOrDefault(part => part.MemoryType == board.MemoryType && part.Capacity >= 16 && part.Capacity <= board.MaxMemoryGb);
                    var storage = Category(Categories[6]).FirstOrDefault(part => part.Capacity >= 500);
                    var chassis = Category(Categories[5]).FirstOrDefault(part => part.MaxGpuMm >= gpu.LengthMm && part.SupportedForms.Split('|').Contains(board.FormFactor));
                    if (ram == null || storage == null || chassis == null)
                        continue;
                    foreach (var psu in Category(Categories[4]))
                    {
                        var build = new BuildSelection(cpu, board, gpu, ram, psu, chassis, storage);
                        if (build.Price + labor > request.Budget || Compatibility.Check(build).HasErrors)
                            continue;
                        var samples = benchmarks.Where(sample => sample.GpuId == gpu.Id && sample.Width == request.Width && commonGames.Contains(sample.GameId)).GroupBy(sample => sample.GameId).Select(group => group.First().AverageFps).ToList();
                        if (samples.Count == 0)
                            continue;
                        var score = (decimal)Math.Exp(samples.Average(value => Math.Log((double)value)));
                        options.Add((build, score));
                        break;
                    }
                }
        var ordered = options.OrderByDescending(option => option.Score).ThenBy(option => option.Build.Price).ToList();
        var distinct = ordered.GroupBy(option => $"{option.Build.Cpu!.Id}-{option.Build.Gpu!.Id}").Select(group => group.First()).Take(3);
        return new
        {
            budget = request.Budget,
            labor,
            proposals = distinct.Select((option, index) => new { name = index == 0 ? "Приоритет графики" : index == 1 ? "Альтернативная платформа" : "Ещё один вариант", build = option.Build, total = option.Build.Price + labor, remaining = request.Budget - option.Build.Price - labor, compatibility = Compatibility.Check(option.Build), reason = "Совместимые компоненты, 16+ ГБ памяти и накопитель от 500 ГБ. Приоритет — геометрическое среднее опубликованного FPS; при равном GPU выбирается меньшая цена. Цены каталога учебные, не рыночная оферта." }),
            message = ordered.Count == 0 ? "В этом бюджете нет полной совместимой сборки. Увеличьте бюджет или разрешите позиции под заказ." : "В стоимость включена сборка. Остатки проверяются повторно при оформлении заказа."
        };
    }

    private static void ValidateWidth(int width)
    {
        if (width is not (1920 or 2560 or 3840))
            throw new BusinessException("Разрешение: 1920, 2560 или 3840 пикселей по ширине.");
    }

    public static async Task<List<Benchmark>> BenchmarksAsync(Repository repository, IReadOnlyList<Part> parts)
    {
        return await repository.BenchmarksAsync();
    }

    public static PerformanceResult Predict(Game game, int? gpuId, int width, IReadOnlyList<Benchmark> rows)
    {
        var direct = rows.FirstOrDefault(row => row.GpuId == gpuId && row.GameId == game.Id && row.Width == width);
        if (direct != null)
            return Result(game, width, direct.AverageFps, 0, "Замер из источника", direct.Preset, "Опубликованный средний FPS на тестовом стенде источника. Это не обещание FPS на выбранном процессоре.", [direct]);
        var sameGpu = rows.Where(row => row.GpuId == gpuId && row.GameId == game.Id).GroupBy(row => (row.Preset, row.RayTracing, row.Upscaling)).FirstOrDefault(group => group.Select(row => row.Width).Distinct().Count() >= 2)?.OrderBy(row => row.Width).ToList();
        if (sameGpu is { Count: >= 2 })
        {
            var first = sameGpu[0];
            var last = sameGpu[^1];
            var exponent = Math.Log((double)(last.AverageFps / first.AverageFps)) / Math.Log((double)last.Width / first.Width);
            var prediction = first.AverageFps * (decimal)Math.Pow((double)width / first.Width, exponent);
            var interpolation = width > first.Width && width < last.Width;
            return Result(game, width, prediction, interpolation ? .15m : .30m, "Расчёт по разрешению", first.Preset, interpolation ? "Интерполяция между замерами того же GPU и пресета; ориентировочный диапазон ±15%, не статистический доверительный интервал." : "Экстраполяция замеров того же GPU и пресета; ориентировочный диапазон ±30%, не статистический доверительный интервал.", [first, last]);
        }
        foreach (var donor in rows.Where(row => row.GameId == game.Id && row.Width == width && row.GpuId != gpuId))
        {
            var pairs = rows.Where(row => row.GpuId == gpuId).Select(target => (Target: target, Donor: rows.FirstOrDefault(row => row.GpuId == donor.GpuId && row.GameId == target.GameId && row.Width == target.Width && row.Preset == target.Preset && row.RayTracing == target.RayTracing && row.Upscaling == target.Upscaling && ComparableSource(row, target)))).Where(pair => pair.Donor != null).ToList();
            if (pairs.Select(pair => pair.Target.GameId).Distinct().Count() < 2)
                continue;
            var ratios = pairs.Select(pair => pair.Target.AverageFps / pair.Donor!.AverageFps).Order().ToArray();
            var ratio = ratios[ratios.Length / 2];
            return Result(game, width, donor.AverageFps * ratio, .35m, "Расчёт по сопоставимым GPU", donor.Preset, "Медианное отношение FPS по общим играм, разрешениям, RT и масштабированию. Используются одна публикация либо одно издание с одинаковым описанием стенда. Разные даты и драйверы могут влиять на результат. Диапазон ±35% — эвристический, не гарантия и не замер.", new[] { donor }.Concat(pairs.SelectMany(pair => new[] { pair.Target, pair.Donor! })).DistinctBy(row => row.Id).ToArray());
        }
        return new(game.Id, game.Name, width, null, null, null, "Нет подтверждённых данных", "", gpuId == null ? "Выберите видеокарту." : "Нужен опубликованный тест или достаточный набор сопоставимых замеров. Приложение не подставляет выдуманный FPS.", []);
    }

    private static bool ComparableSource(Benchmark first, Benchmark second) => first.SourceUrl == second.SourceUrl || first.TestCpu == second.TestCpu && Uri.TryCreate(first.SourceUrl, UriKind.Absolute, out var firstUri) && Uri.TryCreate(second.SourceUrl, UriKind.Absolute, out var secondUri) && firstUri.Host == secondUri.Host;

    private static PerformanceResult Result(Game game, int width, decimal fps, decimal spread, string kind, string preset, string explanation, IReadOnlyList<Benchmark> sources) => new(game.Id, game.Name, width, decimal.Round(fps, 1), decimal.Round(fps * (1 - spread)), decimal.Round(fps * (1 + spread)), kind, preset, explanation, sources);
}
