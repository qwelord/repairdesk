namespace RepairDesk.Core;

public static class Compatibility
{
    public static CompatibilityResult Check(BuildSelection build)
    {
        var findings = new List<Finding>();
        var complete = new[] { build.Cpu, build.Board, build.Gpu, build.Ram, build.Psu, build.Case, build.Storage }.All(part => part != null);
        if (!complete)
            findings.Add(new(FindingLevel.Warning, "Сборка не завершена", "Выберите все семь компонентов для полной проверки."));
        if (build.Cpu is { } cpu && build.Board is { } board)
        {
            Compare(findings, cpu.Socket == board.Socket, "Сокет процессора", $"{cpu.Socket} / {board.Socket}");
            if (cpu.Socket == board.Socket)
            {
                if (!board.CpuSupport.Split('|').Contains(cpu.Name))
                    findings.Add(new(FindingLevel.Warning, "Поддержка процессора требует проверки", "Совпадения сокета недостаточно. Проверьте список CPU и минимальную версию BIOS на сайте платы."));
                else
                    findings.Add(new(FindingLevel.Warning, "Проверьте установленную версию BIOS", "Процессор указан в локальном каталоге совместимости. Сверьте минимальную версию BIOS в CPU Support производителя платы."));
            }
        }
        if (build.Board is { } memoryBoard && build.Ram is { } ram)
        {
            Compare(findings, memoryBoard.MemoryType == ram.MemoryType, "Тип оперативной памяти", $"Плата: {memoryBoard.MemoryType}; память: {ram.MemoryType}.");
            Compare(findings, ram.Capacity <= memoryBoard.MaxMemoryGb, "Объем оперативной памяти", $"Выбрано {ram.Capacity} ГБ; предел платы {memoryBoard.MaxMemoryGb} ГБ.");
        }
        if (build.Case is { } chassis && build.Board is { } caseBoard)
            Compare(findings, chassis.SupportedForms.Split('|').Contains(caseBoard.FormFactor), "Форм-фактор платы", $"{caseBoard.FormFactor}; корпус поддерживает {chassis.SupportedForms.Replace('|', ',')}.");
        if (build.Case is { } gpuCase && build.Gpu is { } gpu)
            Compare(findings, gpu.LengthMm <= gpuCase.MaxGpuMm, "Длина видеокарты", $"{gpu.LengthMm} мм; допустимо до {gpuCase.MaxGpuMm} мм без дополнительных радиаторов.");
        var power = (build.Cpu?.Wattage ?? 0) + (build.Gpu?.Wattage ?? 0) + 80;
        var recommended = (int)(Math.Ceiling(power * 1.35 / 50) * 50);
        if (build.Psu is { } psu && build.Cpu != null && build.Gpu is { } powerGpu)
        {
            Compare(findings, psu.Wattage >= recommended, "Запас мощности БП", $"Расчетная рекомендация: от {recommended} Вт; выбран БП {psu.Wattage} Вт. Это оценка, а не измерение потребления.");
            if (!string.IsNullOrWhiteSpace(powerGpu.PowerConnectors))
                Compare(findings, psu.PowerConnectors.Split('|').Contains(powerGpu.PowerConnectors), "Питание видеокарты", $"Видеокарта требует {powerGpu.PowerConnectors}; БП: {psu.PowerConnectors.Replace('|', ',')}.");
        }
        foreach (var part in build.Parts.Where(part => part.Stock == 0))
            findings.Add(new(FindingLevel.Warning, "Нет на складе", part.Name));
        findings.Add(new(FindingLevel.Warning, "Дополнительная проверка перед сборкой", "Проверьте QVL памяти, кулер и его крепления, зазоры с радиаторами, M.2-линии и питание CPU. Эти параметры не моделируются автоматически."));
        return new(findings, recommended)
        {
            IsComplete = complete
        };
    }

    private static void Compare(List<Finding> findings, bool passed, string title, string detail) => findings.Add(new(passed ? FindingLevel.Pass : FindingLevel.Error, title, detail));
}
